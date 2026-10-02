import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { type ReminderChannel, remindersApi, type ReminderType } from '@/api/reminders';
import type { IconName } from '@/components/Icon';
import type { StatusTone } from '@/components/StatusPill';
import { deliverReminder } from '@/utils/reminders';

import { rentKeys } from './useRent';

export const reminderKeys = {
  all: ['reminders'] as const,
  queue: ['reminders', 'queue'] as const,
  preview: (chargeId: string) => ['reminders', 'preview', chargeId] as const,
  list: (tenantId?: string) => ['reminders', 'list', tenantId ?? ''] as const,
};

export const useReminderQueue = (enabled = true) =>
  useQuery({ queryKey: reminderKeys.queue, queryFn: ({ signal }) => remindersApi.queue(signal), enabled });

export const useReminderPreview = (chargeId: string) =>
  useQuery({ queryKey: reminderKeys.preview(chargeId), queryFn: ({ signal }) => remindersApi.preview(chargeId, signal) });

export function useReminderHistory(tenantId?: string, enabled = true) {
  return useInfiniteQuery({
    queryKey: reminderKeys.list(tenantId),
    queryFn: ({ pageParam, signal }) => remindersApi.list({ page: pageParam, pageSize: 20, tenantId }, signal),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
    enabled,
  });
}

async function refresh(queryClient: ReturnType<typeof useQueryClient>) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: reminderKeys.all }),
    queryClient.invalidateQueries({ queryKey: rentKeys.dashboard }),
  ]);
}

/**
 * Opens the chosen app (or copies), then records the reminder. If the person backs out of the share sheet,
 * nothing is recorded. Resolves to the recorded reminder, or null.
 */
export function useSendReminder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: { chargeId: string; phone: string; message: string; channel: ReminderChannel }) => {
      if (!(await deliverReminder(input.channel, input.phone, input.message))) return null;
      return remindersApi.create({ rentChargeId: input.chargeId, channel: input.channel, message: input.message });
    },
    onSuccess: () => refresh(queryClient),
  });
}

export function useMarkReminderSent() {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: (reminderId: string) => remindersApi.markSent(reminderId), onSuccess: () => refresh(queryClient) });
}

export const REMINDER_TYPE: Record<ReminderType, { label: string; tone: StatusTone; icon: IconName }> = {
  Upcoming: { label: 'Upcoming', tone: 'info', icon: 'time' },
  DueToday: { label: 'Due today', tone: 'warning', icon: 'today' },
  Overdue: { label: 'Overdue', tone: 'danger', icon: 'alert-circle' },
  LongOverdue: { label: '7+ days late', tone: 'danger', icon: 'flame' },
};

export const CHANNEL_LABEL: Record<ReminderChannel, string> = { Copy: 'Copied', Share: 'Shared', WhatsApp: 'WhatsApp', Sms: 'SMS' };
