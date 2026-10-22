using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Infrastructure.Demo;

public static class DemoTargetGuard
{
    public static void Validate(string? environment, string connectionString, string? expectedDatabase = null)
    {
        if (!string.Equals(environment, "Demo", StringComparison.Ordinal))
            throw new InvalidOperationException("Carga permitida somente no ambiente explicito Demo.");
        var cs = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(cs.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase) ||
            !Regex.IsMatch(cs.InitialCatalog, @"^SENTRA_DEMO_[A-Za-z0-9_]{1,50}$") ||
            !cs.IntegratedSecurity || cs.UserID.Length != 0 || cs.Password.Length != 0 ||
            cs.AttachDBFilename.Length != 0 || cs.FailoverPartner.Length != 0)
            throw new InvalidOperationException("Destino recusado: use exclusivamente LocalDB e banco SENTRA_DEMO_*, sem credenciais.");
        if (expectedDatabase is not null && !string.Equals(cs.InitialCatalog, expectedDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException("O banco da conexao externa nao corresponde ao destino confirmado.");
    }
}
