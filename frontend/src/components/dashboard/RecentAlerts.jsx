// Componentes
import { useState } from 'react';
import { alertSeverities, prioritizeAlerts } from '../../services/operations.js';

export default function RecentAlerts({ result, equipments = [], loading, onEquipment }) {
  const [limit, setLimit] = useState(6);
  const alerts = prioritizeAlerts(result?.items || []);
  const names = new Map(equipments.map((item) => [item.id, item.serialNumber]));
  return (
    <section
      className='dashboard-side-panel recent-alerts'
      id='alertas-recentes'
      aria-labelledby='alerts-heading'
    >
      <header className='side-panel-heading'>
        <h2 id='alerts-heading'>Alertas recentes</h2>
        <p>Eventos que exigem atenção</p>
      </header>
      {loading && <p role='status'>Atualizando alertas…</p>}
      {(result?.error || result?.stale) && (
        <p role='alert'>
          Falha ao sincronizar alertas.{' '}
          {result?.items
            ? 'Exibindo a última resposta válida, possivelmente desatualizada.'
            : 'Alertas indisponíveis.'}
        </p>
      )}
      {!loading && result?.items && !alerts.length && (
        <p className='alerts-empty'>Nenhum alerta aberto na resposta da API.</p>
      )}
      {!loading && !result && <p>Consultando alertas…</p>}
      <ul className='operations-alert-list'>
        {alerts.slice(0, limit).map((alert) => (
          <li
            key={alert.id}
            className={`severity-${alert.severity}`}
          >
            <strong>
              {alertSeverities[alert.severity]}
              {['High', 'Critical'].includes(alert.severity) ? ' · Atenção urgente' : ''}
            </strong>
            <p>{alert.message}</p>
            <time dateTime={alert.createdAt}>
              {new Date(alert.createdAt).toLocaleString('pt-BR')}
            </time>
            <p>{alert.type === 'Preventive' ? 'Alerta preventivo' : 'Alerta operacional'}</p>
            <button
              className='secondary'
              onClick={() => onEquipment(alert.equipmentId)}
            >
              Ver equipamento {names.get(alert.equipmentId) || alert.equipmentId}
            </button>
          </li>
        ))}
      </ul>
      {alerts.length > limit && (
        <button
          className='secondary'
          onClick={() => setLimit((value) => value + 6)}
        >
          Mostrar mais alertas ({alerts.length - limit})
        </button>
      )}
      {result?.receivedAt && (
        <p className='operations-note'>
          Última resposta válida:{' '}
          <time dateTime={result.receivedAt}>
            {new Date(result.receivedAt).toLocaleString('pt-BR')}
          </time>
        </p>
      )}
    </section>
  );
}
