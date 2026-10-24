using System.Globalization;
using System.Security.Claims;
using System.Reflection;
using Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Api.Security;
using Application.Security;
using Application.DTOs.Auth;
using Application.Validators;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Tests;

public class AuthenticationCompatibilityTests
{
    [Fact]
    public void Cargos_e_categorias_preservam_nomes_e_valores_persistidos()
    {
        Assert.Equal(new[] { "Admin", "OperationManager", "CentralOperator", "MaintenanceTechnician", "Agent" }, Enum.GetNames<UserRole>());
        Assert.Equal(new[] { "TrafficAgent", "MunicipalGuard", "PoliceOfficer" }, Enum.GetNames<AgentRole>());
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, Enum.GetValues<UserRole>().Select(r => (int)r));
        Assert.Equal(new[] { Roles.Admin, Roles.OperationManager, Roles.CentralOperator, Roles.MaintenanceTechnician, Roles.Agent }, Enum.GetNames<UserRole>());
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=SENTRA_Metadata_Only;Integrated Security=true").Options);
        Assert.False(db.Database.HasPendingModelChanges()); // Apenas metadata, não abre conexão.
        var converter = db.Model.FindEntityType(typeof(User))!.FindProperty(nameof(User.Role))!.GetTypeMapping().Converter!;
        Assert.All(Enum.GetValues<UserRole>(), role => Assert.Equal(role.ToString(), converter.ConvertToProvider(role)));
    }
    [Fact]
    public void Dominio_rejeita_cargo_inexistente_sem_mutar_o_usuario()
    {
        Assert.Throws<BusinessRuleException>(() => User.Create("DEMO", "x@sentra.invalid", "", "unused", (UserRole)99));
        var user = User.Create("DEMO", "x@sentra.invalid", "", "unused", UserRole.Agent);
        Assert.Throws<BusinessRuleException>(() => user.ChangeRole((UserRole)99)); Assert.Equal(UserRole.Agent, user.Role);
        Assert.Throws<UnauthorizedException>(() => new ClaimsPrincipal().GetUserId());
    }
    [Fact]
    public async Task Normalizacao_de_email_e_independente_da_cultura_do_servidor()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.Add(User.Create("DEMO", "I@sentra.invalid", "", "unused", UserRole.Agent)); await db.SaveChangesAsync();
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR"); var repo = new UserRepository(db);
            Assert.True(await repo.EmailExistsAsync("I@sentra.invalid")); Assert.NotNull(await repo.GetByEmailAsync("I@sentra.invalid"));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    [Fact]
    public void Bcrypt_nao_aceita_sufixo_ignorado_apos_72_bytes()
    {
        var hasher = new BCryptPasswordHasher(); var prefix = new string('A', 68) + "a1!b";
        var hash = hasher.Hash(prefix);
        Assert.False(hasher.Verify(prefix + "sufixo", hash));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Limite_de_senha_conta_bytes_UTF8_em_validadores_e_no_hasher(bool unicode)
    {
        var password = unicode ? new string('é', 35) + "A1!" : new string('A', 70) + "a1!";
        Assert.False(PasswordLimits.Accepts(password));
        var dto = new RegisterDto { Name = "DEMO", Email = "demo@sentra.invalid", Cpf = "52998224725", Password = password };
        Assert.Contains(new RegisterDtoValidator().Validate(dto).Errors, e => e.PropertyName == "Password");
        Assert.False(new LoginDtoValidator().Validate(new LoginDto { Email = dto.Email, Password = password }).IsValid);
        Assert.Throws<BusinessRuleException>(() => new BCryptPasswordHasher().Hash(password));
        Assert.True(PasswordLimits.Accepts(new string('é', 36)));
    }
    [Fact]
    public void Todas_as_actions_exigem_autorizacao_exceto_login_e_registro_documentados()
    {
        var known = Enum.GetNames<UserRole>(); var anonymous = new List<string>();
        foreach (var controller in typeof(AuthController).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t)))
        foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any()))
        {
            var authorization = controller.GetCustomAttributes<AuthorizeAttribute>().Concat(action.GetCustomAttributes<AuthorizeAttribute>()).ToArray();
            if (action.GetCustomAttribute<AllowAnonymousAttribute>() is not null) anonymous.Add(controller.Name + "." + action.Name);
            else Assert.NotEmpty(authorization);
            foreach (var attribute in authorization.Where(a => a.Roles is not null))
                Assert.All(attribute.Roles!.Split(','), role => Assert.Contains(role.Trim(), known));
        }
        Assert.Equal(new[] { "AuthController.Login", "AuthController.Register" }, anonymous.Order());
    }
}
