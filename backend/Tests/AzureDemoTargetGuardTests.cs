using Tests.Support;
using Microsoft.Data.SqlClient;

namespace Tests;

public class AzureDemoTargetGuardTests
{
    private static string Connection(string server = "sentra.database.windows.net", string database = "SENTRA")
    {
        return new SqlConnectionStringBuilder
        {
            DataSource = $"tcp:{server},1433", InitialCatalog = database, UserID = "deployment-loader",
            Password = "not-a-real-secret", Encrypt = true, TrustServerCertificate = false
        }.ConnectionString;
    }

    [Fact]
    public void Requires_explicit_Azure_target_and_tls()
    {
        AzureDemoTargetGuard.Validate(AzureDemoTargetGuard.Environment, Connection(),
            "sentra.database.windows.net", "SENTRA");
    }

    [Theory]
    [InlineData("Demo", "sentra.database.windows.net", "SENTRA")]
    [InlineData("AzureSqlDemo", "other.database.windows.net", "SENTRA")]
    [InlineData("AzureSqlDemo", "sentra.database.windows.net", "master")]
    public void Rejects_wrong_environment_server_or_database(string environment, string server, string database)
    {
        Assert.Throws<InvalidOperationException>(() =>
            AzureDemoTargetGuard.Validate(environment, Connection(), server, database));
    }

    [Fact]
    public void Rejects_localdb_and_untrusted_tls()
    {
        var local = new SqlConnectionStringBuilder(Connection())
        {
            DataSource = @"(localdb)\MSSQLLocalDB", TrustServerCertificate = true
        };
        Assert.Throws<InvalidOperationException>(() =>
            AzureDemoTargetGuard.Validate(AzureDemoTargetGuard.Environment, local.ConnectionString,
                "sentra.database.windows.net", "SENTRA"));
    }
}
