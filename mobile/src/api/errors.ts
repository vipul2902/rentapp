export type ApiErrorKind = 'config' | 'network' | 'timeout' | 'http';

interface ApiClientErrorInit {
  kind: ApiErrorKind;
  status?: number;
  code?: string;
  serverMessage?: string;
  traceId?: string;
  fieldErrors?: Record<string, string[]>;
}

/** Every failure from the API client is normalized to this type, so screens handle one shape. */
export class ApiClientError extends Error {
  readonly kind: ApiErrorKind;
  readonly status?: number;
  readonly code?: string;
  /** Message from the API's error body. The API guarantees these are safe to show to users. */
  readonly serverMessage?: string;
  readonly traceId?: string;
  readonly fieldErrors?: Record<string, string[]>;

  constructor(init: ApiClientErrorInit) {
    super(init.serverMessage ?? init.code ?? init.kind);
    this.name = 'ApiClientError';
    this.kind = init.kind;
    this.status = init.status;
    this.code = init.code;
    this.serverMessage = init.serverMessage;
    this.traceId = init.traceId;
    this.fieldErrors = init.fieldErrors;
  }
}

export interface UserMessage {
  title: string;
  detail: string;
}

/**
 * Human-readable text for any error. `action` describes what the user was doing, e.g. "record payment",
 * giving "Unable to record payment." rather than "HTTP 400".
 */
export function userMessageFor(error: unknown, action = 'complete this action'): UserMessage {
  if (!(error instanceof ApiClientError)) {
    return { title: `Unable to ${action}.`, detail: 'Something unexpected happened. Please try again.' };
  }

  switch (error.kind) {
    case 'config':
      return { title: 'App is not configured.', detail: 'The server address is missing. Please contact support.' };
    case 'network':
      return { title: 'Unable to connect.', detail: 'Please check your internet connection.' };
    case 'timeout':
      return { title: 'The server is taking too long.', detail: 'Please check your connection and try again.' };
    case 'http':
      return httpMessage(error, action);
  }
}

function httpMessage(error: ApiClientError, action: string): UserMessage {
  const title = `Unable to ${action}.`;
  const status = error.status ?? 0;

  if (status === 401) {
    return { title: 'Your session has ended.', detail: 'Please sign in again.' };
  }
  if (status === 403) {
    return { title, detail: 'You do not have permission to do this.' };
  }
  if (status === 429) {
    return { title, detail: 'Too many attempts. Please wait a moment and try again.' };
  }
  if (status >= 500) {
    return { title, detail: 'Something went wrong on our side. Please try again.' };
  }
  return { title, detail: error.serverMessage ?? 'Please check the details and try again.' };
}
