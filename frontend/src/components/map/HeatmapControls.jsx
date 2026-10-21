// Serviços e utilitários
import { heatmapMetrics, incidentTypes, weekDays } from '../../services/heatmapQuery.js';

// Estilos
import './HeatmapControls.css';

export default function HeatmapControls({
  heatmap,
  layerError
}) {
  const {
    filters,
    data
  } = heatmap;

  const update = key => event => heatmap.changeFilters({
    ...filters,
    [key]: event.target.value
  });

  const busy = heatmap.loading || heatmap.waitingForShared;
  const unavailable = !heatmap.enabled || busy || Boolean(heatmap.validationError);
  const risk = filters.metric === 'Risk';

  return (
    <details className="heatmap-controls">
      <summary>Mapa de calor · {heatmapMetrics[filters.metric]} · {heatmap.enabled ? 'Ativado' : 'Desativado'}</summary>
      <div className="heatmap-controls-body">
        <p>Filtros exclusivos da camada de calor. Alterar campos cancela a consulta anterior; use Aplicar filtros para consultar ou reutilizar os dados correspondentes.</p>
        <form
          onSubmit={event => {
            event.preventDefault();
            if (!unavailable) void heatmap.apply();
          }}
        >
          <div className="heatmap-fields">
            <label htmlFor="heatmap-metric">Métrica<select
              id="heatmap-metric"
              value={filters.metric}
              onChange={update('metric')}
            >
              {Object.entries(heatmapMetrics).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select></label>
            {!risk && (
              <label htmlFor="heatmap-from">Início (data/hora local)<input
                id="heatmap-from"
                type="datetime-local"
                value={filters.from}
                onChange={update('from')}
              /></label>
            )}
            <label htmlFor="heatmap-to">
              {risk ? 'Referência (data/hora local)' : 'Fim (data/hora local)'}
              <input
                id="heatmap-to"
                type="datetime-local"
                value={filters.to}
                onChange={update('to')}
              />
            </label>
            <label htmlFor="heatmap-neighborhood">Bairro<input
              id="heatmap-neighborhood"
              type="text"
              value={filters.neighborhood}
              onChange={update('neighborhood')}
              placeholder="Todos os bairros"
            /></label>
            {!risk && <>
              <label htmlFor="heatmap-from-hour">Hora inicial (0–23)<input
                id="heatmap-from-hour"
                type="number"
                min="0"
                max="23"
                step="1"
                value={filters.fromHour}
                onChange={update('fromHour')}
              /></label>
              <label htmlFor="heatmap-to-hour">Hora final (0–23)<input
                id="heatmap-to-hour"
                type="number"
                min="0"
                max="23"
                step="1"
                value={filters.toHour}
                onChange={update('toHour')}
              /></label>
              <label htmlFor="heatmap-weekday">Dia da semana<select
                id="heatmap-weekday"
                value={filters.dayOfWeek}
                onChange={update('dayOfWeek')}
              >
                <option value="">Todos os dias</option>
                {Object.entries(weekDays).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select></label>
            </>}
            {filters.metric === 'Incidents' && (
              <label htmlFor="heatmap-type">Tipo de ocorrência<select
                id="heatmap-type"
                value={filters.type}
                onChange={update('type')}
              >
                <option value="">Todos os tipos</option>
                {Object.entries(incidentTypes).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select></label>
            )}
          </div>
          <p>
            {risk ? 'Risco considera somente bairro e instante de referência. Sem referência, a API usa o instante atual.' : 'Datas usam o fuso do navegador e são enviadas em UTC. A faixa de horas e o dia seguem o horário local configurado na API; horas devem vir em par e podem atravessar a meia-noite.'}
          </p>
          {filters.metric === 'Failures' && (
            <p>Falhas utiliza exclusivamente EquipmentFailure, definido pelo backend.</p>
          )}
          <div className="actions heatmap-actions">
            <button type="submit" disabled={unavailable}>Aplicar filtros</button>
            <button
              type="button"
              className="secondary"
              disabled={unavailable}
              onClick={() => void heatmap.refresh()}
            >Atualizar calor</button>
          </div>
        </form>
        <div
          className="heatmap-status"
          aria-live="polite"
          aria-busy={busy}
        >
          {!heatmap.enabled && (
            <p>Ative Mapa de calor no controle de camadas para exibir os pontos.</p>
          )}
          {heatmap.validationError && (
            <p className="message error" role="alert">
              {heatmap.validationError}
            </p>
          )}
          {busy && (
            <p role="status">
              {heatmap.waitingForShared ? 'Aguardando a consulta compartilhada de ocorrências…' : 'Carregando mapa de calor…'}
            </p>
          )}
          {heatmap.error && (
            <p className="message error" role="alert">
              {heatmap.error.message}
            </p>
          )}
          {layerError && heatmap.enabled && (
            <p className="message error" role="alert">
              {layerError}
            </p>
          )}
          {(heatmap.stale || busy && data) && (
            <p className="stale-data">Dados desatualizados da mesma consulta; exibindo a última resposta válida.</p>
          )}
          {!data && !busy && !heatmap.error && !heatmap.validationError && (
            <p>Aplique os filtros para carregar esta consulta. Dados de outros filtros não são exibidos.</p>
          )}
          {data && <>
            <dl className="heatmap-counts">
              <div>
                <dt>Recebidos</dt>
                <dd>
                  {data.totalPoints}
                </dd>
              </div>
              <div>
                <dt>Válidos</dt>
                <dd>
                  {data.validCount}
                </dd>
              </div>
              <div>
                <dt>Descartados</dt>
                <dd>
                  {data.discardedCount}
                </dd>
              </div>
              <div>
                <dt>Intensidade zero</dt>
                <dd>
                  {data.zeroCount}
                </dd>
              </div>
              <div>
                <dt>Pontos para desenho</dt>
                <dd>
                  {data.heatPoints.length}
                </dd>
              </div>
            </dl>
            {data.totalPoints === 0
              ? (
                <p>Nenhum ponto retornado pela API para esta consulta.</p>
              )
              : data.validCount === 0
                ? (
                  <p>Nenhum ponto com coordenadas e intensidade válidas.</p>
                )
                : data.heatPoints.length === 0
                  ? (
                    <p>Todos os pontos válidos têm intensidade zero; nenhuma mancha será desenhada.</p>
                  )
                  : null}
            <p>Intensidade zero é válida e contabilizada, sem produzir mancha. Intensidades negativas, não numéricas ou não finitas são descartadas.</p>
          </>}
          {heatmap.receivedAt && (
            <p>Última atualização válida: <time dateTime={heatmap.receivedAt}>
              {new Date(heatmap.receivedAt).toLocaleString('pt-BR')}
            </time></p>
          )}
        </div>
        <div className="heatmap-legend" aria-label="Legenda de intensidade do calor">
          <strong>Intensidade visual</strong>
          <div className="heatmap-swatches" aria-hidden="true">
            <i />
            <i />
            <i />
            <i />
          </div>
          <span>Menor → Maior</span>
          <p>Cores representam a intensidade visual dos valores da API e sua concentração, com influência do zoom. Escala de referência: 0 a {data?.renderMax ?? 1}. Nenhum piso de intensidade é aplicado.</p>
          {risk && (
            <p>Risk é um índice regional, não certeza de ocorrência nem necessariamente uma probabilidade estatística calibrada. Os pontos são referências regionais, não polígonos dos bairros.</p>
          )}
        </div>
      </div>
    </details>
  );
}
