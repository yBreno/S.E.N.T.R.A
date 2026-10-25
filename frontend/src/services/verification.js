// Serviços e utilitários
import { http, invalidResponse } from './http.js';
import { normalizeHeatmapResponse } from './heatmap.js';
import { normalizeAlerts } from './operations.js';
import { specificFields, addressFields } from '../components/equipment/equipmentModel.js';

function dashboardSummary(data) {
  if (
    !data ||
    !Number.isFinite(data.totalEquipments) ||
    !Number.isFinite(data.activeAgents) ||
    !Number.isFinite(data.availabilityRatePercent) ||
    !data.byStatus ||
    Array.isArray(data.byStatus) ||
    typeof data.byStatus !== 'object' ||
    !Object.values(data.byStatus).every(Number.isFinite)
  ) {
    throw invalidResponse();
  }

  return data;
}

function arrayCount(data) {
  if (!Array.isArray(data)) throw invalidResponse();

  return data.length;
}

function selectFields(records, fields) {
  // Mantém apenas os dados operacionais necessários; CPF e atributos extras não são retidos.
  return records.map((record) =>
    Object.fromEntries(fields.map((field) => [field, record?.[field]])),
  );
}
function equipmentDetails(records) {
  return selectFields(records, [
    'id',
    'type',
    'brand',
    'model',
    'serialNumber',
    'status',
    'category',
    'maintenanceNeed',
    'nextMaintenanceDate',
    'neighborhood',
    'latitude',
    'longitude',
  ]).map((record, index) => ({
    ...record,
    attributes: selectFields(
      [records[index]?.attributes],
      (specificFields[record.type] || []).map((field) => field.name),
    )[0],
    address: records[index]?.address
      ? selectFields(
          [records[index].address],
          addressFields.map((field) => field.name),
        )[0]
      : null,
  }));
}

export const verificationEndpoints = [
  {
    id: 'dashboard',
    label: 'Dashboard',
    path: '/api/dashboard?includePredictive=false',
    summarize: dashboardSummary,
  },
  {
    id: 'equipments',
    label: 'Equipamentos',
    path: '/api/equipments',
    summarize: arrayCount,
    selectItems: equipmentDetails,
  },
  {
    id: 'agents',
    label: 'Agentes',
    path: '/api/agents',
    summarize: arrayCount,
    selectItems: (records) =>
      selectFields(records, [
        'id',
        'name',
        'badgeNumber',
        'role',
        'availability',
        'neighborhood',
        'latitude',
        'longitude',
        'locationUpdatedAt',
      ]),
  },
  {
    id: 'heatmap',
    label: 'Pontos do heatmap (Incidents)',
    path: '/api/heatmap?metric=Incidents',
  },
];
export async function loadVerification(
  token,
  { signal, includeMaintenance = false, client = http } = {},
) {
  const endpoints = [
    ...verificationEndpoints,
    {
      id: 'alerts',
      label: 'Alertas abertos',
      path: '/api/alerts?status=Open',
      summarize: (data) => normalizeAlerts(data).length,
      selectItems: normalizeAlerts,
    },
    ...(includeMaintenance
      ? ['Scheduled', 'Open'].map((status) => ({
          id: `maintenance${status}`,
          label: `Manutenções ${status}`,
          path: `/api/maintenances?status=${status}`,
          summarize: arrayCount,
          selectItems: (records) =>
            selectFields(records, [
              'id',
              'equipmentId',
              'status',
              'kind',
              'scheduledDate',
              'initDate',
            ]),
        }))
      : []),
  ];
  const responses = await Promise.allSettled(
    endpoints.map(async (endpoint) => {
      const data = await client.getJson(endpoint.path, {
        token,
        signal,
      });
      if (endpoint.id === 'heatmap') {
        const heatmap = normalizeHeatmapResponse(data, 'Incidents');

        return {
          data: heatmap.totalPoints,
          heatmap,
        };
      }
      const summary = endpoint.summarize(data);

      return {
        data: summary,
        ...(endpoint.selectItems
          ? {
              items: endpoint.selectItems(data),
            }
          : {}),
      };
    }),
  );
  signal?.throwIfAborted();

  return responses.map((response, index) => ({
    id: endpoints[index].id,
    label: endpoints[index].label,
    path: endpoints[index].path,
    ...(response.status === 'fulfilled'
      ? response.value
      : {
          error: response.reason,
        }),
  }));
}
