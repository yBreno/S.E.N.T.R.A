using Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Demo;

/// <summary>Preparação explícita de banco novo local; não carrega dados nem altera banco já existente.</summary>
public static class DemoEvaluationSchema
{
    private static readonly string[] Manifest = ["20261018170813_InitialCreate", "20261018220253_RemoveOrganization", "20261019031247_AddPredictiveCity", "20261023214553_AddEquipmentCategory", "20261023224842_AddMaintenanceScheduling"];
    public static async Task PrepareNewAsync(string connection, string database, string? environment, bool confirmed, CancellationToken ct = default)
    {
        if (!confirmed) throw new InvalidOperationException("Criação exige confirmação explícita.");
        DemoEvaluationTargetGuard.Validate(environment, connection, database);
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
        if (!db.Database.GetMigrations().SequenceEqual(Manifest)) throw new InvalidOperationException("Manifesto de migrations mudou; revisar antes de criar o banco.");
        var settings = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master" };
        await using var master = new SqlConnection(settings.ConnectionString); await master.OpenAsync(ct);
        await using (var command = master.CreateCommand())
        {
            command.CommandText = "IF ISNULL(CONVERT(int,SERVERPROPERTY('IsLocalDB')),0) <> 1 THROW 51000, 'LocalDB required', 1; " +
                "IF DB_ID(@database) IS NOT NULL THROW 51001, 'Target already exists: stop', 1; " +
                "DECLARE @sql nvarchar(max)=N'CREATE DATABASE '+QUOTENAME(@database); EXEC sys.sp_executesql @sql;";
            command.Parameters.AddWithValue("@database", database); await command.ExecuteNonQueryAsync(ct);
        }
        await db.Database.MigrateAsync(ct);
    }
}
