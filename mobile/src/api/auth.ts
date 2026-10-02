import { apiRequest } from './client';
import type { AuthResponse, UserProfile } from './types';

export interface RegisterInput {
  organizationName: string;
  name: string;
  email: string;
  phone?: string;
  password: string;
}

export const authApi = {
  register: (input: RegisterInput) =>
    apiRequest<AuthResponse>('/api/v1/auth/register', { method: 'POST', body: input, authenticated: false }),

  login: (email: string, password: string) =>
    apiRequest<AuthResponse>('/api/v1/auth/login', { method: 'POST', body: { email, password }, authenticated: false }),

  refresh: (refreshToken: string) =>
    apiRequest<AuthResponse>('/api/v1/auth/refresh', { method: 'POST', body: { refreshToken }, authenticated: false }),

  logout: (refreshToken: string) =>
    apiRequest<void>('/api/v1/auth/logout', { method: 'POST', body: { refreshToken }, authenticated: false, timeoutMs: 5_000 }),

  /** Permanently deletes the signed-in account (for the owner: closes the organization). */
  deleteAccount: (password: string) => apiRequest<void>('/api/v1/auth/delete-account', { method: 'POST', body: { password } }),

  me: (signal?: AbortSignal) => apiRequest<UserProfile>('/api/v1/auth/me', { signal }),
};
