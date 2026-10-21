import { apiConfig } from '../config/api.js'

const statusMessages = {
  400: 'Dados inválidos (400). Confira os campos ou filtros enviados.',
  401: 'Autenticação não aceita (401). Confira as credenciais ou entre novamente.',
  403: 'Acesso negado (403). Seu usuário não tem permissão para este recurso.',
  404: 'Recurso não encontrado (404). Confira a URL e a rota da API.',
  500: 'Erro interno da API (500). Tente novamente mais tarde.',
}

export class HttpError extends Error {
  constructor(message, { status = null, kind = 'http' } = {}) {
    super(message)
    this.name = 'HttpError'
    this.status = status
    this.kind = kind
  }
}

export function isAbortError(error) {
  return error?.name === 'AbortError'
}

export function invalidResponse() {
  return new HttpError('A API retornou uma resposta incompatível com o contrato esperado.', { kind: 'response' })
}

// A factory permite verificar o transporte sem depender de um servidor real.
export function createHttpClient(config = apiConfig, fetchImplementation = globalThis.fetch) {
  async function request(method, path, { token, signal, body } = {}) {
    if (config.error || !config.baseUrl) {
      throw new HttpError(config.error || 'URL da API não configurada.', { kind: 'configuration' })
    }
    if (!path.startsWith('/api/')) throw new HttpError('Rota de API inválida.', { kind: 'configuration' })
    const headers = { Accept: 'application/json' }
    if (token) headers.Authorization = `Bearer ${token}`
    if (body !== undefined) headers['Content-Type'] = 'application/json'
    try {
      signal?.throwIfAborted()
      const response = await fetchImplementation(`${config.baseUrl}${path}`, {
        method, headers, signal, credentials: 'omit', cache: 'no-store',
        ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
      })
      signal?.throwIfAborted()
      if (!response.ok) {
        // Corpos de erro podem ser HTML, texto ou dados sensíveis; não os exibimos.
        await response.body?.cancel()
        throw new HttpError(statusMessages[response.status] || `A API retornou erro HTTP ${response.status}.`, { status: response.status })
      }
      if (response.status === 204) return null
      const text = await response.text()
      signal?.throwIfAborted()
      try {
        return JSON.parse(text)
      } catch {
        throw invalidResponse()
      }
    } catch (error) {
      if (signal?.aborted) signal.throwIfAborted()
      if (isAbortError(error) || error instanceof HttpError) throw error
      throw new HttpError('Não foi possível conectar à API. Confira se ela está em execução, a URL, o certificado HTTPS e o CORS.', { kind: 'network' })
    }
  }
  return {
    getJson: (path, options) => request('GET', path, options),
    postJson: (path, body, options) => request('POST', path, { ...options, body }),
  }
}

export const http = createHttpClient()
