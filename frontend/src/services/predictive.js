import { http, invalidResponse } from './http.js';
import { canMonitor, canManageEquipments } from './auth.js';

export const riskLabels = { Low: 'Baixo', Medium: 'Médio', High: 'Alto', Critical: 'Crítico' };
export const predictiveEndpoints = [
  { id: 'equipment', label: 'Risco dos equipamentos', path: '/api/predictions/equipment-risk' },
  { id: 'neighborhoods', label: 'Exposição dos bairros', path: '/api/risk' },
  { id: 'recommendations', label: 'Recomendações pendentes', path: '/api/recommendations?status=Pending' },
  { id: 'failures', label: 'Estimativas MTBF', path: '/api/predictions/failures' },
  { id: 'anomalies', label: 'Anomalias registradas', path: '/api/predictions/anomalies' }
];
const finite = value => typeof value === 'number' && Number.isFinite(value);
const text = value => typeof value === 'string';
const date = value => text(value) && Number.isFinite(Date.parse(value));
const score = value => finite(value) && value >= 0 && value <= 100;
function factors(values) {
  if (!Array.isArray(values)) throw invalidResponse();
  return values.map(f => {
    if (!f || !text(f.name) || !text(f.label) || !text(f.rawDescription) || !text(f.rule) || !finite(f.points) || !finite(f.weight) || !finite(f.normalized) || !Number.isInteger(f.recordCount)) throw invalidResponse();
    return { name: f.name, label: f.label, rawDescription: f.rawDescription, rule: f.rule, points: f.points, weight: f.weight, normalized: f.normalized, recordCount: f.recordCount };
  });
}
function assessment(value, equipment) {
  if (!value || !score(value.score) || !Object.hasOwn(riskLabels, value.level) || !date(value.evaluatedAt) || !text(value.neighborhood) || !text(value.summary)) throw invalidResponse();
  const common = { score: value.score, level: value.level, evaluatedAt: value.evaluatedAt, neighborhood: value.neighborhood, summary: value.summary, factors: factors(value.factors) };
  if (!equipment) return { ...common, latitude: value.latitude, longitude: value.longitude, incidentWindowFrom: value.incidentWindowFrom, failureWindowFrom: value.failureWindowFrom };
  if (!text(value.equipmentId) || !text(value.serialNumber) || !text(value.equipmentType) || !text(value.status) || !date(value.windowFrom) || typeof value.hasSufficientHistory !== 'boolean' || !text(value.historyDescription)) throw invalidResponse();
  return { ...common, equipmentId: value.equipmentId, serialNumber: value.serialNumber, equipmentType: value.equipmentType, status: value.status,
    windowFrom: value.windowFrom, hasSufficientHistory: value.hasSufficientHistory, historyDescription: value.historyDescription,
    recommendedAction: text(value.recommendedAction) ? value.recommendedAction : null };
}
function recommendation(value) {
  if (!value || !text(value.id) || !['SendMaintenance', 'DeployAgent'].includes(value.type) || !text(value.neighborhood) || !text(value.reason) || !['Pending', 'Accepted', 'Dismissed'].includes(value.status) || !date(value.createdAt) || (value.riskScore != null && !score(value.riskScore))) throw invalidResponse();
  // Somente campos de evidência conhecidos entram no estado da tela; nunca payload bruto ou CPF.
  const e = value.explanation;
  const source = e?.assessment ?? e?.risk;
  return { id: value.id, type: value.type, neighborhood: value.neighborhood, reason: value.reason, status: value.status,
    riskScore: value.riskScore, equipmentId: text(value.equipmentId) ? value.equipmentId : null, createdAt: value.createdAt,
    action: text(e?.action) ? e.action : null, rule: text(e?.rule) ? e.rule : null,
    factors: Array.isArray(source?.factors) ? factors(source.factors) : [],
    evidence: text(e?.anomaly?.explanation) ? e.anomaly.explanation : text(e?.prediction?.explanation) ? e.prediction.explanation : null,
    priority: source?.level && Object.hasOwn(riskLabels, source.level) ? source.level : e?.anomaly?.kind === 'RepeatedFailure' || e?.anomaly?.kind === 'VolumeSpike' || ['Warning', 'Overdue'].includes(e?.prediction?.state) ? 'High' : null };
}
function failure(value) {
  if (!value || !text(value.equipmentId) || !text(value.serialNumber) || !text(value.neighborhood) || !text(value.explanation) || !finite(value.mtbfDays) || !finite(value.ratio) || !finite(value.estimatedDaysToFailure) || !Number.isInteger(value.sampleSize) || !['Equipment', 'Type'].includes(value.mtbfBasis)) throw invalidResponse();
  return { equipmentId: value.equipmentId, serialNumber: value.serialNumber, neighborhood: value.neighborhood, explanation: value.explanation,
    mtbfDays: value.mtbfDays, ratio: value.ratio, estimatedDaysToFailure: value.estimatedDaysToFailure, sampleSize: value.sampleSize, mtbfBasis: value.mtbfBasis };
}
function anomaly(value) {
  if (!value || !text(value.kind) || !text(value.subject) || !text(value.neighborhood) || !text(value.explanation) || !date(value.detectedAt) || !finite(value.current) || !Number.isInteger(value.recordCount)) throw invalidResponse();
  return { kind: value.kind, subject: value.subject, neighborhood: value.neighborhood, explanation: value.explanation, detectedAt: value.detectedAt,
    current: value.current, recordCount: value.recordCount, equipmentId: text(value.equipmentId) ? value.equipmentId : null };
}
const normalizers = { equipment: value => assessment(value, true), neighborhoods: value => assessment(value, false), recommendations: recommendation, failures: failure, anomalies: anomaly };
export function normalizeExecutionStatus(value) {
  const execution = value?.execution;
  if (typeof value?.enabled !== 'boolean' || !Number.isInteger(value.intervalMinutes) || typeof value.multipleInstancesSupported !== 'boolean' ||
      !execution || !['NotRun', 'Running', 'Completed', 'Failed'].includes(execution.status) || ![null, 'Manual', 'Automatic'].includes(execution.origin) || execution.scope !== 'InstanceMemory') throw invalidResponse();
  for (const field of ['startedAt', 'finishedAt', 'lastCompletedAt', 'lastAutomaticCompletedAt']) if (execution[field] != null && !date(execution[field])) throw invalidResponse();
  for (const field of ['recommendationsCreated', 'preventiveAlertsCreated']) if (execution[field] != null && (!Number.isInteger(execution[field]) || execution[field] < 0)) throw invalidResponse();
  return { enabled: value.enabled, intervalMinutes: value.intervalMinutes, multipleInstancesSupported: value.multipleInstancesSupported,
    execution: Object.fromEntries(['status', 'origin', 'startedAt', 'finishedAt', 'lastCompletedAt', 'lastAutomaticCompletedAt', 'recommendationsCreated', 'preventiveAlertsCreated', 'scope'].map(field => [field, execution[field]])) };
}
export function normalizePredictiveOperations(value) {
  if (!value || !date(value.evaluatedAt) || !Array.isArray(value.equipmentRisks) || !Array.isArray(value.neighborhoods) || !Array.isArray(value.recommendations)) throw invalidResponse();
  return { evaluatedAt: value.evaluatedAt, equipmentRisks: value.equipmentRisks.map(item => assessment(item, true)),
    neighborhoods: value.neighborhoods.map(item => assessment(item, false)), recommendations: value.recommendations.map(recommendation), automation: normalizeExecutionStatus(value.automation) };
}
export function attachPredictiveRisk(records, result) {
  const risks = new Map((result?.data?.equipmentRisks ?? []).filter(item => item.level === 'High').map(item => [item.equipmentId, item]));
  return records.map(record => {
    const risk = risks.get(record.id);
    return { ...record, predictiveRisk: risk ? { score: risk.score, evaluatedAt: risk.evaluatedAt, summary: risk.summary, stale: Boolean(result.stale || result.error) } : null };
  });
}
const permitted = allowed => { if (!allowed) throw new Error('Seu perfil não tem permissão para esta ação.'); };
export function createPredictiveApi(session, client = http) {
  const options = signal => ({ token: session.token, signal });
  return {
    async operations(signal) {
      permitted(canMonitor(session.user));
      return normalizePredictiveOperations(await client.getJson('/api/dashboard/predictive', options(signal)));
    },
    async executionStatus(signal) {
      permitted(canMonitor(session.user));
      return normalizeExecutionStatus(await client.getJson('/api/predictions/execution-status', options(signal)));
    },
    async load(signal) {
      permitted(canMonitor(session.user));
      return Promise.all(predictiveEndpoints.map(async endpoint => {
        try {
          const data = await client.getJson(endpoint.path, options(signal));
          if (!Array.isArray(data)) throw invalidResponse();
          return { ...endpoint, data: data.map(normalizers[endpoint.id]) };
        } catch (error) { return { ...endpoint, error }; }
      }));
    },
    async analyze(signal) {
      permitted(canMonitor(session.user));
      const data = await client.postJson('/api/predictions/analyze', undefined, options(signal));
      if (!Number.isInteger(data?.preventiveAlertsCreated) || !Number.isInteger(data?.recommendationsCreated)) throw invalidResponse();
      return { preventiveAlertsCreated: data.preventiveAlertsCreated, recommendationsCreated: data.recommendationsCreated };
    },
    async resolve(id, action, signal) {
      permitted(canManageEquipments(session.user));
      if (!['accept', 'dismiss'].includes(action)) throw new Error('Ação inválida.');
      return recommendation(await client.postJson(`/api/recommendations/${encodeURIComponent(id)}/${action}`, undefined, options(signal)));
    }
  };
}
export function recommendationPriority(rec) {
  if (rec.priority) return rec.priority;
  if (rec.riskScore != null) return rec.riskScore >= 75 ? 'Critical' : rec.riskScore >= 50 ? 'High' : rec.riskScore >= 25 ? 'Medium' : 'Low';
  return rec.priority;
}
export function prioritizeRecommendations(items) {
  const ranks = { Critical: 4, High: 3, Medium: 2, Low: 1 };
  return [...items].sort((a, b) => (ranks[recommendationPriority(b)] ?? 0) - (ranks[recommendationPriority(a)] ?? 0) || (b.riskScore ?? -1) - (a.riskScore ?? -1) || a.createdAt.localeCompare(b.createdAt));
}
export function predictiveError(error, { reading = false } = {}) {
  if (reading && (error?.kind === 'network' || error?.name === 'AbortError')) return 'Não foi possível consultar esta fonte. Confira a conexão e atualize os dados.';
  if (error?.status === 409) return 'Já existe uma ordem ativa ou os dados mudaram. Atualize os dados e consulte a manutenção existente.';
  if (error?.status === 400) return 'A ação não atende às regras atuais do recurso. Atualize os dados e confira equipamento, situação e ordens existentes.';
  if (error?.kind === 'network' || error?.name === 'AbortError') return 'Não foi possível confirmar a operação. Atualize os dados antes de repetir o envio.';
  return error?.message || 'Não foi possível concluir a operação. Atualize os dados antes de tentar novamente.';
}
