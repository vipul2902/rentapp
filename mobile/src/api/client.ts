import { apiBaseUrl } from './config';
import { ApiClientError } from './errors';

export const CORRELATION_HEADER = 'X-Correlation-ID';
const DEFAULT_TIMEOUT_MS = 15_000;

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  body?: unknown;
  /** Cancellation from the caller (TanStack Query passes one to every queryFn). */
  signal?: AbortSignal;
  timeoutMs?: number;
  /** Non-2xx statuses whose body should be returned instead of thrown (e.g. 503 from /health/ready). */
  allowStatuses?: readonly number[];
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
  };
  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  let response: Response;
  try {
    response = await fetch(`${baseUrl}${path}`, {
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

/** Correlation ids only need to be unique enough to find a request in logs; they are not secrets. */
function newCorrelationId(): string {
  const random = Math.random().toString(16).slice(2, 10);
  return `m-${Date.now().toString(16)}-${random}`;
}
