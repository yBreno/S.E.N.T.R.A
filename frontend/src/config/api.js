export function resolveApiConfig(value) {
  const invalid = {
    baseUrl: null,
    error: 'Configure VITE_API_BASE_URL em frontend/.env.local com a URL HTTP ou HTTPS da API, sem /api, credenciais, consulta ou fragmento.'
  };
  if (typeof value !== 'string' || !value.trim()) return invalid;
  try {
    const url = new URL(value.trim());
    if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.search || url.hash) return invalid;
    const baseUrl = url.href.replace(/\/+$/, '');
    if (url.pathname.replace(/\/+$/, '').endsWith('/api')) return invalid;

    return {
      baseUrl,
      error: null
    };
  } catch {
    return invalid;
  }
}
export const apiConfig = resolveApiConfig(import.meta.env?.VITE_API_BASE_URL);
