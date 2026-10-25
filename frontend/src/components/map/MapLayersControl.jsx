// Serviços e utilitários
import { agentStates, equipmentStates } from './markerData.js';
import { getLayerCounts } from './operationalFilters.js';

function LayerMessage({ label, singular, layer, enabled, loading, error }) {
  const counts = getLayerCounts(layer, enabled);
  const failure = layer.error || error;
  const entries = [
    ['received', 'Recebidos'],
    ['valid', 'Válidos'],
    ['filtered', 'Após filtros'],
    ['discarded', 'Descartados'],
    ['visible', 'Na camada'],
  ];

  return (
    <div>
      <h4>{label}</h4>
      <dl className='layer-counts'>
        {entries.map(([key, name]) => (
          <div key={key}>
            <dt>{name}</dt>
            <dd data-counter={key}>{counts[key] ?? '—'}</dd>
          </div>
        ))}
      </dl>
      {loading && (
        <p role='status'>
          {layer.hasData
            ? 'Atualizando; exibindo a última resposta válida, possivelmente desatualizada.'
            : 'Carregando…'}
        </p>
      )}
      {!loading && failure && <p role='alert'>{failure}</p>}
      {layer.stale && (
        <p className='stale-data'>
          Dados desatualizados: a última tentativa não substituiu a resposta válida anterior.
        </p>
      )}
      {layer.receivedAt && (layer.stale || loading) && (
        <p>
          Última resposta válida:{' '}
          <time dateTime={layer.receivedAt}>
            {new Date(layer.receivedAt).toLocaleString('pt-BR')}
          </time>
        </p>
      )}
      {!loading && !layer.hasData && !failure && <p>Sem resposta disponível.</p>}
      {!loading &&
        layer.hasData &&
        (layer.total === 0 ? (
          <p>Nenhum {singular} retornado pela API.</p>
        ) : layer.markers.length === 0 ? (
          <p>Nenhum {singular} com coordenadas e identificador válidos.</p>
        ) : layer.filtered.length === 0 ? (
          <p>Nenhum {singular} encontrado com os filtros atuais.</p>
        ) : null)}
      {layer.discarded > 0 && (
        <p>Descartados por coordenadas ou identificadores inválidos/duplicados.</p>
      )}
      {!enabled && <p>Camada desativada. Os registros após filtros continuam disponíveis.</p>}
    </div>
  );
}

export default function MapLayersControl({
  layers,
  showEquipments,
  showAgents,
  onEquipmentsChange,
  onAgentsChange,
  loading,
  error,
  heatmap,
}) {
  return (
    <div className='map-layers'>
      <fieldset className='layer-toggles'>
        <legend>Camadas no mapa</legend>
        <label>
          <input
            type='checkbox'
            checked={showEquipments}
            onChange={(event) => onEquipmentsChange(event.target.checked)}
          />
          <span
            className='layer-shape equipment-shape'
            aria-hidden='true'
          />{' '}
          Equipamentos
        </label>
        <label>
          <input
            type='checkbox'
            checked={showAgents}
            onChange={(event) => onAgentsChange(event.target.checked)}
          />
          <span
            className='layer-shape agent-shape'
            aria-hidden='true'
          />{' '}
          Agentes
        </label>
        <label>
          <input
            id='heatmap-enabled'
            type='checkbox'
            checked={heatmap.enabled}
            onChange={(event) => heatmap.changeEnabled(event.target.checked)}
          />
          <span
            className='layer-shape heat-shape'
            aria-hidden='true'
          />{' '}
          Mapa de calor
        </label>
      </fieldset>
      {loading && (
        <p
          className='layer-inline-message'
          role='status'
        >
          Atualizando recursos…
        </p>
      )}
      {!loading && (layers.equipments.error || layers.agents.error || error) && (
        <p
          className='layer-alert'
          role='alert'
        >
          {layers.equipments.error || layers.agents.error || error}
        </p>
      )}
      {!loading && (layers.equipments.stale || layers.agents.stale) && (
        <p className='stale-data'>Exibindo recursos da última resposta válida.</p>
      )}
      {!loading && layers.equipments.hasData && layers.equipments.total === 0 && (
        <p className='layer-inline-message'>Nenhum equipamento retornado pela API.</p>
      )}
      {!loading && layers.agents.hasData && layers.agents.total === 0 && (
        <p className='layer-inline-message'>Nenhum agente retornado pela API.</p>
      )}
      <details className='layer-details'>
        <summary>Resumo e legenda das camadas</summary>
        <div
          className='layer-messages layer-statistics'
          aria-live='polite'
        >
          <LayerMessage
            label='Equipamentos'
            singular='equipamento'
            layer={layers.equipments}
            enabled={showEquipments}
            loading={loading}
            error={error}
          />
          <LayerMessage
            label='Agentes'
            singular='agente'
            layer={layers.agents}
            enabled={showAgents}
            loading={loading}
            error={error}
          />
        </div>
        <details className='marker-legend'>
          <summary>Legenda dos estados</summary>
          <div className='legend-columns'>
            <div>
              <strong>Equipamentos · ícone quadrado</strong>
              <ul>
                {Object.entries(equipmentStates).map(([key, state]) => (
                  <li key={key}>
                    <span
                      className={`legend-dot state-${state.tone}`}
                      aria-hidden='true'
                    />
                    {state.label}
                  </li>
                ))}
              </ul>
            </div>
            <div>
              <strong>Agentes · ícone circular</strong>
              <ul>
                {Object.entries(agentStates).map(([key, state]) => (
                  <li key={key}>
                    <span
                      className={`legend-dot state-${state.tone}`}
                      aria-hidden='true'
                    />
                    {state.label}
                  </li>
                ))}
                <li>
                  <span
                    className='legend-dot state-unknown'
                    aria-hidden='true'
                  />
                  Estado não informado ou desconhecido (ambas as camadas)
                </li>
              </ul>
            </div>
          </div>
        </details>
      </details>
    </div>
  );
}
