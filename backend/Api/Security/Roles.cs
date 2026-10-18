namespace Api.Security;

/// <summary>Nomes dos papeis (iguais aos valores do enum UserRole) usados em [Authorize(Roles = ...)].</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string OperationManager = "OperationManager";
    public const string CentralOperator = "CentralOperator";
    public const string MaintenanceTechnician = "MaintenanceTechnician";
    public const string Agent = "Agent";

    /// <summary>Gestores: Admin + OperationManager.</summary>
    public const string Managers = Admin + "," + OperationManager;
    /// <summary>Quem monitora (dashboard, mapa, alertas, localizacao de agentes).</summary>
    public const string Monitoring = Admin + "," + OperationManager + "," + CentralOperator;
    /// <summary>Equipe operacional interna (exclui Agent).</summary>
    public const string Staff = Admin + "," + OperationManager + "," + CentralOperator + "," + MaintenanceTechnician;
    public const string Maintenance = Admin + "," + OperationManager + "," + MaintenanceTechnician;
}
