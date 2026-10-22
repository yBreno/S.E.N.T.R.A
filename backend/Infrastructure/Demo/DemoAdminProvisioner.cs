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
