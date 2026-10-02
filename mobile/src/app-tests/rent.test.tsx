import * as SecureStore from 'expo-secure-store';
import { router } from 'expo-router';
import { act, fireEvent, renderRouter, screen } from 'expo-router/testing-library';

import { authApi } from '@/api/auth';
import { type Dashboard, dashboardApi } from '@/api/dashboard';
import { propertiesApi } from '@/api/properties';
import { type RentCharge, rentApi } from '@/api/rent';
import { resetSessionForTests } from '@/auth/session';
import { authResponse } from '@/test-utils/fixtures';

import TabsLayout from '../app/(app)/(tabs)/_layout';
import AppIndex from '../app/(app)/(tabs)/index';
import MoreTab from '../app/(app)/(tabs)/more';
import PropertiesTab from '../app/(app)/(tabs)/properties';
import RentTab from '../app/(app)/(tabs)/rent';
import TenantsTab from '../app/(app)/(tabs)/tenants';
import AppLayout from '../app/(app)/_layout';
import ChargeRoute from '../app/(app)/rent/[id]/index';
import WaiveRoute from '../app/(app)/rent/[id]/waive';
import AuthLayout from '../app/(auth)/_layout';
import SignIn from '../app/(auth)/sign-in';
import RootLayout from '../app/_layout';

jest.mock('@/api/auth', () => ({
  authApi: { login: jest.fn(), register: jest.fn(), refresh: jest.fn(), logout: jest.fn(), me: jest.fn() },
}));
jest.mock('@/api/rent', () => ({
  rentApi: { summary: jest.fn(), overdue: jest.fn(), charges: jest.fn(), charge: jest.fn(), generate: jest.fn(), waive: jest.fn() },
}));
jest.mock('@/api/dashboard', () => ({ dashboardApi: { get: jest.fn() } }));
jest.mock('@/api/properties', () => ({ propertiesApi: { list: jest.fn() }, roomsApi: {}, bedsApi: {} }));
jest.mock('@/api/tenants', () => ({ tenantsApi: { list: jest.fn() } }));

const auth = jest.mocked(authApi);
const rent = jest.mocked(rentApi);
const secureStore = SecureStore as typeof SecureStore & { __reset: () => void };
const FIND = { timeout: 5_000 };

const routes = {
  _layout: RootLayout,
  '(auth)/_layout': AuthLayout,
  '(auth)/sign-in': SignIn,
  '(app)/_layout': AppLayout,
  '(app)/(tabs)/_layout': TabsLayout,
  '(app)/(tabs)/index': AppIndex,
  '(app)/(tabs)/rent': RentTab,
  '(app)/(tabs)/tenants': TenantsTab,
  '(app)/(tabs)/properties': PropertiesTab,
  '(app)/(tabs)/more': MoreTab,
  '(app)/rent/[id]/index': ChargeRoute,
  '(app)/rent/[id]/waive': WaiveRoute,
};

const overdueCharge: RentCharge = {
  id: 'c1',
  rentAgreementId: 'a1',
  tenantId: 't1',
  tenantName: 'Rahul Sharma',
  tenantPhone: '+91 98765 43210',
  propertyId: 'p1',
  propertyName: 'Green Valley PG',
  roomNumber: '201',
  bedLabel: 'A',
  periodStart: '2026-10-01',
  periodEnd: '2026-10-31',
  dueDate: '2026-10-05',
  amount: 8500,
  paidAmount: 0,
  adjustedAmount: 0,
  balance: 8500,
  status: 'Overdue',
  daysOverdue: 7,
};

function dashboard(overrides: Partial<Dashboard> = {}): Dashboard {
  return {
    today: '2026-10-12',
    generatedAt: '2026-10-12T06:30:00Z',
    occupancy: { properties: 1, rooms: 1, beds: { totalBeds: 3, occupied: 1, vacant: 2, reserved: 0, unavailable: 0 } },
    rent: {
      collectedThisMonth: { amount: 14000, count: 2 },
      thisMonth: { month: '2026-10-01', billed: 17000, waived: 0, expected: 17000, collected: 2000, outstanding: 15000, percentCollected: 11 },
      outstanding: { amount: 25500, count: 3 },
      overdue: { amount: 8500, count: 1 },
      dueToday: { amount: 8500, count: 1 },
      dueThisWeek: { amount: 17000, count: 2 },
      overdueList: [overdueCharge],
      recentPayments: [{
        id: 'pay1', tenantId: 't1', tenantName: 'Rahul Sharma', paymentDate: '2026-10-10', amount: 12000, method: 'Upi',
        status: 'Recorded', receiptId: 'rc1', receiptNumber: 'REC-2026-000001', createdAt: '',
      }],
    },
    ...overrides,
  };
}

const page = <T,>(items: T[]) => ({ items, page: 1, pageSize: 25, totalCount: items.length, totalPages: items.length ? 1 : 0 });

async function openAs(user: NonNullable<Parameters<typeof authResponse>[0]>['user'], go?: () => void) {
  await SecureStore.setItemAsync('rentapp.refreshToken', 'saved');
  auth.refresh.mockResolvedValue(authResponse({ user }));
  await renderRouter(routes, { initialUrl: '/' });
  await screen.findByText(/Hi, /, {}, FIND);
  if (go) await act(go);
}

describe('rent', () => {
  beforeEach(() => {
    resetSessionForTests();
    secureStore.__reset();
    jest.clearAllMocks();
    rent.summary.mockResolvedValue({
      today: '2026-10-12',
      outstanding: { amount: 25500, count: 3 },
      overdue: { amount: 8500, count: 1 },
      dueToday: { amount: 8500, count: 1 },
      dueThisWeek: { amount: 17000, count: 2 },
      billedThisMonth: { amount: 25500, count: 3 },
    });
    rent.overdue.mockResolvedValue(page([overdueCharge]));
    rent.charges.mockResolvedValue(page([overdueCharge]));
    rent.charge.mockResolvedValue({ charge: overdueCharge, adjustments: [], payments: [] });
    rent.generate.mockResolvedValue({ created: 0 });
    jest.mocked(dashboardApi.get).mockResolvedValue(dashboard());
    jest.mocked(propertiesApi.list).mockResolvedValue({
      items: [{
        id: 'p1', name: 'Green Valley PG', address: 'x', city: 'Bengaluru', state: null, postalCode: null, contactPhone: null,
        status: 'Active', roomCount: 1, occupancy: { totalBeds: 3, occupied: 1, vacant: 2, reserved: 0, unavailable: 0 },
        createdAt: '', updatedAt: '',
      }],
      page: 1, pageSize: 20, totalCount: 1, totalPages: 1,
    });
  });

  it('home shows what came in, how much of this month is collected, who is overdue and how full the beds are', async () => {
    await openAs({ role: 'Owner' });

    expect(await screen.findByLabelText('Collected in October: ₹14,000', {}, FIND)).toBeTruthy();
    expect(screen.getByLabelText('11% of October rent collected: ₹2,000 of ₹17,000')).toBeTruthy();
    expect(screen.getByLabelText('Left to collect: ₹25,500')).toBeTruthy();
    expect(screen.getByLabelText('Rahul Sharma, ₹8,500 overdue, 7 days late')).toBeTruthy();
    expect(screen.getByLabelText('Call Rahul Sharma')).toBeTruthy();
    expect(screen.getByLabelText('Record payment from Rahul Sharma')).toBeTruthy();
    expect(screen.getByLabelText('Beds occupied: 1/3')).toBeTruthy();
    expect(screen.getByLabelText('Due this week: ₹17,000')).toBeTruthy();
    expect(screen.getByText('₹12,000 · UPI')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Record payment' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Add tenant' })).toBeTruthy();
    expect(dashboardApi.get).toHaveBeenCalledTimes(1);
    expect(rent.summary).not.toHaveBeenCalled();
  });

  it('home shows staff only the sections the server sent', async () => {
    jest.mocked(dashboardApi.get).mockResolvedValue(dashboard({ rent: null }));
    await openAs({ role: 'Staff', name: 'Meena Staff', permissions: ['ViewProperties'] });

    expect(await screen.findByLabelText('Beds occupied: 1/3', {}, FIND)).toBeTruthy();
    expect(screen.queryByLabelText(/Collected in/)).toBeNull();
    expect(screen.queryByRole('button', { name: 'Record payment' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Add tenant' })).toBeNull();
  });

  it('the rent tab lists dues with status and days late', async () => {
    await openAs({ role: 'Owner' }, () => router.push('/rent'));

    expect(await screen.findByLabelText('Rahul Sharma, October 2026, ₹8,500, Overdue', {}, FIND)).toBeTruthy();
    expect(screen.getByText('7 days late')).toBeTruthy();
    expect(screen.getByLabelText('Overdue, 1')).toBeTruthy();
    expect(rent.charges).toHaveBeenCalledWith(expect.objectContaining({ filter: 'Outstanding' }), expect.anything());
  });

  it('a rent due shows its breakdown, and owners can waive', async () => {
    await openAs({ role: 'Owner' }, () => router.push({ pathname: '/rent/[id]', params: { id: 'c1' } }));

    expect(await screen.findByLabelText('Balance: ₹8,500', {}, FIND)).toBeTruthy();
    expect(screen.getByLabelText('Due on: 5 Oct 2026')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Waive an amount' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Record payment of ₹8,500' })).toBeTruthy();
  });

  it('the waive form needs a reason before calling the server', async () => {
    await openAs({ role: 'Owner' }, () => router.push({ pathname: '/rent/[id]/waive', params: { id: 'c1' } }));

    await fireEvent.press(await screen.findByRole('button', { name: 'Waive amount' }, FIND));

    expect(await screen.findByText('Enter a reason, e.g. "Moved in on the 20th".')).toBeTruthy();
    expect(rent.waive).not.toHaveBeenCalled();
  });

  it('staff can see dues but not waive them', async () => {
    await openAs({ role: 'Staff', name: 'Ravi Staff', permissions: ['ViewTenants'] }, () =>
      router.push({ pathname: '/rent/[id]', params: { id: 'c1' } }));

    expect(await screen.findByLabelText('Balance: ₹8,500', {}, FIND)).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Waive an amount' })).toBeNull();
  });

  it('staff without View tenants never load rent data', async () => {
    // The server leaves out money figures for this user.
    jest.mocked(dashboardApi.get).mockResolvedValue(dashboard({ rent: null }));
    await openAs({ role: 'Staff', name: 'Meena Staff', permissions: ['ViewProperties'] }, () => router.push('/rent'));

    expect(screen.queryByLabelText(/Left to collect/)).toBeNull();
    expect(rent.summary).not.toHaveBeenCalled();
    expect(rent.charges).not.toHaveBeenCalled();
  });
});
