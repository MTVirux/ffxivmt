import type { ApiEnvelope } from './types';

const BASE = import.meta.env.VITE_API_BASE_URL ?? '/api/v1';

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly body: unknown,
    message?: string,
  ) {
    super(message ?? `API ${status}`);
    this.name = 'ApiError';
  }
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    ...init,
    headers: {
      Accept: 'application/json',
      ...(init?.headers ?? {}),
    },
  });

  let body: unknown;
  try {
    body = await res.json();
  } catch {
    body = undefined;
  }

  if (!res.ok) {
    throw new ApiError(res.status, body);
  }

  return body as T;
}

export async function apiGet<T>(path: string, init?: RequestInit): Promise<T> {
  const env = await apiFetch<ApiEnvelope<T>>(path, init);
  if (!env.status) {
    throw new ApiError(0, env, env.message);
  }
  return env.data;
}

// Same error semantics as apiGet, but hands back the whole envelope for endpoints
// that put meaningful fields (item_name, location, request_id) beside `data`.
export async function apiGetEnvelope<T extends { status: boolean; message?: string }>(
  path: string,
  init?: RequestInit,
): Promise<T> {
  const env = await apiFetch<T>(path, init);
  if (!env.status) {
    throw new ApiError(0, env, env.message);
  }
  return env;
}
