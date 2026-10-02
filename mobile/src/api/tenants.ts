import { apiRequest } from './client';
import type { PagedResult } from './types';

export type TenancyState = 'None' | 'Upcoming' | 'Current' | 'Ended';
export type AgreementStatus = 'Active' | 'Ended';
export type AgreementEndReason = 'MovedOut' | 'Transferred' | 'Cancelled';
export type TenantFilter = 'All' | 'Current' | 'Former' | 'Unassigned' | 'Overdue';

/** A stay in one bed. Dates are calendar dates ("2026-10-02") in the organization's time zone. */
export interface Tenancy {
  id: string;
  propertyId: string;
  propertyName: string;
  roomId: string;
  roomNumber: string;
  bedId: string;
  bedLabel: string;
  monthlyRent: number;
  securityDeposit: number;
  rentDueDay: number;
  startDate: string;
  endDate: string | null;
  status: AgreementStatus;
  state: TenancyState;
  endReason: AgreementEndReason | null;
}

export interface TenantSummary {
  id: string;
  fullName: string;
  phone: string;
  email: string | null;
  status: 'Active' | 'Archived';
  currentTenancy: Tenancy | null;
  /** Rupees still owed / of which past due. */
  outstandingAmount: number;
  overdueAmount: number;
  createdAt: string;
}

export interface TenantDetail extends TenantSummary {
  emergencyContactName: string | null;
  emergencyContactPhone: string | null;
  permanentAddress: string | null;
  history: Tenancy[];
  updatedAt: string;
}

export interface TenantDetailsInput {
  fullName: string;
  phone: string;
  email?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  permanentAddress?: string;
}

export interface MoveInInput {
  bedId: string;
  startDate: string;
  monthlyRent?: number;
  securityDeposit: number;
  rentDueDay: number;
}

export interface TermsInput {
  monthlyRent: number;
  securityDeposit: number;
  rentDueDay: number;
}

const id = encodeURIComponent;

export const tenantsApi = {
  list: (
    params: { page: number; pageSize: number; search?: string; filter?: TenantFilter; propertyId?: string },
    signal?: AbortSignal,
  ) => {
    const query = new URLSearchParams({ page: String(params.page), pageSize: String(params.pageSize) });
    if (params.search?.trim()) query.set('search', params.search.trim());
    if (params.filter && params.filter !== 'All') query.set('filter', params.filter);
    if (params.propertyId) query.set('propertyId', params.propertyId);
    return apiRequest<PagedResult<TenantSummary>>(`/api/v1/tenants?${query.toString()}`, { signal });
  },
  get: (tenantId: string, signal?: AbortSignal) => apiRequest<TenantDetail>(`/api/v1/tenants/${id(tenantId)}`, { signal }),
  create: (input: TenantDetailsInput & { moveIn?: MoveInInput }) =>
    apiRequest<TenantDetail>('/api/v1/tenants', { method: 'POST', body: input }),
  update: (tenantId: string, input: TenantDetailsInput) =>
    apiRequest<TenantDetail>(`/api/v1/tenants/${id(tenantId)}`, { method: 'PUT', body: input }),
  archive: (tenantId: string) => apiRequest<void>(`/api/v1/tenants/${id(tenantId)}`, { method: 'DELETE' }),
  moveIn: (tenantId: string, input: MoveInInput) =>
    apiRequest<TenantDetail>(`/api/v1/tenants/${id(tenantId)}/move-in`, { method: 'POST', body: input }),
  moveOut: (tenantId: string, moveOutDate: string) =>
    apiRequest<TenantDetail>(`/api/v1/tenants/${id(tenantId)}/move-out`, { method: 'POST', body: { moveOutDate } }),
  move: (tenantId: string, input: { bedId: string; moveDate: string; monthlyRent?: number }) =>
    apiRequest<TenantDetail>(`/api/v1/tenants/${id(tenantId)}/move`, { method: 'POST', body: input }),
  updateTerms: (tenantId: string, input: TermsInput) =>
    apiRequest<TenantDetail>(`/api/v1/tenants/${id(tenantId)}/tenancy`, { method: 'PUT', body: input }),
};
