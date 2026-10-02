import { QueryClient } from '@tanstack/react-query';

import { ApiClientError } from './errors';

const MAX_RETRIES = 2;

/** Retry only transient failures; a 4xx will not succeed on retry. */
export function shouldRetry(failureCount: number, error: unknown): boolean {
  if (failureCount >= MAX_RETRIES) {
    return false;
  }
  if (error instanceof ApiClientError) {
    return error.kind === 'network' || error.kind === 'timeout' || (error.status ?? 0) >= 500;
  }
  return false;
}

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: shouldRetry,
        staleTime: 30_000,
      },
      mutations: {
        // Never auto-retry writes (e.g. recording a payment); the user decides.
        retry: false,
      },
    },
  });
}
