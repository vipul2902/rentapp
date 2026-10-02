import { authApi, type RegisterInput } from '@/api/auth';
import { setAuthHandler } from '@/api/client';
import { ApiClientError } from '@/api/errors';
import type { AuthResponse, UserProfile } from '@/api/types';

import { tokenStorage } from './tokenStorage';

export type SessionState =
  | { status: 'restoring' }
  /** A saved session exists but the server could not be reached to resume it. */
  | { status: 'unavailable' }
  | { status: 'signedOut'; reason?: 'expired' }
  | { status: 'signedIn'; user: UserProfile };

type RefreshOutcome = 'refreshed' | 'rejected' | 'unreachable';

/** Refresh slightly early so a token never expires mid-request. */
const EXPIRY_MARGIN_MS = 30_000;

let state: SessionState = { status: 'restoring' };
let access: { token: string; expiresAt: number } | null = null;
let refreshInFlight: Promise<RefreshOutcome> | null = null;
const listeners = new Set<() => void>();

function setState(next: SessionState): void {
  state = next;
  listeners.forEach((listener) => listener());
}

/** External store consumed by React through useSyncExternalStore. */
export const sessionStore = {
  getState: (): SessionState => state,
  subscribe(listener: () => void): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },
};

export async function signIn(email: string, password: string): Promise<void> {
  await establish(await authApi.login(email, password));
}

export async function register(input: RegisterInput): Promise<void> {
  await establish(await authApi.register(input));
}

/** Called once at app start: resume the saved session, if any. */
export async function restoreSession(): Promise<void> {
  setState({ status: 'restoring' });
  if (!(await tokenStorage.load())) {
    setState({ status: 'signedOut' });
    return;
  }

  const outcome = await refresh();
  if (outcome === 'unreachable') {
    setState({ status: 'unavailable' });
  } else if (outcome === 'rejected') {
    setState({ status: 'signedOut', reason: 'expired' });
  }
}

export async function signOut(): Promise<void> {
  const refreshToken = await tokenStorage.load();
  await endLocally();
  if (refreshToken) {
    // Best effort: the local session is already gone even if the server cannot be reached.
    authApi.logout(refreshToken).catch(() => undefined);
  }
}

async function establish(response: AuthResponse): Promise<void> {
  access = { token: response.accessToken, expiresAt: Date.parse(response.accessTokenExpiresAt) };
  await tokenStorage.save(response.refreshToken);
  setState({ status: 'signedIn', user: response.user });
}

async function endLocally(reason?: 'expired'): Promise<void> {
  access = null;
  await tokenStorage.clear();
  setState({ status: 'signedOut', reason });
}

/**
 * Single-flight: concurrent callers share one refresh. This matters because the server rotates refresh
 * tokens and treats a second use of the same token as theft, revoking the whole session.
 */
function refresh(): Promise<RefreshOutcome> {
  refreshInFlight ??= (async (): Promise<RefreshOutcome> => {
    try {
      const refreshToken = await tokenStorage.load();
      if (!refreshToken) {
        return 'rejected';
      }
      await establish(await authApi.refresh(refreshToken));
      return 'refreshed';
    } catch (error) {
      if (isTransient(error)) {
        return 'unreachable'; // Keep the saved token; the server may simply be unreachable.
      }
      await endLocally('expired');
      return 'rejected';
    } finally {
      refreshInFlight = null;
    }
  })();
  return refreshInFlight;
}

function isTransient(error: unknown): boolean {
  return (
    error instanceof ApiClientError &&
    (error.kind === 'network' || error.kind === 'timeout' || (error.status ?? 0) >= 500 || error.status === 429)
  );
}

setAuthHandler({
  async getAccessToken() {
    if (access && access.expiresAt - EXPIRY_MARGIN_MS > Date.now()) {
      return access.token;
    }
    if (state.status !== 'signedIn') {
      return null;
    }
    const outcome = await refresh();
    if (outcome === 'unreachable') {
      throw new ApiClientError({ kind: 'network' });
    }
    return outcome === 'refreshed' ? (access?.token ?? null) : null;
  },

  async handleUnauthorized() {
    if (state.status !== 'signedIn') {
      return false;
    }
    access = null;
    return (await refresh()) === 'refreshed';
  },
});

/** Test-only: return the module to its initial state. */
export function resetSessionForTests(): void {
  state = { status: 'restoring' };
  access = null;
  refreshInFlight = null;
  listeners.clear();
}
