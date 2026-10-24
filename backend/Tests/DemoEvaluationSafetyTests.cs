using System.Security.Claims;
using System.Security.Cryptography;
using Api.Demo;
using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Demo;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
namespace Tests;

public class DemoEvaluationSafetyTests
{
    private static ConfigurationManager Settings() => new()
    {
        ["Demo:Enabled"] = "true", ["Demo:InteractiveAcceptance"] = "true", ["Simulation:Enabled"] = "false", ["SeedAdmin:Enabled"] = "false",
        ["PredictiveAutomation:Enabled"] = "false", ["urls"] = DemoApiConfiguration.Url,
        ["SENTRA_DEMO_DATABASE"] = "SENTRA_DEMO_Avaliacao_Teste", ["SENTRA_DEMO_CONNECTION_STRING"] = @"Server=(localdb)\MSSQLLocalDB;Database=SENTRA_DEMO_Avaliacao_Teste;Integrated Security=true",
        ["SENTRA_DEMO_JWT_KEY"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
    };
    [Fact]
    public void Opt_in_de_aceite_exige_demo_local_separado_e_preserva_o_modo_padrao()
    {
        var settings = Settings(); Assert.True(DemoApiConfiguration.Prepare(settings, new EnvironmentStub("Demo")));
        Assert.Equal(settings["SENTRA_DEMO_CONNECTION_STRING"], settings.GetConnectionString("DefaultConnection"));
        Assert.False(new DemoInteractionSettings().InteractiveAcceptance);
    }
    [Theory]
    [InlineData("Production")] [InlineData("Development")]
    public void Opcoes_de_interacao_nao_sao_aceitas_em_outros_ambientes(string environment) =>
        Assert.Throws<InvalidOperationException>(() => DemoApiConfiguration.Prepare(Settings(), new EnvironmentStub(environment)));
    [Theory]
    [InlineData("Simulation:Enabled", "true")]
    [InlineData("PredictiveAutomation:Enabled", "true")]
    [InlineData("SENTRA_DEMO_DATABASE", "SENTRA_DEMO_Stage73")]
    [InlineData("SENTRA_DEMO_CONNECTION_STRING", "Server=forbidden.database.windows.net;Database=SENTRA_DEMO_Avaliacao_Teste;Integrated Security=true")]
    public void Opt_in_recusa_automacoes_e_destinos_ambiguos_antes_de_conectar(string key, string value)
    {
        var settings = Settings(); settings[key] = value;
        Assert.Throws<InvalidOperationException>(() => DemoApiConfiguration.Prepare(settings, new EnvironmentStub("Demo")));
    }
    [Fact]
    public void Politica_de_gravacao_exige_rota_exata_opt_in_e_gestor_autenticado()
    {
        var context = new DefaultHttpContext(); context.Request.Method = "POST"; context.Request.Path = $"/api/recommendations/{DemoEvaluationPlan.PreventiveRecommendationId}/accept";
        Assert.False(DemoInteractionSettings.CanPersist(context, true));
        foreach (var role in new[] { "Admin", "OperationManager", "CentralOperator", "MaintenanceTechnician", "Agent" })
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "teste"));
            Assert.Equal(role is "Admin" or "OperationManager", DemoInteractionSettings.CanPersist(context, true));
            Assert.False(DemoInteractionSettings.CanPersist(context, false));
        }
        context.Request.Path = $"/api/recommendations/{Guid.NewGuid()}/accept"; Assert.False(DemoInteractionSettings.IsAcceptanceRequest(context, true));
        context.Request.Path = "/api/predictions/analyze"; Assert.False(DemoInteractionSettings.IsAcceptanceRequest(context, true));
    }
    [Fact]
    public async Task Interceptor_bloqueia_entidades_fora_do_aceite_mesmo_com_callback_permissivo()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new DemoReadOnlyInterceptor(() => true)).Options);
        db.Addresses.Add(new Address { Street = "DEMO alteração proibida" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task Preparacao_de_schema_exige_confirmacao_e_nunca_aceita_azure()
    {
        const string forbidden = "Server=forbidden.database.windows.net;Database=SENTRA_DEMO_Avaliacao_Teste;Integrated Security=true";
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoEvaluationSchema.PrepareNewAsync(forbidden, "SENTRA_DEMO_Avaliacao_Teste", "Demo", true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoEvaluationSchema.PrepareNewAsync(forbidden, "SENTRA_DEMO_Avaliacao_Teste", "Demo", false));
    }
    private sealed class EnvironmentStub(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "DemoTest";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
