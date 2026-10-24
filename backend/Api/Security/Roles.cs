using Domain.Enums;
namespace Api.Security;

/// <summary>Nomes dos papeis (iguais aos valores do enum UserRole) usados em [Authorize(Roles = ...)].</summary>
public static class Roles
{
    public const string Admin = nameof(UserRole.Admin);
    public const string OperationManager = nameof(UserRole.OperationManager);
    public const string CentralOperator = nameof(UserRole.CentralOperator);
    public const string MaintenanceTechnician = nameof(UserRole.MaintenanceTechnician);
    public const string Agent = nameof(UserRole.Agent);

    /// <summary>Gestores: Admin + OperationManager.</summary>
    public const string Managers = Admin + "," + OperationManager;
    /// <summary>Quem monitora (dashboard, mapa, alertas, localizacao de agentes).</summary>
    public const string Monitoring = Admin + "," + OperationManager + "," + CentralOperator;
    /// <summary>Equipe operacional interna (exclui Agent).</summary>
    public const string Staff = Admin + "," + OperationManager + "," + CentralOperator + "," + MaintenanceTechnician;
    public const string Maintenance = Admin + "," + OperationManager + "," + MaintenanceTechnician;
}
