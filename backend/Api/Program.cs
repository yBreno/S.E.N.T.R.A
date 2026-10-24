using Api.Security;
using System.Text.Json.Serialization;
using Api.Middleware;
using Api.Demo;
using Api.Seed;
using Api.Workers;
using Application;
using Application.Predictive;
using Application.Security;
using Infrastructure;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
var isDemo = DemoApiConfiguration.Prepare(builder.Configuration, builder.Environment);
if (isDemo) builder.WebHost.UseUrls(DemoApiConfiguration.Url);

// ---- Camadas
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
DemoApiConfiguration.RegisterReadOnlyPersistence(builder.Services, isDemo, builder.Configuration.GetValue<bool>("Demo:InteractiveAcceptance"));

// ---- Cidade Preditiva (pesos e limiares auditaveis + worker de telemetria simulada)
builder.Services.Configure<PredictiveSettings>(builder.Configuration.GetSection(PredictiveSettings.SectionName));
builder.Services.Configure<SimulationSettings>(builder.Configuration.GetSection(SimulationSettings.SectionName));
DemoApiConfiguration.RegisterSimulation(builder.Services, isDemo);
builder.Services.AddOptions<PredictiveAutomationSettings>()
    .Bind(builder.Configuration.GetSection(PredictiveAutomationSettings.SectionName))
    .Validate(s => s.HasValidInterval, "PredictiveAutomation:IntervalMinutes deve estar entre 15 e 1440 minutos.")
    .Validate(s => !isDemo || !s.Enabled, "Ambiente Demo nao permite automacao preditiva.")
    .ValidateOnStart();
if (!isDemo) builder.Services.AddHostedService<PredictiveAutomationWorker>();

// ---- JWT (a Key vem de user-secrets / variavel de ambiente: Jwt__Key)
var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
builder.Services.AddSentraJwt(jwt);
builder.Services.AddAuthorization();

// ---- CORS
var origins = isDemo ? DemoApiConfiguration.Origins : builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddPolicy("Default", p =>
{
    if (builder.Environment.IsDevelopment())
        p.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod();
    else
        p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

// ---- MVC + Swagger (esquema Bearer)
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "F.O.G.O. API - Cidade Preditiva", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Informe apenas o token JWT."
    });
    o.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc)] = []
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger disponível no Azure
app.UseSwagger();
app.UseSwaggerUI();

if (DemoApiConfiguration.RunDevelopmentSeeders(app.Environment, isDemo))
{
    await AdminSeeder.SeedAsync(app.Services, app.Configuration, app.Logger);
    await PredictiveSeeder.SeedAsync(app.Services, app.Logger);
}
else if (DemoApiConfiguration.RunAdminSeeder(app.Configuration, isDemo))
{
    await AdminSeeder.SeedAsync(app.Services, app.Configuration, app.Logger);
}

if (!isDemo) app.UseHttpsRedirection();
app.UseCors("Default");
if (isDemo) app.UseMiddleware<DemoReadOnlyMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
