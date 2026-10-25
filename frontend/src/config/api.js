export function resolveApiConfig(value, { demoMode = false } = {}) {
  const invalid = {
    baseUrl: null,
    error:
      'Configure VITE_API_BASE_URL em frontend/.env.local com a URL HTTP ou HTTPS da API, sem /api, credenciais, consulta ou fragmento.',
  };
  if (typeof value !== 'string' || !value.trim()) return invalid;
  try {
    const url = new URL(value.trim());
    if (
      !['http:', 'https:'].includes(url.protocol) ||
      url.username ||
      url.password ||
      url.search ||
      url.hash
    )
      return invalid;
    const baseUrl = url.href.replace(/\/+$/, '');
    if (
      demoMode &&
      (!['localhost', '127.0.0.1'].includes(url.hostname) ||
        url.protocol !== 'http:' ||
        url.port !== '5180' ||
        url.pathname !== '/')
    )
      return {
        baseUrl: null,
        error:
          'O modo DEMO exige a API local isolada em http://127.0.0.1:5180. Destino externo recusado.',
      };
    if (url.pathname.replace(/\/+$/, '').endsWith('/api')) return invalid;

    return {
      baseUrl,
      error: null,
      ...(demoMode ? { demoMode: true } : {}),
    };
  } catch {
    return invalid;
  }
}
export const apiConfig = resolveApiConfig(import.meta.env?.VITE_API_BASE_URL, {
  demoMode: import.meta.env?.VITE_DEMO_MODE === 'true',
});
