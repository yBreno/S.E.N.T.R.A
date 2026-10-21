import { useCallback, useEffect, useRef, useState } from 'react'
import { canMonitor, login as authenticate } from '../services/auth.js'
import { isAbortError } from '../services/http.js'

export function useAuth() {
  const [session, setSession] = useState(null)
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const pendingLogin = useRef(null)

  useEffect(() => () => pendingLogin.current?.abort(), [])

  const logout = useCallback((message = '') => {
    pendingLogin.current?.abort()
    setSession(null)
    setIsSubmitting(false)
    setError(message)
  }, [])

  const login = useCallback(async (credentials) => {
    pendingLogin.current?.abort()
    const controller = new AbortController()
    pendingLogin.current = controller
    setIsSubmitting(true)
    setError('')
    try {
      const authenticated = await authenticate(credentials, { signal: controller.signal })
      if (!controller.signal.aborted) setSession(authenticated)
    } catch (error) {
      if (!controller.signal.aborted && !isAbortError(error)) setError(error.message)
    } finally {
      if (!controller.signal.aborted) setIsSubmitting(false)
    }
  }, [])

  useEffect(() => {
    if (!session) return
    let timer
    function checkExpiration() {
      const remaining = Date.parse(session.expiresAt) - Date.now()
      if (remaining <= 0) logout('Sua sessão expirou. Entre novamente.')
      else timer = setTimeout(checkExpiration, Math.min(remaining, 2_147_483_647))
    }
    timer = setTimeout(checkExpiration, Math.max(0, Math.min(Date.parse(session.expiresAt) - Date.now(), 2_147_483_647)))
    return () => clearTimeout(timer)
  }, [session, logout])

  return { session, canMonitor: canMonitor(session?.user), error, isSubmitting, login, logout }
}
