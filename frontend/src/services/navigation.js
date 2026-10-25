import { canAdministerUsers, canMaintain, canMonitor, canReadEquipments } from './authorization.js';

export function dashboardTarget(hash, user) {
  const target = hash?.startsWith('#') ? hash.slice(1) : '';
  if (target === 'usuarios') return canAdministerUsers(user) ? target : null;
  if (target === 'manutencoes') return canMaintain(user) ? target : null;
  if (target === 'equipamentos') return canReadEquipments(user) ? target : null;
  if (
    ['dashboard', 'agentes', 'mapa-operacional', 'mapa-de-calor', 'cidade-preditiva'].includes(
      target,
    )
  )
    return canMonitor(user) ? target : null;
  return null; // Field-error anchors and the skip link are not module navigation.
}
