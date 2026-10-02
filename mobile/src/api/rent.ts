import { apiRequest } from './client';
import type { PaymentMethod, PaymentStatus } from './payments';
import type { PagedResult } from './types';

export type RentStatus = 'Upcoming' | 'DueToday' | 'Overdue' | 'PartiallyPaid' | 'Paid' | 'Waived' | 'Cancelled';
export type RentFilter = 'All' | 'Outstanding' | 'Overdue' | 'DueToday' | 'Upcoming' | 'Paid';

/** One month of rent for one tenancy. Amounts are rupees; the app never does arithmetic on them. */
export interface RentCharge {
  id: string;
  rentAgreementId: string;
  tenantId: string;
  tenantName: string;
  tenantPhone: string;
  propertyId: string;
  propertyName: string;
  roomNumber: string;
  bedLabel: string;
  periodStart: string;
  periodEnd: string;
  dueDate: string;
  amount: number;
  paidAmount: number;
  adjustedAmount: number;
  balance: number;
  status: RentStatus;
  daysOverdue: number;
}

export interface ChargePayment {
  paymentId: string;
  paymentDate: string;
  method: PaymentMethod;
  allocatedAmount: number;
  status: PaymentStatus;
  receiptId: string;
  receiptNumber: string;
}

export interface RentChargeDetail {
  charge: RentCharge;
  adjustments: { id: string; amount: number; reason: string; createdAt: string }[];
  payments: ChargePayment[];
}

export interface AmountCount {
  amount: number;
  count: number;
}

export interface RentSummary {
  today: string;
  outstanding: AmountCount;
  overdue: AmountCount;
  dueToday: AmountCount;
  dueThisWeek: AmountCount;
  billedThisMonth: AmountCount;
}

const id = encodeURIComponent;

export const rentApi = {
  charges: (
    params: { page: number; pageSize: number; filter?: RentFilter; tenantId?: string; propertyId?: string },
    signal?: AbortSignal,
  ) => {
    const query = new URLSearchParams({ page: String(params.page), pageSize: String(params.pageSize) });
    if (params.filter && params.filter !== 'All') query.set('filter', params.filter);
    if (params.tenantId) query.set('tenantId', params.tenantId);
    if (params.propertyId) query.set('propertyId', params.propertyId);
    return apiRequest<PagedResult<RentCharge>>(`/api/v1/rent/charges?${query.toString()}`, { signal });
  },
  charge: (chargeId: string, signal?: AbortSignal) => apiRequest<RentChargeDetail>(`/api/v1/rent/charges/${id(chargeId)}`, { signal }),
  summary: (signal?: AbortSignal) => apiRequest<RentSummary>('/api/v1/rent/summary', { signal }),
  overdue: (pageSize: number, signal?: AbortSignal) =>
    apiRequest<PagedResult<RentCharge>>(`/api/v1/rent/overdue?pageSize=${pageSize}`, { signal }),
  generate: () => apiRequest<{ created: number }>('/api/v1/rent/generate', { method: 'POST' }),
  waive: (chargeId: string, amount: number, reason: string) =>
    apiRequest<RentChargeDetail>(`/api/v1/rent/charges/${id(chargeId)}/adjustments`, { method: 'POST', body: { amount, reason } }),
};
