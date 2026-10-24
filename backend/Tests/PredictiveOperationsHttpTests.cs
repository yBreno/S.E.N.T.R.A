using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Enums;
using Domain.IRepositories;
using Application.Interfaces.IServices;
using Application.DTOs.Predictive;
using Application.Services;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Support;
namespace Tests;

public class PredictiveOperationsHttpTests
{
    [Fact]
    public async Task Cancelamento_da_analise_real_registra_falha_sem_contagem_e_libera_semaforo()
    {
        await using var host = await EquipmentTestHost.StartAsync();
        using var scope = host.App.Services.CreateScope(); var services = scope.ServiceProvider;
        var blocker = new EquipmentRiskBlocker();
        var service = new PredictiveAnalysisService(services.GetRequiredService<IFailurePredictionService>(), services.GetRequiredService<IAnomalyService>(),
            services.GetRequiredService<IRiskService>(), services.GetRequiredService<IRecommendationService>(), services.GetRequiredService<IAlertRepository>(),
            services.GetRequiredService<IUnitOfWork>(), blocker, services.GetRequiredService<PredictiveExecutionGate>(),
            services.GetRequiredService<IPredictiveWriteCoordinator>(), services.GetRequiredService<PredictiveExecutionState>());
        using var cancellation = new CancellationTokenSource();
        var task = service.RunAutomaticAsync(cancellation.Token); await blocker.Entered.Task;
        Assert.Equal("Running", services.GetRequiredService<PredictiveExecutionState>().Snapshot().Status);
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        var failed = services.GetRequiredService<PredictiveExecutionState>().Snapshot();
        Assert.Equal("Failed", failed.Status); Assert.Equal("Automatic", failed.Origin);
        Assert.Null(failed.LastAutomaticCompletedAt); Assert.Null(failed.RecommendationsCreated);
        await services.GetRequiredService<IPredictiveAnalysisService>().RunAsync();
        Assert.Equal("Completed", services.GetRequiredService<PredictiveExecutionState>().Snapshot().Status);
    }
    [Fact]
    public async Task Falha_do_repositorio_na_analise_real_nao_publica_contagem_de_sucesso()
    {
        await using var host = await EquipmentTestHost.StartAsync();
        using var scope = host.App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPredictiveAnalysisService>();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.RunAsync());
        var state = scope.ServiceProvider.GetRequiredService<PredictiveExecutionState>().Snapshot();
        Assert.Equal("Failed", state.Status); Assert.Null(state.RecommendationsCreated); Assert.Null(state.LastCompletedAt);
    }
    private sealed class EquipmentRiskBlocker : IEquipmentRiskService
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IReadOnlyList<EquipmentRiskDto>> GetAllAsync(CancellationToken ct = default)
        {
            Entered.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); return [];
        }
    }
    [Theory]
    [InlineData(null, 401)] [InlineData(UserRole.Agent, 403)] [InlineData(UserRole.MaintenanceTechnician, 403)]
    [InlineData(UserRole.CentralOperator, 200)] [InlineData(UserRole.OperationManager, 200)] [InlineData(UserRole.Admin, 200)]
    public async Task Consulta_operacional_e_estado_exigem_perfil_de_monitoramento(UserRole? role, int expected)
    {
        await using var host = await EquipmentTestHost.StartAsync();
        foreach (var path in new[] { "/api/dashboard/predictive", "/api/predictions/execution-status" })
        {
            using var request = host.Request(HttpMethod.Get, path, role);
            using var response = await host.Client.SendAsync(request); Assert.Equal(expected, (int)response.StatusCode);
        }
    }
    [Fact]
    public async Task Leituras_nao_analisam_nao_gravam_e_distinguem_risco_alto_de_falha_real()
    {
        await using var host = await EquipmentTestHost.StartAsync();
        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var e = EquipmentRiskTests.Equipamento();
            e.MaintenanceNeed = MaintenanceNeed.Periodic; e.NextMaintenanceDate = DateTime.UtcNow.AddDays(-2);
            db.Add(e); db.AddRange(EquipmentRiskTests.Falhas(e.Id, DateTime.UtcNow)); await db.SaveChangesAsync();
        }
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = host.Request(HttpMethod.Get, "/api/dashboard/predictive", UserRole.CentralOperator);
            using var response = await host.Client.SendAsync(request); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var data = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Operational", data.GetProperty("equipmentRisks")[0].GetProperty("status").GetString());
            Assert.Equal("High", data.GetProperty("equipmentRisks")[0].GetProperty("level").GetString());
            Assert.Equal("NotRun", data.GetProperty("automation").GetProperty("execution").GetProperty("status").GetString());
            Assert.False(data.GetProperty("automation").GetProperty("enabled").GetBoolean());
            Assert.EndsWith("Z", data.GetProperty("evaluatedAt").GetString());
        }
        using var final = host.App.Services.CreateScope(); var context = final.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await context.Recommendations.ToListAsync()); Assert.Empty(await context.MaintenanceRecords.ToListAsync());
        Assert.Equal(3, await context.Alerts.CountAsync()); Assert.Equal(EquipmentStatus.Operational, (await context.Equipments.SingleAsync()).Status);
    }
    [Fact]
    public async Task Analise_manual_atualiza_estado_somente_com_contagens_concluidas()
    {
        await using var host = await EquipmentTestHost.StartAsync();
        using var analyze = host.Request(HttpMethod.Post, "/api/predictions/analyze", UserRole.CentralOperator);
        using var response = await host.Client.SendAsync(analyze); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        using var status = host.Request(HttpMethod.Get, "/api/predictions/execution-status", UserRole.CentralOperator);
        var snapshot = await (await host.Client.SendAsync(status)).Content.ReadFromJsonAsync<JsonElement>();
        var execution = snapshot.GetProperty("execution"); Assert.Equal("Completed", execution.GetProperty("status").GetString());
        Assert.Equal("Manual", execution.GetProperty("origin").GetString()); Assert.Equal(JsonValueKind.Null, execution.GetProperty("lastAutomaticCompletedAt").ValueKind);
        Assert.Equal(result.GetProperty("recommendationsCreated").GetInt32(), execution.GetProperty("recommendationsCreated").GetInt32());
    }
}
