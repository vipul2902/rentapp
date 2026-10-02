import { apiBaseUrl } from './config';
import { ApiClientError } from './errors';

export const CORRELATION_HEADER = 'X-Correlation-ID';
const DEFAULT_TIMEOUT_MS = 15_000;

/** Supplied by the session module, so this file stays free of auth state. */
export interface AuthHandler {
  /** A usable access token (refreshing first if needed), or null when signed out. */
  getAccessToken(): Promise<string | null>;
  /** Called after a 401 on an authenticated request. Resolves true if a fresh token is now available. */
  handleUnauthorized(): Promise<boolean>;
}

let authHandler: AuthHandler | null = null;

export function setAuthHandler(handler: AuthHandler | null): void {
  authHandler = handler;
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  body?: unknown;
  /** Cancellation from the caller (TanStack Query passes one to every queryFn). */
  signal?: AbortSignal;
  timeoutMs?: number;
  /** Non-2xx statuses whose body should be returned instead of thrown (e.g. 503 from /health/ready). */
  allowStatuses?: readonly number[];
  /** Attach the access token and refresh once on 401. Default true; sign-in/up/refresh pass false. */
  authenticated?: boolean;
  /** Extra request headers, e.g. Idempotency-Key. */
  headers?: Record<string, string>;
  /** Override for tests. */
  baseUrl?: string | null;
}

interface ApiErrorBody {
  code?: string;
  message?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

/** The single HTTP entry point for the app. Screens never call fetch directly. */
export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const baseUrl = options.baseUrl === undefined ? apiBaseUrl : options.baseUrl;
  if (!baseUrl) {
    throw new ApiClientError({ kind: 'config' });
  }

  const auth = (options.authenticated ?? true) ? authHandler : null;
  const url = `${baseUrl}${path}`;

  let response = await send(url, options, auth ? await auth.getAccessToken() : null);
  if (response.status === 401 && auth && (await auth.handleUnauthorized())) {
    response = await send(url, options, await auth.getAccessToken());
  }

  const body = await readBody(response);
  if (response.ok || options.allowStatuses?.includes(response.status)) {
    return body as T;
  }

  const error = (body ?? {}) as ApiErrorBody;
  throw new ApiClientError({
    kind: 'http',
    status: response.status,
    code: error.code,
    serverMessage: error.message,
    traceId: error.traceId ?? response.headers.get(CORRELATION_HEADER) ?? undefined,
    fieldErrors: error.errors,
  });
}

async function send(url: string, options: RequestOptions, accessToken: string | null): Promise<Response> {
  const controller = new AbortController();
  let timedOut = false;
  const timer = setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, options.timeoutMs ?? DEFAULT_TIMEOUT_MS);

  const onCallerAbort = () => controller.abort();
  options.signal?.addEventListener('abort', onCallerAbort);

  const headers: Record<string, string> = {
    Accept: 'application/json',
    [CORRELATION_HEADER]: newCorrelationId(),
    ...options.headers,
  };
  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }
  if (accessToken) {
    headers.Authorization = `Bearer ${accessToken}`;
  }

  try {
    return await fetch(url, {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: controller.signal,
    });
  } catch (cause) {
    if (timedOut) {
      throw new ApiClientError({ kind: 'timeout' });
    }
    if (options.signal?.aborted) {
      throw cause; // Caller cancelled; let it propagate as an abort, not a user-facing error.
    }
    throw new ApiClientError({ kind: 'network' });
  } finally {
    clearTimeout(timer);
    options.signal?.removeEventListener('abort', onCallerAbort);
  }
}

async function readBody(response: Response): Promise<unknown> {
  if (response.status === 204) {
    return undefined;
  }
  const text = await response.text();
  if (!text) {
    return undefined;
  }
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return undefined;
  }
}

/**
 * For downloads that bypass apiRequest (e.g. a receipt PDF saved straight to a file): the full URL and
 * the headers to send. Throws the same config error as apiRequest when no API URL is set.
 */
export async function authorizedDownload(path: string): Promise<{ url: string; headers: Record<string, string> }> {
  if (!apiBaseUrl) {
    throw new ApiClientError({ kind: 'config' });
  }
  const token = authHandler ? await authHandler.getAccessToken() : null;
  const headers: Record<string, string> = { [CORRELATION_HEADER]: newCorrelationId() };
  if (token) headers.Authorization = `Bearer ${token}`;
  return { url: `${apiBaseUrl}${path}`, headers };
}

/** A fresh key per payment attempt; retries of the same attempt reuse it so the server records it once. */
export function newIdempotencyKey(): string {
  const random = () => Math.random().toString(16).slice(2, 10);
  return `pay-${Date.now().toString(16)}-${random()}${random()}`;
}

/** Correlation ids only need to be unique enough to find a request in logs; they are not secrets. */
function newCorrelationId(): string {
  const random = Math.random().toString(16).slice(2, 10);
  return `m-${Date.now().toString(16)}-${random}`;
}
