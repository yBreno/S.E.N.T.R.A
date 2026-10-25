import { useEffect, useMemo, useRef, useState } from 'react';
import { canAdministerUsers, userRoles, userRoleLabel } from '../services/authorization.js';
import { createUsersApi, userError } from '../services/users.js';
import { apiConfig } from '../config/api.js';
import { createPasswordRecoveryApi, recoveryError } from '../services/passwordRecovery.js';
import UserForm from '../components/users/UserForm.jsx';
import InventoryDetails from '../components/inventory/InventoryDetails.jsx';
import './EquipmentPage.css';

const emptyFilters = { search: '', role: '', isActive: '' };
const auditLabels = { Created: 'Conta criada', InitialProvisioning: 'Primeiro administrador provisionado', ProfileUpdated: 'Dados atualizados', RoleChanged: 'Cargo alterado', Deactivated: 'Conta desativada', Reactivated: 'Conta reativada', PasswordChanged: 'Senha alterada pelo titular', PasswordResetRequested: 'Admin solicitou redefinição', PasswordRecoveryRequested: 'Titular solicitou recuperação', PasswordResetCompleted: 'Senha redefinida por link', PasswordResetDeliveryFailed: 'Entrega não confirmada' };
export default function UserPage({ session, onLogout, onSavingChange }) {
  const api = useMemo(() => createUsersApi(session), [session]);
  const allowed = canAdministerUsers(session.user); const readOnly = apiConfig.demoMode === true;
  const [filters, setFilters] = useState(emptyFilters); const [query, setQuery] = useState({ ...emptyFilters, page: 1, pageSize: 10 });
  const [revision, setRevision] = useState(0); const [list, setList] = useState({ loading: true, data: null, error: '' });
  const [selected, setSelected] = useState(null); const [mode, setMode] = useState('list'); const [historyPage, setHistoryPage] = useState(1);
  const [detail, setDetail] = useState({ loading: false, data: null, history: null, error: '' });
  const [saving, setSaving] = useState(false); const [notice, setNotice] = useState(null); const [serverErrors, setServerErrors] = useState({});
  const [confirmation, setConfirmation] = useState(null); const [nextRole, setNextRole] = useState('');
  const pending = useRef(null); const busy = useRef(false);
  const [recoveryEnabled, setRecoveryEnabled] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    createPasswordRecoveryApi().availability(controller.signal).then(setRecoveryEnabled).catch(() => {});
    return () => controller.abort();
  }, []);
  useEffect(() => () => pending.current?.abort(), []);
  useEffect(() => {
    if (!allowed) return;
    const controller = new AbortController();
    queueMicrotask(async () => {
      if (controller.signal.aborted) return; setList(previous => ({ ...previous, loading: true, error: '' }));
      try { const data = await api.list(query, controller.signal); if (!controller.signal.aborted) setList({ loading: false, data, error: '' }); }
      catch (error) { if (controller.signal.aborted) return; if (error.status === 401) onLogout('Sua sessão foi revogada ou expirou. Entre novamente.'); else setList(previous => ({ ...previous, loading: false, error: userError(error) })); }
    });
    return () => controller.abort();
  }, [allowed, api, query, revision, onLogout]);
  useEffect(() => {
    if (!allowed || !selected) return;
    const controller = new AbortController();
    queueMicrotask(async () => {
      if (controller.signal.aborted) return; setDetail({ loading: true, data: null, history: null, error: '' });
      try {
        const [data, history] = await Promise.all([api.details(selected, controller.signal), api.history(selected, historyPage, controller.signal)]);
        if (!controller.signal.aborted) { setDetail({ loading: false, data, history, error: '' }); setNextRole(data.account.role); }
      } catch (error) { if (controller.signal.aborted) return; if (error.status === 401) onLogout('Sua sessão foi revogada ou expirou. Entre novamente.'); else setDetail({ loading: false, data: null, history: null, error: userError(error) }); }
    });
    return () => controller.abort();
  }, [allowed, api, selected, historyPage, revision, onLogout]);
  function open(id, target = 'details') { setSelected(id); setHistoryPage(1); setMode(target); setConfirmation(null); setServerErrors({}); }
  async function write(action, success, formatError = userError) {
    if (busy.current || readOnly) return;
    busy.current = true; setSaving(true); onSavingChange(true); setNotice(null); setServerErrors({});
    const controller = new AbortController(); pending.current = controller;
    const timeout = setTimeout(() => controller.abort(), 20000);
    try {
      const result = await action(controller.signal); if (controller.signal.aborted) return;
      if (result?.reauthenticate) { onLogout('A conta foi alterada e a sessão revogada. Entre novamente.'); return; }
      if (result?.account) open(result.account.id); else setConfirmation(null);
      setRevision(value => value + 1); setNotice({ type: 'success', text: success }); setQuery(previous => ({ ...previous, page: 1 }));
    } catch (error) {
      if (error.status === 401) onLogout('Sua sessão foi revogada ou expirou. Entre novamente.');
      else { setNotice({ type: 'error', text: controller.signal.aborted ? 'Não foi possível confirmar o envio. Atualize os dados antes de repetir.' : formatError(error) }); setServerErrors(error.fieldErrors ?? {}); setConfirmation(null); }
    } finally { clearTimeout(timeout); pending.current = null; busy.current = false; setSaving(false); onSavingChange(false); }
  }
  if (!allowed) return <section className="panel"><h2>Usuários</h2><p role="alert">Somente Admin pode administrar contas.</p></section>;
  const account = detail.data?.account; const pages = list.data ? Math.max(1, Math.ceil(list.data.total / query.pageSize)) : 1;
  return <div className="equipment-page" id="usuarios" aria-busy={saving}>
    <section className="inventory-panel"><div className="inventory-section-heading"><div><p className="inventory-eyebrow">Acesso e segurança</p><h2>Administração de usuários</h2><p className="inventory-muted">Contas, cargos e histórico. Ações exclusivas de Admin.</p></div>
      <div className="inventory-actions"><button className="secondary" disabled={saving || list.loading} onClick={() => setRevision(value => value + 1)}>Atualizar usuários</button>{!readOnly && <button disabled={saving} onClick={() => { open(null, 'create'); setNotice(null); }}>Novo usuário</button>}</div></div>
      {readOnly && <p className="inventory-muted">DEMO: contas disponíveis somente para consulta. O provisionamento sintético permanece manual.</p>}
      {notice && <p className={`message ${notice.type}`} role={notice.type === 'error' ? 'alert' : 'status'}>{notice.text}</p>}
      <form onSubmit={event => { event.preventDefault(); setQuery({ ...filters, page: 1, pageSize: query.pageSize }); }}><fieldset disabled={saving}><legend>Buscar e filtrar contas</legend><div className="inventory-form-grid">
        <label>Nome ou e-mail<input id="users-search" type="search" maxLength={150} value={filters.search} onChange={event => setFilters({ ...filters, search: event.target.value })} /></label>
        <label>Cargo<select id="users-role" value={filters.role} onChange={event => setFilters({ ...filters, role: event.target.value })}><option value="">Todos</option>{Object.entries(userRoles).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
        <label>Estado da conta<select id="users-active" value={filters.isActive} onChange={event => setFilters({ ...filters, isActive: event.target.value })}><option value="">Todos</option><option value="true">Ativos</option><option value="false">Inativos</option></select></label>
      </div><div className="inventory-actions"><button type="submit">Buscar usuários</button><button type="button" className="secondary" onClick={() => { setFilters(emptyFilters); setQuery({ ...emptyFilters, page: 1, pageSize: 10 }); }}>Limpar filtros</button></div></fieldset></form>
      {list.loading && <p role="status">Consultando usuários…</p>}{list.error && <p className="message error" role="alert">{list.error} Os registros anteriores podem estar desatualizados.</p>}
      {list.data?.items.length === 0 && !list.loading && <p className="inventory-empty">Nenhuma conta corresponde à busca e aos filtros.</p>}
      {list.data?.items.length > 0 && <div className="inventory-table-scroll" tabIndex={0} role="region" aria-label="Lista de usuários"><table className="inventory-table"><thead><tr><th>Nome / e-mail</th><th>Cargo de acesso</th><th>Estado</th><th>Ações</th></tr></thead><tbody>{list.data.items.map(user => <tr key={user.id}><td><strong>{user.name}</strong><span>{user.email}</span></td><td>{userRoleLabel(user.role)}</td><td>{user.isActive ? 'Ativo' : 'Inativo'}</td><td><button className="secondary" disabled={saving || list.loading} onClick={() => open(user.id)} aria-label={`Ver conta de ${user.name}`}>Detalhes</button></td></tr>)}</tbody></table></div>}
      {list.data && <nav className="inventory-pagination" aria-label="Páginas de usuários"><button className="secondary" disabled={saving || list.loading || query.page <= 1} onClick={() => setQuery({ ...query, page: query.page - 1 })}>Anterior</button><span>Página {query.page} de {pages} · {list.data.total} conta(s)</span><button className="secondary" disabled={saving || list.loading || query.page >= pages} onClick={() => setQuery({ ...query, page: query.page + 1 })}>Próxima</button></nav>}
    </section>
    {mode === 'create' ? <UserForm key="create" saving={saving} serverErrors={serverErrors} onCancel={() => setMode('list')} onSave={payload => write(signal => api.create(payload, signal), 'Conta criada com sucesso.')} /> : selected && <>
      {detail.loading && <p role="status">Consultando conta e histórico…</p>}{detail.error && <p className="message error" role="alert">{detail.error} <button className="secondary" onClick={() => setRevision(value => value + 1)}>Tentar novamente</button></p>}
      {account && (mode === 'edit' ? <UserForm key={`${account.id}:${account.revision}`} account={account} saving={saving} serverErrors={serverErrors} onCancel={() => setMode('details')} onSave={payload => write(signal => api.update(account.id, payload, signal), 'Dados da conta atualizados.')} /> : <section className="inventory-panel" aria-labelledby="user-detail-title"><h2 id="user-detail-title">Detalhes da conta</h2>
        <InventoryDetails entries={[["Nome", account.name], ["E-mail", account.email], ["Cargo de autorização", userRoleLabel(account.role)], ["Estado", account.isActive ? 'Ativo' : 'Inativo'], ["Criada em", new Date(account.createdAt).toLocaleString('pt-BR')], ["Agente de campo vinculado", detail.data.agentId || 'Nenhum vínculo']]} />
        {!readOnly && <><div className="inventory-actions"><button disabled={saving} onClick={() => setMode('edit')}>Editar nome e e-mail</button><button className="secondary" disabled={saving} onClick={() => setConfirmation({ type: 'status', value: !account.isActive })}>{account.isActive ? 'Desativar conta' : 'Reativar conta'}</button></div>
          <form onSubmit={event => { event.preventDefault(); setConfirmation({ type: 'role', value: nextRole }); }}><label>Cargo de acesso<select id="user-next-role" disabled={saving} value={nextRole} onChange={event => setNextRole(event.target.value)}>{Object.entries(userRoles).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label><button disabled={saving || nextRole === account.role}>Alterar cargo</button></form></>}
        <div className="inventory-actions"><button className="secondary" disabled={saving || readOnly || !recoveryEnabled || !account.isActive} onClick={() => setConfirmation({ type: 'reset' })}>Enviar redefinição de senha</button></div><p className="inventory-muted">{recoveryEnabled ? 'O titular confirma a redefinição pelo e-mail cadastrado. A solicitação preserva a senha atual.' : 'Envio indisponível até a configuração privada do provedor.'} Nenhuma senha ou token é revelado ao administrador.</p>
        {confirmation?.type === 'reset' ? <section aria-labelledby="user-reset-confirm-title"><h3 id="user-reset-confirm-title">Confirmar solicitação de redefinição</h3><p>Enviar uma solicitação ao e-mail cadastrado desta conta? A senha só será alterada após a confirmação do titular pelo link.</p><div className="inventory-actions"><button disabled={saving} onClick={() => write(signal => api.requestPasswordReset(account.id, signal), 'Solicitação registrada. O titular receberá o link se a entrega for possível. Nenhuma senha foi alterada agora.', recoveryError)}>Confirmar envio</button><button className="secondary" disabled={saving} onClick={() => setConfirmation(null)}>Cancelar envio</button></div></section> : confirmation && <section aria-labelledby="user-confirm-title"><h3 id="user-confirm-title">Confirmar alteração de acesso</h3><p>{confirmation.type === 'role' ? `Alterar cargo para ${userRoleLabel(confirmation.value)}?` : confirmation.value ? 'Reativar esta conta?' : 'Desativar esta conta?'} Sessões anteriores serão revogadas. Reativar não restaura sessões antigas. O último Admin ativo é protegido.</p><div className="inventory-actions"><button disabled={saving} onClick={() => write(signal => confirmation.type === 'role' ? api.role(account.id, confirmation.value, account.revision, signal) : api.status(account.id, confirmation.value, account.revision, signal), 'Acesso atualizado e sessões anteriores revogadas.')}>Confirmar alteração</button><button className="secondary" disabled={saving} onClick={() => setConfirmation(null)}>Cancelar alteração</button></div></section>}
        <h3>Histórico administrativo</h3><p className="inventory-muted">Registros desta versão; sem senha, token ou valores pessoais anteriores.</p>
        {!detail.history?.items.length ? <p>Nenhuma alteração registrada nesta versão.</p> : <ul className="operations-resource-list">{detail.history.items.map(event => <li key={event.id}><strong>{auditLabels[event.action] ?? event.action}</strong><span>{new Date(event.createdAt).toLocaleString('pt-BR')} · Autor: {event.actorUserId || 'Provisionamento inicial'}</span>{event.previousRole && <span>{userRoleLabel(event.previousRole)} → {userRoleLabel(event.newRole)}</span>}</li>)}</ul>}
        {detail.history && detail.history.total > 10 && <nav className="inventory-pagination" aria-label="Páginas do histórico"><button className="secondary" disabled={saving || historyPage <= 1} onClick={() => setHistoryPage(value => value - 1)}>Histórico anterior</button><span>Página {historyPage}</span><button className="secondary" disabled={saving || historyPage * 10 >= detail.history.total} onClick={() => setHistoryPage(value => value + 1)}>Histórico seguinte</button></nav>}
      </section>)}
    </>}
  </div>;
}
