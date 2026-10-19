using System.Text;
using System.Text.Json.Serialization;
using Api.Middleware;
using Api.Seed;
using Api.Workers;
using Application;
using Application.Predictive;
using Application.Security;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ---- Camadas
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---- Cidade Preditiva (pesos e limiares auditaveis + worker de telemetria simulada)
builder.Services.Configure<PredictiveSettings>(builder.Configuration.GetSection(PredictiveSettings.SectionName));
builder.Services.Configure<SimulationSettings>(builder.Configuration.GetSection(SimulationSettings.SectionName));
builder.Services.AddHostedService<TelemetrySimulationWorker>();

// ---- JWT (a Key vem de user-secrets / variavel de ambiente: Jwt__Key)
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key ausente ou curta (minimo 32 caracteres). Use: dotnet user-secrets set \"Jwt:Key\" \"<chave>\" --project Api");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // mantem "sub", "email", "role" como no token
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();

// ---- CORS
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
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

//Deixar fora pro Swagger rodar no link do azure
app.UseSwagger();    
app.UseSwaggerUI();
if (app.Environment.IsDevelopment())
{
    
    await AdminSeeder.SeedAsync(app.Services, app.Configuration, app.Logger);
    await PredictiveSeeder.SeedAsync(app.Services, app.Logger);
}

app.UseHttpsRedirection();
app.UseCors("Default");
app.UseAuthentication(); // sempre antes de UseAuthorization
app.UseAuthorization();
app.MapControllers();
app.Run();
