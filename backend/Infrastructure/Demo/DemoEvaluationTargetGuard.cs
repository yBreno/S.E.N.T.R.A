using Microsoft.Data.SqlClient;
namespace Infrastructure.Demo;

public static class DemoEvaluationTargetGuard
{
    public static void Validate(string? environment, string connection, string? expectedDatabase = null)
    {
        DemoTargetGuard.Validate(environment, connection, expectedDatabase);
        var database = new SqlConnectionStringBuilder(connection).InitialCatalog;
        if (!database.StartsWith(DemoEvaluationPlan.DatabasePrefix, StringComparison.Ordinal) || database.Length <= DemoEvaluationPlan.DatabasePrefix.Length)
            throw new InvalidOperationException("Avaliação exige banco LocalDB próprio SENTRA_DEMO_Avaliacao_<nome>.");
    }
}
