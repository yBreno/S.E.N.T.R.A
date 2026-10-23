using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Api.Controllers;
using Api.Middleware;
using Application;
using Application.Security;
using Domain.Entities;
using Domain.Enums;
using Domain.IRepositories;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Tests.Support;

/// <summary>Host exclusivamente de teste: controllers e JWT reais; InMemory por padrao ou banco LocalDB com nome restrito.
/// Nao inicia Program da API, nao le conexoes privadas, nao registra workers, seeders ou migrations.</summary>
public sealed class EquipmentTestHost : IAsyncDisposable
{
    public WebApplication App { get; }
    public HttpClient Client { get; }
    private readonly JwtSettings jwt;
    private EquipmentTestHost(WebApplication app, JwtSettings settings)
    {
        App = app;
        jwt = settings;
        Client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    }

    public static async Task<EquipmentTestHost> StartAsync(int port = 0, string? localSqlDatabase = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ContentRootPath = Path.GetTempPath(), Args = [] });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        var settings = new JwtSettings { Key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), Issuer = "sentra-sprint-tests", Audience = "sentra-sprint-tests" };
        builder.Services.AddApplication();
        builder.Services.Configure<JwtSettings>(options => { options.Key = settings.Key; options.Issuer = settings.Issuer; options.Audience = settings.Audience; });
        var databaseName = $"Sprint01_{Guid.NewGuid():N}";
        if (localSqlDatabase is not null && !System.Text.RegularExpressions.Regex.IsMatch(localSqlDatabase, "^SENTRA_Sprint0[34]_[A-Za-z0-9_]+$"))
            throw new ArgumentException("O banco de teste deve ter um nome exclusivo com prefixo SENTRA_Sprint03_ ou SENTRA_Sprint04_.");
        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            if (localSqlDatabase is null) options.UseInMemoryDatabase(databaseName);
            else options.UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={localSqlDatabase};Trusted_Connection=True;TrustServerCertificate=True");
        });
        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        builder.Services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        builder.Services.AddScoped<IAddressRepository, AddressRepository>();
        builder.Services.AddScoped<IAgentRepository, AgentRepository>();
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IAlertRepository, AlertRepository>();
        builder.Services.AddScoped<IMaintenanceRecordRepository, MaintenanceRecordRepository>();
        builder.Services.AddScoped<IIncidentRepository, IncidentRepository>();
        builder.Services.AddScoped<IRecommendationRepository, RecommendationRepository>();
        builder.Services.AddScoped<IAgentPositionLogRepository, AgentPositionLogRepository>();
        builder.Services.AddControllers().AddApplicationPart(typeof(EquipmentsController).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = settings.Issuer, ValidateAudience = true, ValidAudience = settings.Audience,
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
                ValidateLifetime = true, RoleClaimType = "role", NameClaimType = "sub", ClockSkew = TimeSpan.Zero
            };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins("http://127.0.0.1:5405").AllowAnyHeader().AllowAnyMethod()));
        var app = builder.Build();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseCors(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        return new EquipmentTestHost(app, settings);
    }

    public string Token(UserRole role, Guid? userId = null)
    {
        var user = User.Create("Usuario sintetico de teste", "test@sentra.invalid", "", "unused", role);
        if (userId.HasValue) user.Id = userId.Value;
        return new JwtTokenService(Options.Create(jwt)).GenerateToken(user).Token;
    }

    public HttpRequestMessage Request(HttpMethod method, string path, UserRole? role, Guid? userId = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (role.HasValue) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(role.Value, userId));
        return request;
    }

    public async ValueTask DisposeAsync() { Client.Dispose(); await App.StopAsync(); await App.DisposeAsync(); }
}
