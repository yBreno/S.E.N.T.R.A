// Bibliotecas externas
import { useCallback, useEffect, useRef, useState } from "react";

// Serviços e utilitários
import { canMonitor, login as authenticate } from "../services/auth.js";
import { invalidResponse, isAbortError } from "../services/http.js";
import {
  normalizeSession,
  restoreSession,
  scheduleSessionExpiration,
  sessionStore,
} from "../services/session.js";

export function useAuth() {
  const [session, setSession] = useState(null);
  const [error, setError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isRestoring, setIsRestoring] = useState(true);

  const pendingLogin = useRef(null);
  const pendingRestore = useRef(null);

  useEffect(() => () => pendingLogin.current?.abort(), []);

  const logout = useCallback((message = "") => {
    pendingLogin.current?.abort();
    pendingRestore.current?.abort();
    sessionStore.clear();
    setSession(null);
    setIsSubmitting(false);
    setIsRestoring(false);
    setError(message);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    pendingRestore.current = controller;

    async function restore() {
      try {
        const restored = await restoreSession({ signal: controller.signal });
        if (!controller.signal.aborted) setSession(restored);
      } catch (error) {
        if (!controller.signal.aborted && !isAbortError(error)) {
          setError(
            error.status === 401
              ? "A API recusou a sessão (401). Entre novamente."
              : "Não foi possível validar a sessão anterior. Entre novamente.",
          );
        }
      } finally {
        if (!controller.signal.aborted) setIsRestoring(false);
      }
    }

    queueMicrotask(() => {
      if (!controller.signal.aborted) void restore();
    });

    return () => controller.abort();
  }, []);

  const login = useCallback(async (credentials) => {
    pendingLogin.current?.abort();
    pendingRestore.current?.abort();
    const controller = new AbortController();
    pendingLogin.current = controller;
    setIsSubmitting(true);
    setIsRestoring(false);
    setError("");
    try {
      const authenticated = await authenticate(credentials, {
        signal: controller.signal,
      });
      if (!controller.signal.aborted) {
        const valid = normalizeSession(authenticated);
        if (!valid) throw invalidResponse();
        sessionStore.save(valid);
        setSession(valid);
      }
    } catch (error) {
      if (!controller.signal.aborted && !isAbortError(error))
        setError(error.message);
    } finally {
      if (!controller.signal.aborted) setIsSubmitting(false);
    }
  }, []);

  useEffect(() => {
    if (!session) return;

    return scheduleSessionExpiration(session, () =>
      logout("Sua sessão expirou. Entre novamente."),
    );
  }, [session, logout]);

  return {
    session,
    canMonitor: canMonitor(session?.user),
    error,
    isSubmitting,
    isRestoring,
    login,
    logout,
  };
}
