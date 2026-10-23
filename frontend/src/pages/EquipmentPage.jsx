import { useEffect, useMemo, useRef, useState } from 'react';
import EquipmentForm from '../components/equipment/EquipmentForm.jsx';
import EquipmentDetails from '../components/equipment/EquipmentDetails.jsx';
import { equipmentTypes, filterEquipments, maintenanceNeeds } from '../components/equipment/equipmentModel.js';
import { equipmentStates, getEquipmentState } from '../components/map/markerData.js';
import { canManageEquipments, canReadEquipments, canMaintain } from '../services/auth.js';
import { createEquipmentApi, inventoryError } from '../services/equipments.js';
import { isAbortError } from '../services/http.js';
import './EquipmentPage.css';

const emptyFilters = { search: '', type: '', status: '', neighborhood: '', maintenanceNeed: '' };
export default function EquipmentPage({ session, equipmentId = '', onLogout, onChanged, onSavingChange, onMaintenance }) {
  const api = useMemo(() => createEquipmentApi(session), [session]);
  const manager = canManageEquipments(session.user);
  const reader = canReadEquipments(session.user);
  const [revision, setRevision] = useState(0);
  const [list, setList] = useState({ loading: true, items: null, addresses: [], error: '', addressError: '', receivedAt: null });
  const [filters, setFilters] = useState(emptyFilters);
  const [page, setPage] = useState(1);
  const [mode, setMode] = useState(equipmentId ? 'details' : 'list');
  const [selected, setSelected] = useState(equipmentId || null);
  const [detailRevision, setDetailRevision] = useState(0);
  const [detail, setDetail] = useState({ loading: !!equipmentId, equipment: null, address: null, error: '', addressError: '' });
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState(null);
  const pendingWrite = useRef(null);
  const busy = useRef(false);
  const content = useRef(null);
  const createButton = useRef(null);

  useEffect(() => {
    const controller = new AbortController();
    queueMicrotask(async () => {
      if (controller.signal.aborted) return;
      const results = await Promise.allSettled([api.list(controller.signal), api.addresses(controller.signal)]);
      if (controller.signal.aborted) return;
      if (results.some(result => result.status === 'rejected' && result.reason?.status === 401)) { onLogout('Sua sessão não foi aceita. Entre novamente.'); return; }
      const [equipment, addresses] = results;
      setList(previous => ({
        loading: false,
        items: equipment.status === 'fulfilled' ? equipment.value : equipment.reason?.status === 403 ? null : previous.items,
        addresses: addresses.status === 'fulfilled' ? addresses.value : previous.addresses,
        error: equipment.status === 'rejected' ? inventoryError(equipment.reason) : '',
        addressError: addresses.status === 'rejected' ? inventoryError(addresses.reason) : '',
        receivedAt: equipment.status === 'fulfilled' ? new Date().toISOString() : previous.receivedAt
      }));
    });
    return () => controller.abort();
  }, [api, revision, onLogout]);

  useEffect(() => {
    if (!selected) return;
    const controller = new AbortController();
    queueMicrotask(async () => {
      if (controller.signal.aborted) return;
      try {
        const equipment = await api.detail(selected, controller.signal);
        let address = null;
        let addressError = '';
        try { address = await api.address(equipment.addressId, controller.signal); }
        catch (error) { if (error.status === 401 || isAbortError(error)) throw error; addressError = inventoryError(error); }
        if (!controller.signal.aborted) setDetail({ loading: false, equipment, address, addressError, error: '' });
      } catch (error) {
        if (controller.signal.aborted) return;
        if (error.status === 401) onLogout('Sua sessão não foi aceita. Entre novamente.');
        else setDetail({ loading: false, equipment: null, address: null, addressError: '', error: inventoryError(error) });
      }
    });
    return () => controller.abort();
  }, [api, selected, detailRevision, onLogout]);

  useEffect(() => () => { pendingWrite.current?.abort(); onSavingChange(false); }, [onSavingChange]);

  const filtered = useMemo(() => filterEquipments(list.items ?? [], filters), [list.items, filters]);
  const pages = Math.max(1, Math.ceil(filtered.length / 10));
  const visiblePage = Math.min(page, pages);
  const visible = filtered.slice((visiblePage - 1) * 10, visiblePage * 10);
  function refresh() { if (busy.current) return; setList(previous => ({ ...previous, loading: true, error: '' })); setRevision(value => value + 1); }
  function openDetails(id) {
    setSelected(id); setDetailRevision(value => value + 1); setMode('details');
    setDetail({ loading: true, equipment: null, address: null, addressError: '', error: '' });
    queueMicrotask(() => content.current?.focus());
  }
  function close() { if (busy.current) return; setMode('list'); setSelected(null); setNotice(null); queueMicrotask(() => createButton.current?.focus()); }
  async function write(operation, success) {
    if (busy.current) return null;
    busy.current = true;
    const controller = new AbortController(); pendingWrite.current = controller;
    setSaving(true); onSavingChange(true); setNotice(null);
    try {
      const result = await operation(controller.signal);
      if (controller.signal.aborted) return null;
      setNotice({ type: 'success', text: success });
      setMode('details'); setSelected(result.id); setDetailRevision(value => value + 1);
      setDetail({ loading: true, equipment: null, address: null, addressError: '', error: '' });
      setList(previous => ({ ...previous, loading: true })); setRevision(value => value + 1);
      onChanged();
      return result;
    } catch (error) {
      if (!controller.signal.aborted) {
        if (error.status === 401) onLogout('Sua sessão não foi aceita. Entre novamente.');
        else setNotice({ type: 'error', text: inventoryError(error) });
      }
      throw error;
    } finally {
      busy.current = false;
      if (!controller.signal.aborted) { setSaving(false); onSavingChange(false); }
    }
  }
  function filter(name, value) { setFilters(current => ({ ...current, [name]: value })); setPage(1); }

  return <div className="equipment-page" id="equipamentos">
    <section className="inventory-panel" aria-labelledby="inventory-title">
      <div className="inventory-section-heading"><div><p className="inventory-eyebrow">Gestão operacional</p><h2 id="inventory-title">Inventário de equipamentos</h2><p className="inventory-muted">Consulte os recursos urbanos e mantenha seus dados atualizados.</p></div><div className="inventory-actions">
        <button className="secondary" onClick={refresh} disabled={saving || list.loading}> {list.loading ? 'Carregando…' : 'Atualizar lista'}</button>
        {manager && <button ref={createButton} disabled={saving} onClick={() => { setMode('create'); setSelected(null); setNotice(null); queueMicrotask(() => content.current?.focus()); }}>Novo equipamento</button>}
      </div></div>
      {!manager && <p className="inventory-muted">Seu perfil permite consulta{reader ? ' e as mudanças de situação autorizadas pela API' : ''}. Cadastro e edição são exclusivos de administradores e gestores.</p>}
      {notice && <p className={`message ${notice.type}`} role={notice.type === 'error' ? 'alert' : 'status'}>{notice.text}</p>}
      <div className="inventory-filter-grid">
        <label htmlFor="inventory-search">Buscar<input id="inventory-search" type="search" value={filters.search} placeholder="Série, marca, modelo ou categoria" onChange={event => filter('search', event.target.value)} /></label>
        <label htmlFor="inventory-type">Tipo<select id="inventory-type" value={filters.type} onChange={event => filter('type', event.target.value)}><option value="">Todos os tipos</option>{Object.entries(equipmentTypes).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
        <label htmlFor="inventory-status">Situação<select id="inventory-status" value={filters.status} onChange={event => filter('status', event.target.value)}><option value="">Todas as situações</option>{Object.entries(equipmentStates).map(([value, state]) => <option key={value} value={value}>{state.label}</option>)}</select></label>
        <label htmlFor="inventory-neighborhood">Bairro<input id="inventory-neighborhood" value={filters.neighborhood} onChange={event => filter('neighborhood', event.target.value)} placeholder="Todos os bairros" /></label>
        <label htmlFor="inventory-maintenance">Necessidade de manutenção<select id="inventory-maintenance" value={filters.maintenanceNeed} onChange={event => filter('maintenanceNeed', event.target.value)}><option value="">Todas</option>{Object.entries(maintenanceNeeds).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
      </div>
      <div className="inventory-list-summary"><p aria-live="polite">{list.items ? `${filtered.length} de ${list.items.length} equipamento(s)` : 'Aguardando dados do inventário'}</p><button className="secondary" onClick={() => { setFilters(emptyFilters); setPage(1); }}>Limpar filtros</button></div>
      {list.loading && <p role="status">Consultando equipamentos e localizações…</p>}
      {list.error && <p className="message error" role="alert">{list.items ? 'Os registros anteriores podem estar desatualizados. ' : ''}{list.error}</p>}
      {list.addressError && <p className="message error" role="alert">Localizações existentes indisponíveis. {list.addressError}</p>}
      {list.items && !list.loading && !list.error && filtered.length === 0 && <p className="inventory-empty">{list.items.length === 0 ? 'Nenhum equipamento cadastrado. Use Novo equipamento para iniciar o inventário.' : 'Nenhum equipamento corresponde à busca e aos filtros.'}</p>}
      {visible.length > 0 && <div className="inventory-table-scroll" role="region" aria-label="Lista de equipamentos" tabIndex={0} aria-busy={list.loading}><table className="inventory-table"><thead><tr><th>Série / identificação</th><th>Tipo / categoria</th><th>Situação</th><th>Localização</th><th>Manutenção</th><th><span className="sr-only">Ações</span></th></tr></thead><tbody>{visible.map(item => <tr key={item.id}>
        <td><strong>{item.serialNumber}</strong><span>{item.brand} · {item.model}</span></td>
        <td>{equipmentTypes[item.type] ?? item.type}<span>{item.category || 'Sem categoria informada'}</span></td>
        <td><span className={`inventory-badge state-${getEquipmentState(item.status).tone}`}>{getEquipmentState(item.status).label}</span></td>
        <td>{item.neighborhood || 'Não informado'}</td><td>{maintenanceNeeds[item.maintenanceNeed] ?? item.maintenanceNeed}</td>
        <td><button className="secondary" disabled={saving || list.loading || Boolean(list.error)} onClick={() => openDetails(item.id)} aria-label={`Ver detalhes de ${item.serialNumber}`}>Detalhes</button></td>
      </tr>)}</tbody></table></div>}
      {filtered.length > 10 && <nav className="inventory-pagination" aria-label="Páginas de equipamentos"><button className="secondary" disabled={visiblePage === 1} onClick={() => setPage(visiblePage - 1)}>Anterior</button><span>Página {visiblePage} de {pages}</span><button className="secondary" disabled={visiblePage === pages} onClick={() => setPage(visiblePage + 1)}>Próxima</button></nav>}
      {list.receivedAt && <p className="inventory-muted">Última consulta válida: <time dateTime={list.receivedAt}>{new Date(list.receivedAt).toLocaleString('pt-BR')}</time>. Busca e filtros são aplicados sobre a lista carregada.</p>}
    </section>
    {mode !== 'list' && <div ref={content} tabIndex={-1} className="inventory-workspace">
      {mode !== 'create' && <button className="secondary inventory-back" disabled={saving} onClick={close}>Voltar à lista</button>}
      {mode === 'create' ? <EquipmentForm key="new" addresses={list.addresses} addressError={list.addressError} saving={saving} onCancel={close} onSave={(payload, id) => write(signal => api.save(payload, id, signal), 'Equipamento cadastrado com sucesso.')} /> :
        detail.loading ? <p role="status">Carregando os dados completos do equipamento…</p> : detail.error ? <p className="message error" role="alert">{detail.error} <button className="secondary" onClick={() => openDetails(selected)}>Tentar novamente</button></p> : detail.equipment ?
          mode === 'edit' ? <EquipmentForm key={detail.equipment.id} equipment={detail.equipment} address={detail.address} addresses={list.addresses} addressError={list.addressError || detail.addressError} saving={saving} onCancel={() => { setMode('details'); setNotice(null); }} onSave={(payload, id) => write(signal => api.save(payload, id, signal), 'Alterações salvas com sucesso.')} /> :
            <EquipmentDetails key={`${detail.equipment.id}:${detail.equipment.status}`} equipment={detail.equipment} address={detail.address} addressError={detail.addressError} onMaintenance={canMaintain(session.user) ? () => onMaintenance(detail.equipment.id) : undefined} canManage={manager} canChangeStatus={reader} saving={saving} onEdit={() => { setMode('edit'); setNotice(null); }} onRetryAddress={() => openDetails(selected)} onStatus={async (next, reason) => { try { return await write(signal => api.status(detail.equipment, next, reason, signal), 'Situação operacional atualizada.'); } catch { return null; } }} /> : null}
    </div>}
  </div>;
}
