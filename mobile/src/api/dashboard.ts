import { apiRequest } from './client';
import type { PaymentSummary } from './payments';
import type { OccupancySummary } from './properties';
import type { AmountCount, RentCharge } from './rent';

/** This month's rent: billed, waived, expected (billed − waived) and collected against it. */
export interface MonthProgress {
  month: string;
  billed: number;
  waived: number;
  expected: number;
  collected: number;
  outstanding: number;
  /** Whole percent, rounded down by the server. */
  percentCollected: number;
}

export interface Dashboard {
  today: string;
  generatedAt: string;
  /** Null when the user cannot view properties. */
  occupancy: { properties: number; rooms: number; beds: OccupancySummary } | null;
  /** Null when the user cannot view tenants. */
  rent: {
    collectedThisMonth: AmountCount;
    thisMonth: MonthProgress;
    outstanding: AmountCount;
    overdue: AmountCount;
    dueToday: AmountCount;
    dueThisWeek: AmountCount;
    overdueList: RentCharge[];
    recentPayments: PaymentSummary[];
  } | null;
  /** Dues the reminder queue suggests today. Null when the user cannot send reminders. */
  remindersToSend: number | null;
}

export const dashboardApi = {
  get: (signal?: AbortSignal) => apiRequest<Dashboard>('/api/v1/dashboard', { signal }),
};
