using System.Text.Json;
using System.Net.Http.Json;
using Api.Middleware;
using Api.Workers;
using Application.Mappings;
using Application.Predictive;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Tests.Support;
namespace Tests;

public sealed class FinalQualityTests
{
    [Fact]
    public async Task Perda_de_conexao_aceita_portugues_acentuado_ascii_ingles_e_alias_legado()
    {
        foreach (var eventType in new[] { "Conexão Perdida", "Conexao Perdida", "ConnectionLost", "Conex\u00c3\u00a3o Perdida" })
        {
            await using var host = await EquipmentTestHost.StartAsync(); Guid id;
            using (var scope = host.App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var equipment = EquipmentRiskTests.Equipamento();
                if (equipment.Status == EquipmentStatus.NotInstalled) equipment.ChangeStatus(EquipmentStatus.Operational);
                db.Equipments.Add(equipment); await db.SaveChangesAsync(); id = equipment.Id;
            }
            using var request = host.Request(HttpMethod.Post, "/api/telemetry", UserRole.Admin);
            request.Content = JsonContent.Create(new { equipmentId = id, eventType, timestamp = DateTime.UtcNow });
            using var response = await host.Client.SendAsync(request); Assert.True(response.IsSuccessStatusCode);
            Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("triggeredIncident").GetBoolean(), eventType);
        }
    }
    [Fact]
    public void Simulacao_e_automacao_sao_desabilitadas_sem_opt_in_explicito()
    {
        Assert.False(new SimulationSettings().Enabled);
        Assert.False(new PredictiveAutomationSettings().Enabled);
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json")).Build();
        Assert.False(configuration.GetSection("Simulation").Get<SimulationSettings>()!.Enabled);
        Assert.False(configuration.GetSection("PredictiveAutomation").Get<PredictiveAutomationSettings>()!.Enabled);
    }
    [Theory]
    [InlineData(DateTimeKind.Unspecified)] [InlineData(DateTimeKind.Utc)] [InlineData(DateTimeKind.Local)]
    public void Ocorrencias_posicoes_e_recomendacoes_serializam_instantes_UTC_sem_mutar_entidades(DateTimeKind kind)
    {
        var date = new DateTime(2026, 10, 25, 14, 30, 00, kind).AddTicks(1234567);
        var expected = kind == DateTimeKind.Local ? date.ToUniversalTime() : DateTime.SpecifyKind(date, DateTimeKind.Utc);
        var incident = new Incident { OccurredAt = date };
        var position = new AgentPositionLog { RecordedAt = date };
        var recommendation = new Recommendation { CreatedAt = date, ResolvedAt = date };
        var telemetry = new TelemetryLog { Timestamp = date };
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var incidentResponse = incident.ToResponse(); var positionResponse = position.ToResponse(); var recommendationResponse = recommendation.ToResponse();
        var telemetryResponse = telemetry.ToResponse();
        Assert.Equal(expected, incidentResponse.OccurredAt); Assert.Equal(DateTimeKind.Utc, incidentResponse.OccurredAt.Kind);
        Assert.Equal(expected, positionResponse.RecordedAt); Assert.Equal(DateTimeKind.Utc, positionResponse.RecordedAt.Kind);
        Assert.Equal(expected, recommendationResponse.CreatedAt); Assert.Equal(DateTimeKind.Utc, recommendationResponse.CreatedAt.Kind);
        Assert.Equal(expected, recommendationResponse.ResolvedAt);
        Assert.Equal(expected, telemetryResponse.Timestamp); Assert.Equal(DateTimeKind.Utc, telemetryResponse.Timestamp.Kind);
        Assert.EndsWith("Z", JsonSerializer.SerializeToElement(incidentResponse, jsonOptions).GetProperty("occurredAt").GetString());
        Assert.EndsWith("Z", JsonSerializer.SerializeToElement(positionResponse, jsonOptions).GetProperty("recordedAt").GetString());
        Assert.Equal(kind, incident.OccurredAt.Kind); Assert.Equal(kind, position.RecordedAt.Kind); Assert.Equal(kind, recommendation.CreatedAt.Kind);
    }
    [Fact]
    public async Task Erro_inesperado_nao_expoe_conteudo_privado_na_resposta_nem_no_logger()
    {
        const string secret = "sintetico-email-senha-token-NAO-REGISTRAR";
        var logger = new RecordingLogger(); var context = new DefaultHttpContext();
        context.Request.Method = "POST"; context.Request.Path = "/api/auth/password-recovery/confirm";
        context.TraceIdentifier = "quality-synthetic-trace"; context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => throw new InvalidOperationException(secret), logger);
        await middleware.InvokeAsync(context); context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal(500, context.Response.StatusCode);
        Assert.DoesNotContain(secret, body); Assert.DoesNotContain(secret, logger.Content);
        Assert.Contains("quality-synthetic-trace", logger.Content); Assert.Contains(nameof(InvalidOperationException), logger.Content);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }
    private sealed class RecordingLogger : ILogger<ExceptionHandlingMiddleware>
    {
        public string Content = "";
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Content += formatter(state, exception) + exception?.ToString();
    }
}
