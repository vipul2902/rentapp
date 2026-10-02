import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { dashboardApi } from '@/api/dashboard';
import { type RentFilter, rentApi, type RentStatus } from '@/api/rent';
import type { StatusTone } from '@/components/StatusPill';
import type { IconName } from '@/components/Icon';

import { tenantKeys } from './useTenants';

const PAGE_SIZE = 25;

export const rentKeys = {
  all: ['rent'] as const,
  list: (filter: RentFilter, tenantId?: string) => ['rent', 'list', filter, tenantId ?? ''] as const,
  charge: (id: string) => ['rent', 'charge', id] as const,
  summary: ['rent', 'summary'] as const,
  overdue: ['rent', 'overdue'] as const,
  /** Under 'rent' so every rent, tenant and payment change refreshes the home screen too. */
  dashboard: ['rent', 'dashboard'] as const,
};

export function useCharges(filter: RentFilter, tenantId?: string) {
  return useInfiniteQuery({
    queryKey: rentKeys.list(filter, tenantId),
    queryFn: ({ pageParam, signal }) => rentApi.charges({ page: pageParam, pageSize: PAGE_SIZE, filter, tenantId }, signal),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
  });
}

export const useCharge = (id: string, enabled = true) =>
  useQuery({ queryKey: rentKeys.charge(id), queryFn: ({ signal }) => rentApi.charge(id, signal), enabled });

export const useRentSummary = (enabled = true) =>
  useQuery({ queryKey: rentKeys.summary, queryFn: ({ signal }) => rentApi.summary(signal), enabled });

/** Everything the home screen shows, in one request (cached briefly on the server). */
export const useDashboard = () => useQuery({ queryKey: rentKeys.dashboard, queryFn: ({ signal }) => dashboardApi.get(signal) });

export const useOverdue = (enabled = true) =>
  useQuery({ queryKey: rentKeys.overdue, queryFn: ({ signal }) => rentApi.overdue(10, signal), enabled });

/** Owner pull-to-refresh: make sure this month's dues exist, then reload everything rent-related. */
export function useGenerateCharges() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => rentApi.generate(),
    onSettled: () => queryClient.invalidateQueries({ queryKey: rentKeys.all }),
  });
}

export function useWaive(chargeId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ amount, reason }: { amount: number; reason: string }) => rentApi.waive(chargeId, amount, reason),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: rentKeys.all }),
        queryClient.invalidateQueries({ queryKey: tenantKeys.all }),
      ]);
    },
  });
}

/** How each rent status looks everywhere in the app. */
export const RENT_STATUS: Record<RentStatus, { label: string; tone: StatusTone; icon: IconName }> = {
  Overdue: { label: 'Overdue', tone: 'danger', icon: 'alert-circle' },
  DueToday: { label: 'Due today', tone: 'warning', icon: 'today' },
  Upcoming: { label: 'Upcoming', tone: 'info', icon: 'time' },
  PartiallyPaid: { label: 'Partially paid', tone: 'warning', icon: 'pie-chart' },
  Paid: { label: 'Paid', tone: 'success', icon: 'checkmark-circle' },
  Waived: { label: 'Waived', tone: 'neutral', icon: 'gift' },
  Cancelled: { label: 'Cancelled', tone: 'neutral', icon: 'close-circle' },
};

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

/** "2026-10-01" → "October 2026". */
export function periodLabel(periodStart: string): string {
  const [year, month] = periodStart.split('-').map(Number);
  return `${MONTHS[(month ?? 1) - 1] ?? ''} ${year ?? ''}`.trim();
}
