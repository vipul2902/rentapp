import { apiRequest } from './client';
import type { PagedResult } from './types';

export type PaymentMethod = 'Cash' | 'Upi' | 'BankTransfer' | 'Card' | 'Other';
export type PaymentStatus = 'Recorded' | 'Voided';

export const PAYMENT_METHODS: readonly { value: PaymentMethod; label: string }[] = [
  { value: 'Upi', label: 'UPI' },
  { value: 'Cash', label: 'Cash' },
  { value: 'BankTransfer', label: 'Bank transfer' },
  { value: 'Card', label: 'Card' },
  { value: 'Other', label: 'Other' },
];

export const methodLabel = (method: PaymentMethod) => PAYMENT_METHODS.find((m) => m.value === method)?.label ?? method;

export interface PaymentAllocation {
  rentChargeId: string;
  periodStart: string;
  dueDate: string;
  amount: number;
}

export interface Payment {
  id: string;
  tenantId: string;
  tenantName: string;
  paymentDate: string;
  amount: number;
  method: PaymentMethod;
  referenceNumber: string | null;
  notes: string | null;
  status: PaymentStatus;
  recordedByName: string | null;
  createdAt: string;
  voidedAt: string | null;
  voidReason: string | null;
  receiptId: string;
  receiptNumber: string;
  allocations: PaymentAllocation[];
}

export interface PaymentSummary {
  id: string;
  tenantId: string;
  tenantName: string;
  paymentDate: string;
  amount: number;
  method: PaymentMethod;
  status: PaymentStatus;
  receiptId: string;
  receiptNumber: string;
  createdAt: string;
}

/** What the receipt says. A snapshot taken when the payment was recorded; it never changes. */
export interface Receipt {
  id: string;
  paymentId: string;
  receiptNumber: string;
  generatedAt: string;
  issuedOn: string;
  organizationName: string;
  propertyName: string;
  propertyAddress: string;
  propertyContactPhone: string | null;
  tenantName: string;
  tenantPhone: string;
  roomNumber: string;
  bedLabel: string;
  periodLabel: string;
  amount: number;
  paymentDate: string;
  method: PaymentMethod;
  referenceNumber: string | null;
  isVoid: boolean;
}

export interface RecordPaymentInput {
  tenantId: string;
  amount: number;
  paymentDate: string;
  method: PaymentMethod;
  referenceNumber?: string;
  notes?: string;
  chargeIds?: string[];
}

export interface RecordPaymentResult {
  payment: Payment;
  receipt: Receipt;
}

const id = encodeURIComponent;

export const paymentsApi = {
  /** The key makes retries safe: the server returns the original payment instead of recording it twice. */
  record: (input: RecordPaymentInput, idempotencyKey: string) =>
    apiRequest<RecordPaymentResult>('/api/v1/payments', { method: 'POST', body: input, headers: { 'Idempotency-Key': idempotencyKey } }),
  get: (paymentId: string, signal?: AbortSignal) => apiRequest<Payment>(`/api/v1/payments/${id(paymentId)}`, { signal }),
  list: (params: { page: number; pageSize: number; tenantId?: string }, signal?: AbortSignal) => {
    const query = new URLSearchParams({ page: String(params.page), pageSize: String(params.pageSize) });
    if (params.tenantId) query.set('tenantId', params.tenantId);
    return apiRequest<PagedResult<PaymentSummary>>(`/api/v1/payments?${query.toString()}`, { signal });
  },
  void: (paymentId: string, reason: string) =>
    apiRequest<Payment>(`/api/v1/payments/${id(paymentId)}/void`, { method: 'POST', body: { reason } }),
  receipt: (receiptId: string, signal?: AbortSignal) => apiRequest<Receipt>(`/api/v1/receipts/${id(receiptId)}`, { signal }),
  receiptPdfPath: (receiptId: string) => `/api/v1/receipts/${id(receiptId)}/pdf`,
};
