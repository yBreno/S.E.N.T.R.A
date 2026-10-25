using Microsoft.Data.SqlClient;

namespace Tests.Support;

public static class AzureDemoTargetGuard
{
    public const string Environment = "AzureSqlDemo";

    public static void Validate(string environment, string connectionString, string expectedServer, string expectedDatabase)
    {
        if (!string.Equals(environment, Environment, StringComparison.Ordinal))
            throw new InvalidOperationException("Ambiente AzureSqlDemo explicito obrigatorio.");
        if (string.IsNullOrWhiteSpace(expectedServer) || string.IsNullOrWhiteSpace(expectedDatabase))
            throw new InvalidOperationException("Servidor e banco confirmados sao obrigatorios.");

        var cs = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(
    cs.DataSource,
    $"tcp:{expectedServer},1433",
    StringComparison.OrdinalIgnoreCase
) ||
            !string.Equals(cs.InitialCatalog, expectedDatabase, StringComparison.Ordinal) ||
            cs.DataSource.Contains("localdb", StringComparison.OrdinalIgnoreCase) ||
            cs.IntegratedSecurity ||
            !cs.Encrypt ||
            cs.TrustServerCertificate ||
            !expectedServer.EndsWith(".database.windows.net", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Destino recusado: confirme o servidor Azure SQL, banco e TLS.");
    }
}
