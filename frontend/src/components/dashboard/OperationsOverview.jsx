import { useState } from 'react';
import { buildCityOverview, MAINTENANCE_LOOKAHEAD_DAYS, maintenanceAttention } from '../../services/operations.js';
import { maintenanceNeeds } from '../equipment/equipmentModel.js';

function SourceStatus({ verification, ids }) {
  const sources = ids.map(id => verification.results.find(item => item.id === id));
  const failed = sources.some(source => source?.error || source?.stale) || !!verification.error;
  return <>{verification.loading && <p role="status">Atualizando a visão operacional…</p>}{failed && <p className="message error" role="alert">Visão parcial ou desatualizada. Consulte os horários e tente atualizar; ausência de dados não significa ausência de problemas.</p>}</>;
}
export function MaintenanceHighlights({ verification, canMaintain, onEquipment, onMaintenance }) {
  const [limit, setLimit] = useState(6);
  const equipments = verification.results.find(item => item.id === 'equipments');
  const scheduled = verification.results.find(item => item.id === 'maintenanceScheduled');
  const open = verification.results.find(item => item.id === 'maintenanceOpen');
  const now = verification.attemptedAt?.getTime() || 0;
  const items = (equipments?.items || []).map(item => ({ item, attention: maintenanceAttention(item, now) })).filter(({ attention }) => attention.immediate || attention.upcoming || attention.inProgress)
    .sort((a, b) => Number(b.attention.immediate) - Number(a.attention.immediate) || Number(b.attention.inProgress) - Number(a.attention.inProgress) || Date.parse(a.item.nextMaintenanceDate || '9999-01-01') - Date.parse(b.item.nextMaintenanceDate || '9999-01-01'));
  const names = new Map((equipments?.items || []).map(item => [item.id, item.serialNumber]));
  return <section className="dashboard-side-panel operations-maintenance" aria-labelledby="maintenance-highlights-title"><header className="side-panel-heading"><h2 id="maintenance-highlights-title">Manutenção em destaque</h2><p>Necessidades atuais e intervenções</p></header>
    <p className="operations-note">Próxima: até {MAINTENANCE_LOOKAHEAD_DAYS} dias a partir de agora, incluindo previsões vencidas. Agendamento não significa indisponibilidade.</p>
    <SourceStatus verification={verification} ids={canMaintain ? ['equipments', 'maintenanceScheduled', 'maintenanceOpen'] : ['equipments']} />
    {canMaintain ? <p>Ordens reais: {scheduled?.items?.length ?? '—'} agendada(s) · {open?.items?.length ?? '—'} em andamento.</p> : <p className="operations-note">Ordens são restritas a gestores e técnicos. Seu perfil consulta necessidades e situação pelo inventário.</p>}
    {equipments?.items && !items.length && <p>Nenhuma necessidade imediata, previsão próxima ou intervenção na resposta atual.</p>}
    <ul className="operations-resource-list">{items.slice(0, limit).map(({ item, attention }) => <li key={item.id}><strong>{item.serialNumber}</strong><span>{attention.immediate ? 'Necessidade imediata' : attention.inProgress ? 'Em manutenção' : attention.overdue ? 'Previsão vencida' : 'Previsão próxima'}</span><span>{maintenanceNeeds[item.maintenanceNeed] || 'Necessidade não informada'}{item.nextMaintenanceDate ? ` · ${new Date(item.nextMaintenanceDate).toLocaleString('pt-BR')}` : ''}</span><button className="secondary" onClick={() => onEquipment(item.id)}>Ver equipamento</button>{canMaintain && <button className="secondary" onClick={() => onMaintenance(item.id)}>Histórico e manutenção</button>}</li>)}</ul>
    {items.length > limit && <button className="secondary" onClick={() => setLimit(value => value + 6)}>Mostrar mais equipamentos</button>}
    {canMaintain && (scheduled?.items?.length > 0 || open?.items?.length > 0) && <details><summary>Consultar ordens agendadas e em andamento</summary><ul className="operations-resource-list">{[...(scheduled?.items || []), ...(open?.items || [])].map(order => <li key={order.id}><strong>{names.get(order.equipmentId) || order.equipmentId}</strong><span>{order.status === 'Scheduled' ? 'Agendada' : 'Em andamento'} · {order.kind === 'Preventive' ? 'Preventiva' : 'Corretiva'}</span><span>{order.scheduledDate ? new Date(order.scheduledDate).toLocaleString('pt-BR') : 'Sem previsão'}</span><button className="secondary" onClick={() => onMaintenance(order.equipmentId)}>Consultar ordem e histórico</button></li>)}</ul></details>}
  </section>;
}
export function CityOverview({ verification, onNeighborhood }) {
  const equipment = verification.results.find(item => item.id === 'equipments');
  const alerts = verification.results.find(item => item.id === 'alerts');
  const overview = buildCityOverview(equipment?.items || [], alerts?.items ?? null, verification.attemptedAt?.getTime() || 0);
  return <section className="dashboard-side-panel city-overview" aria-labelledby="city-overview-title"><header className="side-panel-heading"><h2 id="city-overview-title">Atenção por bairro</h2><p>Situação operacional atual da cidade inteira</p></header>
    <p className="operations-note">Contagens observadas, sem índice preditivo. Não acompanham os filtros locais do mapa. Órgãos e regiões administrativas não estão cadastrados; agrupamento por bairro.</p>
    <SourceStatus verification={verification} ids={['equipments', 'alerts']} />
    {equipment?.items && !overview.rows.length && <p>Nenhum equipamento para consolidar por bairro.</p>}
    {!alerts?.items && <p>Criticidade de alertas indisponível; não interprete como zero.</p>}
    <div className="operations-table-scroll" role="region" aria-label="Situação dos bairros" tabIndex={0}><table><thead><tr><th>Bairro</th><th>Recursos</th><th>Operacionais</th><th>Indisponíveis</th><th>Imediata</th><th>Vencida / até {MAINTENANCE_LOOKAHEAD_DAYS} dias</th><th>Previsão agendada</th><th>Em manutenção</th><th>Alertas altos / críticos</th><th>Sinais atuais</th></tr></thead><tbody>{overview.rows.map(row => <tr key={row.key}><th><button className="secondary" onClick={() => onNeighborhood(row.label)}>{row.label}</button></th><td>{row.total}</td><td>{row.operational}</td><td>{row.unavailable}</td><td>{row.immediate}</td><td>{row.upcoming}</td><td>{row.planned}</td><td>{row.inProgress}</td><td>{row.critical ?? '—'}</td><td>{row.critical || row.immediate || row.unavailable || row.upcoming ? 'Requer atenção' : 'Sem sinais nos dados recebidos'}</td></tr>)}</tbody></table></div>
    {overview.unlocatedAlerts > 0 && <p>{overview.unlocatedAlerts} alerta(s) alto(s)/crítico(s) sem equipamento localizado na resposta atual.</p>}
    <p className="operations-note">Categorias podem se sobrepor. Previsão agendada é metadado do equipamento; contagem de ordens reais aparece no painel de manutenção conforme permissão.</p>
  </section>;
}
