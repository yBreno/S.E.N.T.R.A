const statuses = {
  Operational: { label: 'Operacionais', color: '#26c66b' },
  Malfunctioning: { label: 'Em falha', color: '#f3ad31' },
  UnderMaintenance: { label: 'Em manutenção', color: '#529ff0' },
  Deactivated: { label: 'Desativados', color: '#89939e' },
  Stolen: { label: 'Furtados / roubados', color: '#ed646b' },
  NotInstalled: { label: 'Não instalados', color: '#bdc8d3' }
};

export function getDashboardSummary(data) {
  if (!data || !Number.isInteger(data.totalEquipments) || data.totalEquipments < 0 ||
    !data.byStatus || typeof data.byStatus !== 'object' || Array.isArray(data.byStatus)) return null;

  const entries = Object.entries(data.byStatus);
  if (!entries.every(([, count]) => Number.isInteger(count) && count >= 0)) return null;

  const count = name => Object.hasOwn(data.byStatus, name) ? data.byStatus[name] : null;
  const unavailableStates = ['Malfunctioning', 'UnderMaintenance', 'Stolen'];
  const unavailable = unavailableStates.every(name => count(name) !== null)
    ? unavailableStates.reduce((total, name) => total + count(name), 0)
    : null;
  const distribution = entries.map(([name, value]) => ({
    name,
    ...(Object.hasOwn(statuses, name) ? statuses[name] : { label: `Outro: ${name}`, color: '#a5a9b0' }),
    count: value,
    percentage: data.totalEquipments ? value / data.totalEquipments * 100 : 0
  }));

  return {
    operational: count('Operational'),
    unavailable,
    distribution,
    consistent: entries.reduce((total, [, value]) => total + value, 0) === data.totalEquipments
  };
}

export function userInitials(name) {
  return typeof name === 'string' && name.trim()
    ? name.trim().split(/\s+/).slice(0, 2).map(part => part[0]).join('').toLocaleUpperCase('pt-BR')
    : '?';
}

export function userRoleLabel(role) {
  const labels = { Admin: 'Administrador', OperationManager: 'Gestor de operações', CentralOperator: 'Operador da central' };

  return Object.hasOwn(labels, role) ? labels[role] : role;
}
