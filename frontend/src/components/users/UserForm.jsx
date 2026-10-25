import { useState } from 'react';
import InventoryField from '../inventory/InventoryField.jsx';
import { userRoles } from '../../services/authorization.js';
import { buildUserForm } from './userModel.js';

export default function UserForm({ account, saving, serverErrors, onSave, onCancel }) {
  const [form, setForm] = useState({ name: account?.name ?? '', email: account?.email ?? '', role: '', cpf: '', password: '' });
  const [errors, setErrors] = useState({});
  const specs = [
    { name: 'name', label: 'Nome', required: true, maxLength: 150, autoComplete: 'name' },
    { name: 'email', label: 'E-mail de acesso', type: 'email', required: true, maxLength: 200, autoComplete: 'email' },
    ...(!account ? [
      { name: 'cpf', label: 'CPF (somente no cadastro)', required: true, maxLength: 14, autoComplete: 'off' },
      { name: 'role', label: 'Cargo de autorização', required: true, options: Object.entries(userRoles).map(([value, label]) => ({ value, label })) },
      { name: 'password', label: 'Senha inicial privada', type: 'password', required: true, autoComplete: 'new-password' }
    ] : [])
  ];
  return <section className="inventory-panel"><h2>{account ? 'Editar conta' : 'Criar conta de acesso'}</h2>
    <p className="inventory-muted">Cargo de login é diferente da categoria profissional. A senha não é retornada nem enviada por e-mail nesta etapa.</p>
    <form onSubmit={event => { event.preventDefault(); const result = buildUserForm(form, account); setErrors(result.errors); if (result.payload) onSave(result.payload); }}>
      <fieldset disabled={saving}><legend>Dados da conta</legend><div className="inventory-form-grid">{specs.map(spec => <InventoryField key={spec.name} spec={spec} idPrefix="user-" value={form[spec.name]}
        onChange={value => { setForm(previous => ({ ...previous, [spec.name]: value })); setErrors(previous => ({ ...previous, [spec.name]: null })); }}
        error={errors[spec.name] || (Object.keys(serverErrors).some(key => key.toLowerCase() === spec.name.toLowerCase()) ? 'Confira este campo.' : null)} />)}</div></fieldset>
      <div className="inventory-actions"><button disabled={saving}>{saving ? 'Salvando…' : account ? 'Salvar dados' : 'Criar usuário'}</button><button type="button" className="secondary" disabled={saving} onClick={onCancel}>Cancelar</button></div>
    </form>
  </section>;
}
