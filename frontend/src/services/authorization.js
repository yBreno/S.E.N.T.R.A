// Cargos de login. As categorias profissionais dos agentes têm contrato próprio.
export const userRoles = Object.freeze({
  Admin: 'Administrador',
  OperationManager: 'Gestor de operações',
  CentralOperator: 'Operador da central',
  MaintenanceTechnician: 'Técnico de manutenção',
  Agent: 'Agente'
});
export const isUserRole = role => typeof role === 'string' && Object.hasOwn(userRoles, role);
export const userRoleLabel = role => isUserRole(role) ? userRoles[role] : role;

const managers = new Set(['Admin', 'OperationManager']);
const monitoring = new Set([...managers, 'CentralOperator']);
export const canMonitor = user => monitoring.has(user?.role);
export const canReadEquipments = user => canMonitor(user) || user?.role === 'MaintenanceTechnician';
export const canManageEquipments = user => managers.has(user?.role);
export const canReadAgents = canMonitor;
export const canManageAgents = canManageEquipments;
export const canMaintain = user => canManageEquipments(user) || user?.role === 'MaintenanceTechnician';
