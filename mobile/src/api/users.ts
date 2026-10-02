import { apiRequest } from './client';
import type { PagedResult, StaffMember, StaffPermission } from './types';

export interface CreateStaffInput {
  name: string;
  email: string;
  phone?: string;
  password: string;
  permissions: StaffPermission[];
}

export const usersApi = {
  list: (params: { page: number; pageSize: number; search?: string }, signal?: AbortSignal) => {
    const query = new URLSearchParams({ page: String(params.page), pageSize: String(params.pageSize) });
    if (params.search?.trim()) {
      query.set('search', params.search.trim());
    }
    return apiRequest<PagedResult<StaffMember>>(`/api/v1/users?${query.toString()}`, { signal });
  },

  get: (id: string, signal?: AbortSignal) => apiRequest<StaffMember>(`/api/v1/users/${encodeURIComponent(id)}`, { signal }),

  create: (input: CreateStaffInput) => apiRequest<StaffMember>('/api/v1/users', { method: 'POST', body: input }),

  updatePermissions: (id: string, permissions: StaffPermission[]) =>
    apiRequest<StaffMember>(`/api/v1/users/${encodeURIComponent(id)}/permissions`, { method: 'PUT', body: { permissions } }),

  disable: (id: string) => apiRequest<StaffMember>(`/api/v1/users/${encodeURIComponent(id)}/disable`, { method: 'POST' }),

  enable: (id: string) => apiRequest<StaffMember>(`/api/v1/users/${encodeURIComponent(id)}/enable`, { method: 'POST' }),

  resetPassword: (id: string, newPassword: string) =>
    apiRequest<void>(`/api/v1/users/${encodeURIComponent(id)}/reset-password`, { method: 'POST', body: { newPassword } }),
};
