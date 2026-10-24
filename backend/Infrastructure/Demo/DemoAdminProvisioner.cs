using System.Data;
using System.Text;
using Application.DTOs.Auth;
using Application.Security;
using Application.Validators;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Demo;

/// <summary>Explicit out-of-band bootstrap; never registered or invoked by the API.</summary>
public static class DemoAdminProvisioner
{
    public static async Task<int> CreateEvaluationUsersAsync(AppDbContext db, string password, string environment, bool confirmed, CancellationToken ct = default)
    {
        if (environment != "Demo" || !confirmed || db.ChangeTracker.HasChanges()) throw new InvalidOperationException("Provisionamento exige Demo, confirmação e contexto exclusivo.");
        var sql = db.Database.IsSqlServer();
        if (sql) DemoEvaluationTargetGuard.Validate(environment, db.Database.GetConnectionString() ?? "");
        else if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory") throw new InvalidOperationException("Provider não autorizado.");
        if (Encoding.UTF8.GetByteCount(password) > 72) throw new InvalidOperationException("Senha excede 72 bytes.");
        var roles = Enum.GetValues<UserRole>();
        foreach (var role in roles)
        {
            var dto = new RegisterDto { Name = $"DEMO {role}", Email = $"{role.ToString().ToLowerInvariant()}.s07@sentra.invalid", Cpf = DemoEvaluationPlan.SyntheticDocument((int)role), Password = password, Role = role };
            var failures = new RegisterDtoValidator().Validate(dto).Errors.Where(error => error.PropertyName != nameof(RegisterDto.Cpf)).ToList();
            if (failures.Count > 0) throw new ValidationException(failures);
        }
        if (sql && (await db.Database.GetPendingMigrationsAsync(ct)).Any()) throw new InvalidOperationException("Schema pendente; não há migration automática.");
        await using var transaction = sql ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        try
        {
            var existing = await db.Users.AsNoTracking().ToListAsync(ct);
            if (existing.Any(user => !roles.Any(role => user.Id == DemoEvaluationPlan.Id("user", (int)role) && user.Role == role && user.Email == $"{role.ToString().ToLowerInvariant()}.s07@sentra.invalid" && user.Cpf == DemoEvaluationPlan.SyntheticDocument((int)role) && user.Name == $"DEMO {role}")))
                throw new InvalidOperationException("Usuário externo ao perfil de avaliação; nenhuma sobrescrita permitida.");
            var inserted = 0;
            foreach (var role in roles)
            {
                var id = DemoEvaluationPlan.Id("user", (int)role);
                if (existing.Any(user => user.Id == id)) continue;
                var user = User.Create($"DEMO {role}", $"{role.ToString().ToLowerInvariant()}.s07@sentra.invalid", DemoEvaluationPlan.SyntheticDocument((int)role), new BCryptPasswordHasher().Hash(password), role);
                user.Id = id; db.Users.Add(user); inserted++;
            }
            await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct); return inserted;
        }
        catch { if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }

    public const string Database = "SENTRA_DEMO_Stage73";
    public const string Email = "admin.demo@sentra.invalid";

    public static RegisterDto ValidatePassword(string password)
    {
        if (Encoding.UTF8.GetByteCount(password) > 72)
            throw new InvalidOperationException("A senha excede o limite seguro de 72 bytes do BCrypt.");
        var digits = "900000101"; // Algorithmic synthetic document, never sourced from a person.
        for (var size = 9; size <= 10; size++)
        {
            var check = digits.Select((digit, i) => (digit - '0') * (size + 1 - i)).Sum() * 10 % 11;
            digits += check == 10 ? "0" : check.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        var dto = new RegisterDto { Name = "DEMO Administrador", Email = Email, Cpf = digits, Password = password, Role = UserRole.Admin };
        new RegisterDtoValidator().ValidateAndThrow(dto);
        return dto;
    }

    public static async Task CreateAsync(AppDbContext db, string password, string environment, bool confirmed,
        CancellationToken ct = default)
    {
        if (environment != "Demo" || !confirmed) throw new InvalidOperationException("Provisionamento DEMO exige confirmacao explicita.");
        var sql = db.Database.IsSqlServer();
        if (sql) DemoTargetGuard.Validate(environment, db.Database.GetConnectionString() ?? "", Database);
        else if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            throw new InvalidOperationException("Provider nao autorizado.");
        if (db.ChangeTracker.HasChanges()) throw new InvalidOperationException("Use um contexto exclusivo sem alteracoes pendentes.");
        var dto = ValidatePassword(password);
        if (sql)
        {
            await db.Database.OpenConnectionAsync(ct);
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT CASE WHEN CONVERT(int,SERVERPROPERTY('IsLocalDB'))=1 AND DB_NAME()=N'SENTRA_DEMO_Stage73' THEN 1 ELSE 0 END";
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 1) throw new InvalidOperationException("Destino nao confirmado pelo servidor.");
            if ((await db.Database.GetPendingMigrationsAsync(ct)).Any()) throw new InvalidOperationException("Schema pendente; nenhuma migration sera executada.");
        }
        await using var transaction = sql ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        try
        {
            if (await db.Users.AnyAsync(ct)) throw new InvalidOperationException("Ja existem usuarios; nenhum usuario sera criado ou alterado.");
            // Deliberately no AuthService.Register elevation bypass exposed by HTTP; this is a local administrative command.
            var hasher = new BCryptPasswordHasher();
            db.Users.Add(User.Create(dto.Name, dto.Email, dto.Cpf, hasher.Hash(password), UserRole.Admin));
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
