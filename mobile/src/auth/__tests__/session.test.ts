import * as SecureStore from 'expo-secure-store';

import { authApi } from '@/api/auth';
import { apiRequest } from '@/api/client';
import { ApiClientError } from '@/api/errors';
import { authResponse, jsonResponse } from '@/test-utils/fixtures';

import { resetSessionForTests, restoreSession, sessionStore, signIn, signOut } from '../session';

jest.mock('@/api/auth', () => ({
  authApi: { login: jest.fn(), register: jest.fn(), refresh: jest.fn(), logout: jest.fn(), me: jest.fn() },
}));

const api = jest.mocked(authApi);
const secureStore = SecureStore as typeof SecureStore & { __reset: () => void };
const REFRESH_KEY = 'rentapp.refreshToken';
const BASE = 'http://api.test';

describe('session', () => {
  const fetchMock = jest.fn<Promise<Response>, [string, RequestInit]>();

  beforeEach(() => {
    resetSessionForTests();
    secureStore.__reset();
    jest.clearAllMocks();
    api.logout.mockResolvedValue(undefined);
    globalThis.fetch = fetchMock as unknown as typeof fetch;
  });

  it('signs in, keeps only the refresh token in secure storage', async () => {
    api.login.mockResolvedValue(authResponse());

    await signIn('asha@example.test', 'Owner-Pass-123');

    expect(sessionStore.getState()).toMatchObject({ status: 'signedIn', user: { name: 'Asha Owner' } });
    expect(await SecureStore.getItemAsync(REFRESH_KEY)).toMatch(/^refresh-/);
    expect(SecureStore.setItemAsync).toHaveBeenCalledTimes(1);
  });

  it('starts signed out when nothing is saved, without calling the server', async () => {
    await restoreSession();

    expect(sessionStore.getState()).toEqual({ status: 'signedOut' });
    expect(api.refresh).not.toHaveBeenCalled();
  });

  it('resumes a saved session by refreshing', async () => {
    await SecureStore.setItemAsync(REFRESH_KEY, 'saved-token');
    api.refresh.mockResolvedValue(authResponse());

    await restoreSession();

    expect(api.refresh).toHaveBeenCalledWith('saved-token');
    expect(sessionStore.getState().status).toBe('signedIn');
  });

  it('forgets a saved session the server rejects', async () => {
    await SecureStore.setItemAsync(REFRESH_KEY, 'revoked-token');
    api.refresh.mockRejectedValue(new ApiClientError({ kind: 'http', status: 401, code: 'SESSION_EXPIRED' }));

    await restoreSession();

    expect(sessionStore.getState()).toEqual({ status: 'signedOut', reason: 'expired' });
    expect(await SecureStore.getItemAsync(REFRESH_KEY)).toBeNull();
  });

  it('keeps a saved session when the server is unreachable', async () => {
    await SecureStore.setItemAsync(REFRESH_KEY, 'saved-token');
    api.refresh.mockRejectedValue(new ApiClientError({ kind: 'network' }));

    await restoreSession();

    expect(sessionStore.getState()).toEqual({ status: 'unavailable' });
    expect(await SecureStore.getItemAsync(REFRESH_KEY)).toBe('saved-token');
  });

  it('signs out locally and revokes the session on the server', async () => {
    api.login.mockResolvedValue(authResponse());
    await signIn('asha@example.test', 'Owner-Pass-123');
    const refreshToken = await SecureStore.getItemAsync(REFRESH_KEY);

    await signOut();

    expect(sessionStore.getState()).toEqual({ status: 'signedOut', reason: undefined });
    expect(await SecureStore.getItemAsync(REFRESH_KEY)).toBeNull();
    expect(api.logout).toHaveBeenCalledWith(refreshToken);
  });

  it('attaches the access token to authenticated requests', async () => {
    const auth = authResponse();
    api.login.mockResolvedValue(auth);
    await signIn('asha@example.test', 'Owner-Pass-123');
    fetchMock.mockResolvedValue(jsonResponse(200, { ok: true }));

    await apiRequest('/api/v1/auth/me', { baseUrl: BASE });

    expect((fetchMock.mock.calls[0]![1].headers as Record<string, string>).Authorization).toBe(`Bearer ${auth.accessToken}`);
  });

  it('shares one refresh between concurrent requests when the access token has expired', async () => {
    api.login.mockResolvedValue(authResponse({ accessTtlMs: -1_000 }));
    await signIn('asha@example.test', 'Owner-Pass-123');
    let resolveRefresh!: (value: ReturnType<typeof authResponse>) => void;
    api.refresh.mockReturnValue(new Promise((resolve) => (resolveRefresh = resolve)));
    fetchMock.mockImplementation(async () => jsonResponse(200, {}));

    const requests = Promise.all([1, 2, 3].map(() => apiRequest('/x', { baseUrl: BASE })));
    const renewed = authResponse();
    resolveRefresh(renewed);
    await requests;

    expect(api.refresh).toHaveBeenCalledTimes(1);
    for (const [, init] of fetchMock.mock.calls) {
      expect((init.headers as Record<string, string>).Authorization).toBe(`Bearer ${renewed.accessToken}`);
    }
  });

  it('refreshes and retries once when the server answers 401', async () => {
    api.login.mockResolvedValue(authResponse());
    await signIn('asha@example.test', 'Owner-Pass-123');
    const renewed = authResponse();
    api.refresh.mockResolvedValue(renewed);
    fetchMock
      .mockResolvedValueOnce(jsonResponse(401, { code: 'UNAUTHORIZED', message: 'Please sign in to continue.' }))
      .mockResolvedValueOnce(jsonResponse(200, { ok: true }));

    await expect(apiRequest('/x', { baseUrl: BASE })).resolves.toEqual({ ok: true });

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect((fetchMock.mock.calls[1]![1].headers as Record<string, string>).Authorization).toBe(`Bearer ${renewed.accessToken}`);
  });

  it('signs out when the refresh after a 401 is rejected', async () => {
    api.login.mockResolvedValue(authResponse());
    await signIn('asha@example.test', 'Owner-Pass-123');
    api.refresh.mockRejectedValue(new ApiClientError({ kind: 'http', status: 401, code: 'SESSION_EXPIRED' }));
    fetchMock.mockResolvedValue(jsonResponse(401, { code: 'UNAUTHORIZED' }));

    await expect(apiRequest('/x', { baseUrl: BASE })).rejects.toMatchObject({ kind: 'http', status: 401 });

    expect(sessionStore.getState()).toEqual({ status: 'signedOut', reason: 'expired' });
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('does not attach tokens to sign-in requests', async () => {
    api.login.mockResolvedValue(authResponse());
    await signIn('asha@example.test', 'Owner-Pass-123');
    fetchMock.mockResolvedValue(jsonResponse(200, {}));

    await apiRequest('/api/v1/auth/login', { baseUrl: BASE, authenticated: false, method: 'POST', body: {} });

    expect((fetchMock.mock.calls[0]![1].headers as Record<string, string>).Authorization).toBeUndefined();
  });
});
