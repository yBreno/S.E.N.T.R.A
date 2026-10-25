import { useEffect, useMemo, useRef, useState } from 'react';
import AgentForm from '../components/agents/AgentForm.jsx';
import AgentDetails from '../components/agents/AgentDetails.jsx';
import { agentRoles, availabilityOptions, filterAgents } from '../components/agents/agentModel.js';
import { agentRoleLabel, getAgentState } from '../components/map/markerData.js';
import { canManageAgents } from '../services/auth.js';
import { agentError, createAgentApi } from '../services/agents.js';
import './EquipmentPage.css';
import './AgentPage.css';

const emptyFilters = { search: '', role: '', availability: '', neighborhood: '' };
const emptyDetail = {
  loading: true,
  agent: null,
  vehicle: null,
  vehicles: [],
  error: '',
  vehicleError: '',
  vehiclesError: '',
};
export default function AgentPage({ session, agentId = '', onLogout, onChanged, onSavingChange }) {
  const api = useMemo(() => createAgentApi(session), [session]);
  const manager = canManageAgents(session.user);
  const [revision, setRevision] = useState(0);
  const [list, setList] = useState({ loading: true, items: null, error: '', receivedAt: null });
  const [filters, setFilters] = useState(emptyFilters);
  const [page, setPage] = useState(1);
  const [mode, setMode] = useState(agentId ? 'details' : 'list');
  const [selected, setSelected] = useState(agentId || null);
  const [detailRevision, setDetailRevision] = useState(0);
  const [detail, setDetail] = useState(emptyDetail);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState(null);
  const pendingWrite = useRef(null);
  const busy = useRef(false);
  const content = useRef(null);
  const newButton = useRef(null);

  useEffect(() => {
    const controller = new AbortController();
    queueMicrotask(async () => {
      if (controller.signal.aborted) return;
      try {
        const items = await api.list(controller.signal);
        if (!controller.signal.aborted)
          setList({ loading: false, items, error: '', receivedAt: new Date().toISOString() });
      } catch (error) {
        if (controller.signal.aborted) return;
        if (error.status === 401) onLogout('Sua sessão não foi aceita. Entre novamente.');
        else
          setList((previous) => ({
            ...previous,
            loading: false,
            items: error.status === 403 ? null : previous.items,
            error: agentError(error),
          }));
      }
    });
    return () => controller.abort();
  }, [api, revision, onLogout]);

  useEffect(() => {
    if (!selected) return;
    const controller = new AbortController();
    queueMicrotask(async () => {
      if (controller.signal.aborted) return;
      const results = await Promise.allSettled([
        api.detail(selected, controller.signal),
        api.vehicle(selected, controller.signal),
        manager ? api.vehicles(controller.signal) : Promise.resolve([]),
      ]);
      if (controller.signal.aborted) return;
      if (results.some((result) => result.status === 'rejected' && result.reason?.status === 401)) {
        onLogout('Sua sessão não foi aceita. Entre novamente.');
        return;
      }
      const [agent, vehicle, vehicles] = results;
      if (agent.status === 'rejected') {
        setDetail({ ...emptyDetail, loading: false, error: agentError(agent.reason) });
        return;
      }
      setDetail({
        loading: false,
        agent: agent.value,
        error: '',
        vehicle: vehicle.status === 'fulfilled' ? vehicle.value : null,
        vehicleError: vehicle.status === 'rejected' ? agentError(vehicle.reason) : '',
        vehicles: vehicles.status === 'fulfilled' ? vehicles.value : [],
        vehiclesError: vehicles.status === 'rejected' ? agentError(vehicles.reason) : '',
      });
    });
    return () => controller.abort();
  }, [api, selected, detailRevision, manager, onLogout]);

  useEffect(
    () => () => {
      pendingWrite.current?.abort();
      onSavingChange(false);
    },
    [onSavingChange],
  );
  const filtered = useMemo(() => filterAgents(list.items ?? [], filters), [list.items, filters]);
  const pages = Math.max(1, Math.ceil(filtered.length / 10));
  const visiblePage = Math.min(page, pages);
  const visible = filtered.slice((visiblePage - 1) * 10, visiblePage * 10);
  function refresh() {
    if (busy.current) return;
    setList((previous) => ({ ...previous, loading: true, error: '' }));
    setRevision((value) => value + 1);
  }
  function openDetails(id) {
    if (busy.current) return;
    setSelected(id);
    setMode('details');
    setDetail(emptyDetail);
    setDetailRevision((value) => value + 1);
    queueMicrotask(() => content.current?.focus());
  }
  function close() {
    if (busy.current) return;
    setMode('list');
    setSelected(null);
    setNotice(null);
    queueMicrotask(() => newButton.current?.focus());
  }
  function filter(key, value) {
    setFilters((previous) => ({ ...previous, [key]: value }));
    setPage(1);
  }
  async function write(operation, message, id) {
    if (busy.current) return null;
    busy.current = true;
    const controller = new AbortController();
    pendingWrite.current = controller;
    setSaving(true);
    onSavingChange(true);
    setNotice(null);
    try {
      const result = await operation(controller.signal);
      if (controller.signal.aborted) return null;
      setMode('details');
      setSelected(id ?? result.id);
      setDetailRevision((value) => value + 1);
      setDetail(emptyDetail);
      setList((previous) => ({ ...previous, loading: true }));
      setRevision((value) => value + 1);
      setNotice({ type: 'success', text: message });
      onChanged();
      return result;
    } catch (error) {
      if (!controller.signal.aborted) {
        if (error.status === 401) onLogout('Sua sessão não foi aceita. Entre novamente.');
        else setNotice({ type: 'error', text: agentError(error) });
      }
      throw error;
    } finally {
      busy.current = false;
      if (!controller.signal.aborted) {
        setSaving(false);
        onSavingChange(false);
      }
    }
  }

  return (
    <div
      className='equipment-page agent-page'
      id='agentes'
    >
      <section
        className='inventory-panel'
        aria-labelledby='agents-title'
      >
        <div className='inventory-section-heading'>
          <div>
            <p className='inventory-eyebrow'>Gestão operacional</p>
            <h2 id='agents-title'>Agentes de mobilidade e segurança</h2>
            <p className='inventory-muted'>
              Profissionais, área atual de atuação e recursos móveis associados.
            </p>
          </div>
          <div className='inventory-actions'>
            <button
              className='secondary'
              disabled={saving || list.loading}
              onClick={refresh}
            >
              {list.loading ? 'Carregando…' : 'Atualizar lista'}
            </button>
            {manager && (
              <button
                ref={newButton}
                disabled={saving}
                onClick={() => {
                  setMode('create');
                  setSelected(null);
                  setNotice(null);
                  queueMicrotask(() => content.current?.focus());
                }}
              >
                Novo agente
              </button>
            )}
          </div>
        </div>
        {!manager && (
          <p className='inventory-muted'>
            Seu perfil permite consultar e atualizar posição/disponibilidade. Identificação e
            vínculos de veículos são gerenciados por administradores e gestores.
          </p>
        )}
        {notice && (
          <p
            className={`message ${notice.type}`}
            role={notice.type === 'error' ? 'alert' : 'status'}
          >
            {notice.text}
          </p>
        )}
        <div className='inventory-filter-grid'>
          <label htmlFor='agents-search'>
            Buscar
            <input
              id='agents-search'
              type='search'
              placeholder='Nome, matrícula, categoria ou bairro'
              value={filters.search}
              onChange={(event) => filter('search', event.target.value)}
            />
          </label>
          <label htmlFor='agents-role'>
            Categoria de atuação
            <select
              id='agents-role'
              value={filters.role}
              onChange={(event) => filter('role', event.target.value)}
            >
              <option value=''>Todas as categorias</option>
              {Object.entries(agentRoles).map(([value, label]) => (
                <option
                  key={value}
                  value={value}
                >
                  {label}
                </option>
              ))}
            </select>
          </label>
          <label htmlFor='agents-availability'>
            Disponibilidade
            <select
              id='agents-availability'
              value={filters.availability}
              onChange={(event) => filter('availability', event.target.value)}
            >
              <option value=''>Todas</option>
              {Object.entries(availabilityOptions).map(([value, label]) => (
                <option
                  key={value}
                  value={value}
                >
                  {label}
                </option>
              ))}
            </select>
          </label>
          <label htmlFor='agents-neighborhood'>
            Bairro / área atual
            <input
              id='agents-neighborhood'
              placeholder='Todas as áreas'
              value={filters.neighborhood}
              onChange={(event) => filter('neighborhood', event.target.value)}
            />
          </label>
        </div>
        <div className='inventory-list-summary'>
          <p aria-live='polite'>
            {list.items
              ? `${filtered.length} de ${list.items.length} agente(s)`
              : 'Aguardando dados dos agentes'}
          </p>
          <button
            className='secondary'
            onClick={() => {
              setFilters(emptyFilters);
              setPage(1);
            }}
          >
            Limpar filtros
          </button>
        </div>
        {list.loading && <p role='status'>Consultando agentes…</p>}
        {list.error && (
          <p
            className='message error'
            role='alert'
          >
            {list.items ? 'Os registros anteriores podem estar desatualizados. ' : ''}
            {list.error}
          </p>
        )}
        {list.items && !list.loading && !list.error && filtered.length === 0 && (
          <p className='inventory-empty'>
            {list.items.length === 0
              ? 'Nenhum agente cadastrado.'
              : 'Nenhum agente corresponde à busca e aos filtros.'}
          </p>
        )}
        {visible.length > 0 && (
          <div
            className='inventory-table-scroll'
            role='region'
            aria-label='Lista de agentes'
            tabIndex={0}
            aria-busy={list.loading}
          >
            <table className='inventory-table'>
              <thead>
                <tr>
                  <th>Profissional / matrícula</th>
                  <th>Categoria de atuação</th>
                  <th>Disponibilidade</th>
                  <th>Bairro / área atual</th>
                  <th>
                    <span className='sr-only'>Ações</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {visible.map((agent) => (
                  <tr key={agent.id}>
                    <td>
                      <strong>{agent.name}</strong>
                      <span>{agent.badgeNumber}</span>
                    </td>
                    <td>{agentRoleLabel(agent.role)}</td>
                    <td>
                      <span
                        className={`inventory-badge state-${getAgentState(agent.availability).tone}`}
                      >
                        {getAgentState(agent.availability).label}
                      </span>
                    </td>
                    <td>{agent.neighborhood || 'Não informado'}</td>
                    <td>
                      <button
                        className='secondary'
                        disabled={saving || list.loading || Boolean(list.error)}
                        onClick={() => openDetails(agent.id)}
                        aria-label={`Ver detalhes de ${agent.name}`}
                      >
                        Detalhes
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {filtered.length > 10 && (
          <nav
            className='inventory-pagination'
            aria-label='Páginas de agentes'
          >
            <button
              className='secondary'
              disabled={visiblePage === 1}
              onClick={() => setPage(visiblePage - 1)}
            >
              Anterior
            </button>
            <span>
              Página {visiblePage} de {pages}
            </span>
            <button
              className='secondary'
              disabled={visiblePage === pages}
              onClick={() => setPage(visiblePage + 1)}
            >
              Próxima
            </button>
          </nav>
        )}
        {list.receivedAt && (
          <p className='inventory-muted'>
            Última consulta válida:{' '}
            <time dateTime={list.receivedAt}>
              {new Date(list.receivedAt).toLocaleString('pt-BR')}
            </time>
            . Busca e filtros usam a lista carregada.
          </p>
        )}
      </section>
      {mode !== 'list' && (
        <div
          ref={content}
          tabIndex={-1}
          className='inventory-workspace'
        >
          {mode !== 'create' && (
            <button
              className='secondary inventory-back'
              disabled={saving}
              onClick={close}
            >
              Voltar à lista
            </button>
          )}
          {mode === 'create' ? (
            <AgentForm
              key='new'
              saving={saving}
              onCancel={close}
              onSave={(payload, id) =>
                write((signal) => api.save(payload, id, signal), 'Agente cadastrado com sucesso.')
              }
            />
          ) : detail.loading ? (
            <p role='status'>Carregando os dados completos do agente…</p>
          ) : detail.error ? (
            <p
              className='message error'
              role='alert'
            >
              {detail.error}{' '}
              <button
                className='secondary'
                onClick={() => openDetails(selected)}
              >
                Tentar novamente
              </button>
            </p>
          ) : detail.agent ? (
            mode === 'edit' ? (
              <AgentForm
                key={detail.agent.id}
                agent={detail.agent}
                saving={saving}
                onCancel={() => {
                  setMode('details');
                  setNotice(null);
                }}
                onSave={(payload, id) =>
                  write(
                    (signal) => api.save(payload, id, signal),
                    'Alterações do agente salvas com sucesso.',
                  )
                }
              />
            ) : (
              <AgentDetails
                agent={detail.agent}
                vehicle={detail.vehicle}
                vehicles={detail.vehicles}
                vehicleError={detail.vehicleError}
                vehiclesError={detail.vehiclesError}
                manager={manager}
                saving={saving}
                onEdit={() => {
                  setMode('edit');
                  setNotice(null);
                }}
                onRetry={() => openDetails(selected)}
                onLocation={(payload) =>
                  write(
                    (signal) => api.location(detail.agent.id, payload, signal),
                    'Posição e disponibilidade atualizadas.',
                    detail.agent.id,
                  )
                }
                onVehicle={(vehicleId, expected) =>
                  write(
                    (signal) => api.assignVehicle(detail.agent.id, vehicleId, expected, signal),
                    'Vínculo do veículo atualizado.',
                    detail.agent.id,
                  )
                }
              />
            )
          ) : null}
        </div>
      )}
    </div>
  );
}
