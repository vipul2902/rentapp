import { apiRequest } from './client';
import type { RentCharge } from './rent';
import type { PagedResult } from './types';

export type ReminderType = 'Upcoming' | 'DueToday' | 'Overdue' | 'LongOverdue';
export type ReminderChannel = 'Copy' | 'Share' | 'WhatsApp' | 'Sms';
export type ReminderStatus = 'Prepared' | 'Sent';

export interface Reminder {
  id: string;
  tenantId: string;
  tenantName: string;
  rentChargeId: string;
  periodStart: string;
  type: ReminderType;
  channel: ReminderChannel;
  status: ReminderStatus;
  message: string;
  createdAt: string;
  sentAt: string | null;
  createdByName: string | null;
}

export interface ReminderSuggestion {
  charge: RentCharge;
  type: ReminderType;
  message: string;
  lastRemindedAt: string | null;
}

export interface ReminderSuggestions {
  today: string;
  upcoming: number;
  dueToday: number;
  overdue: number;
  longOverdue: number;
  items: ReminderSuggestion[];
}

export interface ReminderPreview {
  charge: RentCharge;
  type: ReminderType;
  message: string;
  history: Reminder[];
}

const id = encodeURIComponent;

export const remindersApi = {
  queue: (signal?: AbortSignal) => apiRequest<ReminderSuggestions>('/api/v1/reminders/queue', { signal }),
  preview: (rentChargeId: string, signal?: AbortSignal) =>
    apiRequest<ReminderPreview>(`/api/v1/reminders/preview?rentChargeId=${id(rentChargeId)}`, { signal }),
  create: (input: { rentChargeId: string; channel: ReminderChannel; message?: string }) =>
    apiRequest<Reminder>('/api/v1/reminders', { method: 'POST', body: input }),
  markSent: (reminderId: string) => apiRequest<Reminder>(`/api/v1/reminders/${id(reminderId)}/sent`, { method: 'POST' }),
  list: (params: { page: number; pageSize: number; tenantId?: string }, signal?: AbortSignal) => {
    const query = new URLSearchParams({ page: String(params.page), pageSize: String(params.pageSize) });
    if (params.tenantId) query.set('tenantId', params.tenantId);
    return apiRequest<PagedResult<Reminder>>(`/api/v1/reminders?${query.toString()}`, { signal });
  },
};
