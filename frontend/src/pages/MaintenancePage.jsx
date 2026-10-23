import { useEffect, useMemo, useRef, useState } from 'react';
import MaintenanceForm from '../components/inventory/MaintenanceForm.jsx';
import InventoryDetails from '../components/inventory/InventoryDetails.jsx';
import { maintenanceNeeds } from '../components/equipment/equipmentModel.js';
import { createEquipmentApi } from '../services/equipments.js';
import { createMaintenanceApi, filterMaintenances, maintenanceError, maintenanceKinds, maintenanceStatuses, urgencies } from '../services/maintenances.js';
import './EquipmentPage.css';

const formatDate = value => value ? new Date(value).toLocaleString('pt-BR') : 'Não informada';
const emptyFilters = { search: '', equipmentId: '', status: '', kind: '', need: '' };
export default function MaintenancePage({ session, equipmentId = '', onLogout, onChanged, onSavingChange, onEquipment }) {
  const api = useMemo(() => createMaintenanceApi(session), [session]);
  const equipmentApi = useMemo(() => createEquipmentApi(session), [session]);
  const [data, setData] = useState({ items: [], equipments: [], loaded: false, loading: true, error: '', equipmentError: '' });
  const [revision, setRevision] = useState(0);
  const [filters, setFilters] = useState({ ...emptyFilters, equipmentId });
  const [page, setPage] = useState(1);
  const [mode, setMode] = useState('list');
  const [detail, setDetail] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState('');
  const [selected, setSelected] = useState(null);
  const [detailRevision, setDetailRevision] = useState(0);
  const [notice, setNotice] = useState(null);
  const [saving, setSaving] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const busy = useRef(false);
  const pending = useRef(null);
  const workspace = useRef(null);
  useEffect(() => {
    const controller = new AbortController();
    queueMicrotask(async () => {
      const [orders, equipments] = await Promise.allSettled([api.list('', controller.signal), equipmentApi.list(controller.signal)]);
      if (controller.signal.aborted) return;
      if ([orders, equipments].some(result => result.status === 'rejected' && result.reason?.status === 401)) { onLogout('Sua sessão expirou. Entre novamente.'); return; }
      setData(previous => ({
        loading: false, loaded: orders.status === 'fulfilled',
        items: orders.status === 'fulfilled' ? orders.value : orders.reason?.status === 403 ? [] : previous.items,
        equipments: equipments.status === 'fulfilled' ? equipments.value : [],
        error: orders.status === 'rejected' ? maintenanceError(orders.reason) : '',
        equipmentError: equipments.status === 'rejected' ? 'Não foi possível consultar os equipamentos. Os identificadores das ordens continuam disponíveis.' : ''
      }));
    });
    return () => controller.abort();
  }, [api, equipmentApi, revision, onLogout]);
  useEffect(() => {
    if (!selected) return;
    const controller = new AbortController();
    queueMicrotask(async () => {
      try {
        const record = await api.detail(selected, controller.signal);
        if (!controller.signal.aborted) { setDetail(record); setDetailLoading(false); }
      } catch (error) {
        if (controller.signal.aborted) return;
        if (error.status === 401) onLogout('Sua sessão expirou. Entre novamente.');
        else { setDetailError(maintenanceError(error)); setDetailLoading(false); }
      }
    });
    return () => controller.abort();
  }, [api, selected, detailRevision, onLogout]);
  useEffect(() => () => { pending.current?.abort(); onSavingChange(false); }, [onSavingChange]);
  useEffect(() => { if (mode !== 'list') workspace.current?.focus(); }, [mode]);
  const equipments = new Map(data.equipments.map(item => [item.id, item]));
  const filtered = filterMaintenances(data.items, data.equipments, filters);
  const pages = Math.max(1, Math.ceil(filtered.length / 10));
  const currentPage = Math.min(page, pages);
  const visible = filtered.slice((currentPage - 1) * 10, currentPage * 10);
  function filter(name, value) { setFilters(previous => ({ ...previous, [name]: value })); setPage(1); }
  function refresh() { if (busy.current) return; setData(previous => ({ ...previous, loading: true, error: '' })); setRevision(value => value + 1); if (mode === 'details' && selected) open(selected); }
  function open(id) {
    if (busy.current) return;
    setSelected(id); setDetail(null); setDetailLoading(true); setDetailError(''); setConfirmed(false);
    setDetailRevision(value => value + 1); setMode('details');
    queueMicrotask(() => workspace.current?.focus());
  }
  function newOrder(nextMode) { setMode(nextMode); setSelected(null); setNotice(null); queueMicrotask(() => workspace.current?.focus()); }
  async function write(operation, message) {
    if (busy.current) return;
    busy.current = true;
    const controller = new AbortController(); pending.current = controller;
    setSaving(true); onSavingChange(true); setNotice(null);
    try {
      const result = await operation(controller.signal);
      if (controller.signal.aborted) return;
      setNotice({ type: 'success', text: message });
      setSelected(result.id); setDetail(null); setDetailLoading(true); setDetailError(''); setConfirmed(false);
      setDetailRevision(value => value + 1); setMode('details');
      setData(previous => ({ ...previous, loading: true })); setRevision(value => value + 1); onChanged();
    } catch (error) {
      if (!controller.signal.aborted) {
        if (error.status === 401) onLogout('Sua sessão expirou. Entre novamente.');
        else setNotice({ type: 'error', text: maintenanceError(error) });
      }
      throw error;
    } finally { busy.current = false; if (!controller.signal.aborted) { setSaving(false); onSavingChange(false); } }
  }
  const equipment = detail && equipments.get(detail.equipmentId);
  return <div className="equipment-page" id="manutencoes"><section className="inventory-panel" aria-labelledby="maintenance-title">
    <div className="inventory-section-heading"><div><p className="inventory-eyebrow">Gestão operacional</p><h2 id="maintenance-title">Manutenções</h2><p className="inventory-muted">Agendamento, intervenção e histórico dos equipamentos.</p></div><div className="inventory-actions">
      <button className="secondary" disabled={saving || data.loading} onClick={refresh}>{data.loading ? 'Carregando…' : 'Atualizar lista'}</button>
      <button disabled={saving || data.loading || !!data.equipmentError} onClick={() => newOrder('schedule')}>Agendar preventiva</button><button className="secondary" disabled={saving || data.loading || !!data.equipmentError} onClick={() => newOrder('corrective')}>Abrir corretiva</button>
    </div></div>
    {notice && <p className={`message ${notice.type}`} role={notice.type === 'error' ? 'alert' : 'status'}>{notice.text}</p>}
    <div className="inventory-filter-grid">
      <label htmlFor="maintenance-search">Buscar<input id="maintenance-search" type="search" value={filters.search} onChange={event => filter('search', event.target.value)} placeholder="Série, marca, descrição ou identificador" /></label>
      <label htmlFor="maintenance-equipmentFilter">Equipamento<select id="maintenance-equipmentFilter" value={filters.equipmentId} onChange={event => filter('equipmentId', event.target.value)}><option value="">Todos os equipamentos</option>{data.equipments.map(item => <option key={item.id} value={item.id}>{item.serialNumber}</option>)}{filters.equipmentId && !equipments.has(filters.equipmentId) && <option value={filters.equipmentId}>{filters.equipmentId}</option>}</select></label>
      <label htmlFor="maintenance-statusFilter">Estado da ordem<select id="maintenance-statusFilter" value={filters.status} onChange={event => filter('status', event.target.value)}><option value="">Todos</option>{Object.entries(maintenanceStatuses).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
      <label htmlFor="maintenance-kindFilter">Natureza<select id="maintenance-kindFilter" value={filters.kind} onChange={event => filter('kind', event.target.value)}><option value="">Todas</option>{Object.entries(maintenanceKinds).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
      <label htmlFor="maintenance-needFilter">Necessidade atual do equipamento<select id="maintenance-needFilter" disabled={!!data.equipmentError} value={filters.need} onChange={event => filter('need', event.target.value)}><option value="">Todas</option>{Object.entries(maintenanceNeeds).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
    </div>
    <div className="inventory-list-summary"><p aria-live="polite">{filtered.length} ordem(ns). Filtros aplicados sobre os dados carregados.</p><button className="secondary" onClick={() => { setFilters(emptyFilters); setPage(1); }}>Limpar filtros</button></div>
    {data.loading && <p role="status">Consultando ordens e equipamentos…</p>}
    {data.error && <p className="message error" role="alert">{data.items.length > 0 && 'Os registros anteriores podem estar desatualizados. '}{data.error}</p>}
    {data.equipmentError && <p className="message error" role="alert">{data.equipmentError}</p>}
    {data.loaded && !data.loading && !filtered.length && <p className="inventory-empty">{data.items.length ? 'Nenhuma ordem corresponde aos filtros.' : 'Nenhuma ordem registrada. A previsão informada no inventário não cria uma ordem automaticamente.'}</p>}
    {visible.length > 0 && <div className="inventory-table-scroll" role="region" aria-label="Histórico de manutenções" tabIndex={0}><table className="inventory-table"><thead><tr><th>Equipamento / serviço</th><th>Natureza / urgência</th><th>Estado</th><th>Previsão / início</th><th>Conclusão</th><th><span className="sr-only">Ações</span></th></tr></thead><tbody>{visible.map(item => <tr key={item.id}><td><strong>{equipments.get(item.equipmentId)?.serialNumber || item.equipmentId}</strong><span>{item.description}</span></td><td>{maintenanceKinds[item.kind]}<span>{urgencies[item.urgency]}</span></td><td>{maintenanceStatuses[item.status]}</td><td>{formatDate(item.scheduledDate)}<span>Início: {formatDate(item.initDate)}</span></td><td>{formatDate(item.endDate)}</td><td><button className="secondary" disabled={saving || data.loading || !!data.error} onClick={() => open(item.id)} aria-label={`Ver ordem de ${equipments.get(item.equipmentId)?.serialNumber || item.equipmentId}`}>Detalhes</button></td></tr>)}</tbody></table></div>}
    {pages > 1 && <nav className="inventory-pagination" aria-label="Páginas de manutenções"><button className="secondary" disabled={currentPage === 1} onClick={() => setPage(currentPage - 1)}>Anterior</button><span>Página {currentPage} de {pages}</span><button className="secondary" disabled={currentPage === pages} onClick={() => setPage(currentPage + 1)}>Próxima</button></nav>}
  </section>
  {mode !== 'list' && <div className="inventory-workspace" ref={workspace} tabIndex={-1}>
    <button className="secondary inventory-back" disabled={saving} onClick={() => { setMode('list'); setSelected(null); setNotice(null); }}>Voltar à lista</button>
    {mode === 'schedule' || mode === 'corrective' ? <MaintenanceForm key={mode} mode={mode} equipments={data.equipments} equipmentId={filters.equipmentId} saving={saving} onCancel={() => setMode('list')} onSave={payload => write(signal => api.create(payload, mode === 'schedule', signal), mode === 'schedule' ? 'Preventiva agendada. Equipamento permanece operacional.' : 'Corretiva aberta. Equipamento em manutenção.')} /> : detailLoading ? <p role="status">Consultando ordem…</p> : detailError ? <p className="message error" role="alert">{detailError} <button className="secondary" onClick={() => open(selected)}>Tentar novamente</button></p> : detail && (mode === 'close' ? <MaintenanceForm key={detail.id} mode="close" equipments={[]} saving={saving} onCancel={() => setMode('details')} onSave={payload => write(signal => api.close(detail.id, payload, signal), 'Manutenção concluída. Equipamento operacional e histórico preservado.')} /> : <section className="inventory-panel" aria-labelledby="maintenance-detail-title"><h2 id="maintenance-detail-title">Ordem de manutenção</h2>
      <InventoryDetails entries={[
        ['Identificador', detail.id], ['Equipamento', equipment?.serialNumber || detail.equipmentId], ['Natureza', maintenanceKinds[detail.kind]], ['Estado', maintenanceStatuses[detail.status]], ['Urgência', urgencies[detail.urgency]],
        ['Descrição', detail.description], ['Data prevista', formatDate(detail.scheduledDate)], ['Início efetivo', formatDate(detail.initDate)], ['Conclusão', formatDate(detail.endDate)],
        ['Custo', detail.cost == null ? null : Number(detail.cost).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })], ['Periodicidade em dias', detail.periodicityDays], ['Agente vinculado (identificador)', detail.agentId], ['Recomendação de origem', detail.recommendationId],
        ['Necessidade atual do equipamento', equipment && maintenanceNeeds[equipment.maintenanceNeed]], ['Próxima previsão atual', formatDate(equipment?.nextMaintenanceDate)]
      ]} />
      <div className="inventory-actions"><button className="secondary" disabled={saving} onClick={() => { filter('equipmentId', detail.equipmentId); setMode('list'); setSelected(null); }}>Histórico deste equipamento</button><button className="secondary" disabled={saving} onClick={() => onEquipment(detail.equipmentId)}>Ver equipamento</button></div>
      {detail.status === 'Scheduled' && <form onSubmit={async event => { event.preventDefault(); if (!confirmed || saving) return; try { await write(signal => api.start(detail.id, signal), 'Intervenção iniciada. Equipamento em manutenção.'); } catch { /* Mensagem exibida pelo fluxo de escrita. */ } }}><p className="inventory-muted">Iniciar registra a intervenção agora, mesmo antes da previsão, e muda o equipamento para Em manutenção. Se ocorreu uma falha real após o agendamento, a intervenção passa a ser corretiva.</p><label className="inventory-checkbox"><span>Confirmo o início efetivo da intervenção.</span><input type="checkbox" checked={confirmed} disabled={saving} onChange={event => setConfirmed(event.target.checked)} /></label><button disabled={saving || !confirmed}>Iniciar intervenção</button></form>}
      {detail.status === 'Open' && <button disabled={saving} onClick={() => setMode('close')}>Concluir manutenção</button>}
    </section>)}
  </div>}</div>;
}
