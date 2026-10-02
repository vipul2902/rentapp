import type { AuthResponse, UserProfile } from '@/api/types';

export function userProfile(overrides: Partial<UserProfile> = {}): UserProfile {
  return {
    id: 'user-1',
    name: 'Asha Owner',
    email: 'asha@example.test',
    phone: null,
    role: 'Owner',
    status: 'Active',
    permissions: ['ViewProperties', 'ViewTenants', 'RecordPayments', 'GenerateReceipts', 'SendReminders'],
    organization: { id: 'org-1', name: 'Sunrise PG', timeZone: 'Asia/Kolkata' },
    ...overrides,
  };
}

let counter = 0;

/** A fresh auth response; `accessTtlMs` controls how long the access token stays valid. */
export function authResponse(overrides: { user?: Partial<UserProfile>; accessTtlMs?: number } = {}): AuthResponse {
  counter += 1;
  const now = Date.now();
  return {
    accessToken: `access-${counter}`,
    accessTokenExpiresAt: new Date(now + (overrides.accessTtlMs ?? 15 * 60_000)).toISOString(),
    refreshToken: `refresh-${counter}`,
    refreshTokenExpiresAt: new Date(now + 30 * 86_400_000).toISOString(),
    user: userProfile(overrides.user),
  };
}

export function jsonResponse(status: number, body: unknown): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}
