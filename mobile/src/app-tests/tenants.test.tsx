import * as SecureStore from 'expo-secure-store';
import { router } from 'expo-router';
import { act, renderRouter, screen } from 'expo-router/testing-library';

import { authApi } from '@/api/auth';
import { propertiesApi, type Room, roomsApi } from '@/api/properties';
import { type Tenancy, type TenantDetail, tenantsApi } from '@/api/tenants';
import { resetSessionForTests } from '@/auth/session';
import { authResponse } from '@/test-utils/fixtures';

import AppLayout from '../app/(app)/_layout';
import TabsLayout from '../app/(app)/(tabs)/_layout';
import AppIndex from '../app/(app)/(tabs)/index';
import MoreTab from '../app/(app)/(tabs)/more';
import PropertiesTab from '../app/(app)/(tabs)/properties';
import RentTab from '../app/(app)/(tabs)/rent';
import TenantsTab from '../app/(app)/(tabs)/tenants';
import RoomRoute from '../app/(app)/rooms/[id]/index';
import TenantRoute from '../app/(app)/tenants/[id]/index';
import AuthLayout from '../app/(auth)/_layout';
import SignIn from '../app/(auth)/sign-in';
import RootLayout from '../app/_layout';

jest.mock('@/api/auth', () => ({
  authApi: { login: jest.fn(), register: jest.fn(), refresh: jest.fn(), logout: jest.fn(), me: jest.fn() },
}));
jest.mock('@/api/tenants', () => ({ tenantsApi: { list: jest.fn(), get: jest.fn() } }));
jest.mock('@/api/rent', () => ({
  rentApi: {
    summary: jest.fn(async () => ({ today: '2026-10-12', outstanding: { amount: 8500, count: 1 }, overdue: { amount: 8500, count: 1 }, dueToday: { amount: 0, count: 0 }, dueThisWeek: { amount: 0, count: 0 }, billedThisMonth: { amount: 8500, count: 1 } })),
    overdue: jest.fn(async () => ({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 })),
    charges: jest.fn(async () => ({ items: [], page: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
    generate: jest.fn(async () => ({ created: 0 })),
  },
}));
jest.mock('@/api/properties', () => ({ propertiesApi: { list: jest.fn(), rooms: jest.fn() }, roomsApi: { get: jest.fn() }, bedsApi: {} }));

const auth = jest.mocked(authApi);
const tenants = jest.mocked(tenantsApi);
const rooms = jest.mocked(roomsApi);
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
  '(app)/tenants/[id]/index': TenantRoute,
  '(app)/rooms/[id]/index': RoomRoute,
};

const tenancy: Tenancy = {
  id: 'a1',
  propertyId: 'p1',
  propertyName: 'Green Valley PG',
  roomId: 'r1',
  roomNumber: '201',
  bedId: 'b1',
  bedLabel: 'A',
  monthlyRent: 8500,
  securityDeposit: 10000,
  rentDueDay: 5,
  startDate: '2026-09-01',
  endDate: null,
  status: 'Active',
  state: 'Current',
  endReason: null,
};

const rahul: TenantDetail = {
  id: 't1',
  fullName: 'Rahul Sharma',
  phone: '+91 98765 43210',
  email: null,
  status: 'Active',
  currentTenancy: tenancy,
  outstandingAmount: 8500,
  overdueAmount: 8500,
  createdAt: '2026-09-01T00:00:00Z',
  emergencyContactName: null,
  emergencyContactPhone: null,
  permanentAddress: null,
  history: [tenancy],
  updatedAt: '2026-09-01T00:00:00Z',
};

const room201: Room = {
  id: 'r1',
  propertyId: 'p1',
  roomNumber: '201',
  roomType: null,
  capacity: 2,
  status: 'Active',
  occupancy: { totalBeds: 2, occupied: 1, vacant: 1, reserved: 0, unavailable: 0 },
  beds: [
    { id: 'b1', roomId: 'r1', label: 'A', status: 'Available', occupancy: 'Occupied', defaultMonthlyRent: 8500, tenant: { tenantId: 't1', fullName: 'Rahul Sharma', moveInDate: '2026-09-01' } },
    { id: 'b2', roomId: 'r1', label: 'B', status: 'Available', occupancy: 'Vacant', defaultMonthlyRent: 8500, tenant: null },
  ],
};

type Path = '/tenants' | '/tenants/t1' | '/rooms/r1';

async function openAs(user: NonNullable<Parameters<typeof authResponse>[0]>['user'], path?: Path) {
  await SecureStore.setItemAsync('rentapp.refreshToken', 'saved');
  auth.refresh.mockResolvedValue(authResponse({ user }));
  await renderRouter(routes, { initialUrl: '/' });
  await screen.findByText(/Hi, /, {}, FIND);
  if (path === '/tenants') await act(() => router.push('/tenants'));
  if (path === '/tenants/t1') await act(() => router.push({ pathname: '/tenants/[id]', params: { id: 't1' } }));
  if (path === '/rooms/r1') await act(() => router.push({ pathname: '/rooms/[id]', params: { id: 'r1' } }));
}

describe('tenants', () => {
  beforeEach(() => {
    resetSessionForTests();
    secureStore.__reset();
    jest.clearAllMocks();
    tenants.list.mockResolvedValue({ items: [rahul], page: 1, pageSize: 25, totalCount: 1, totalPages: 1 });
    tenants.get.mockResolvedValue(rahul);
    rooms.get.mockResolvedValue(room201);
    jest.mocked(propertiesApi.list).mockResolvedValue({ items: [], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 });
  });

  it('lists current tenants with where they live and their rent', async () => {
    await openAs({ role: 'Owner' }, '/tenants');

    expect(await screen.findByLabelText('Rahul Sharma, Green Valley PG · Room 201 · Bed A', {}, FIND)).toBeTruthy();
    expect(screen.getByText('₹8,500 / month')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Add tenant' })).toBeTruthy();
    expect(tenants.list).toHaveBeenCalledWith(expect.objectContaining({ filter: 'Current' }), expect.anything());
  });

  it('shows the tenancy and owner actions on the tenant page', async () => {
    await openAs({ role: 'Owner' }, '/tenants/t1');

    expect(await screen.findByText('Room 201 · Bed A', {}, FIND)).toBeTruthy();
    expect(screen.getByLabelText('Rent: ₹8,500 / month')).toBeTruthy();
    expect(screen.getByLabelText('Due: 5th of every month')).toBeTruthy();
    expect(screen.getByLabelText('Deposit: ₹10,000')).toBeTruthy();
    expect(screen.getByLabelText('Moved in: 1 Sep 2026')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Move out' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Move to another bed' })).toBeTruthy();
    expect(screen.getByLabelText('Call +91 98765 43210')).toBeTruthy();
  });

  it('lets staff with View tenants look without changing anything', async () => {
    await openAs({ role: 'Staff', name: 'Ravi Staff', permissions: ['ViewTenants'] }, '/tenants/t1');

    expect(await screen.findByText('Room 201 · Bed A', {}, FIND)).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Move out' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Edit details' })).toBeNull();
  });

  it('hides tenants from staff without View tenants', async () => {
    await openAs({ role: 'Staff', name: 'Meena Staff', permissions: ['ViewProperties'] });

    expect(await screen.findByText(/Hi, Meena/, {}, FIND)).toBeTruthy();
    expect(screen.queryByLabelText(/Left to collect/)).toBeNull();
  });

  it('shows who lives in each bed on the room page ("A - Rahul - Occupied")', async () => {
    await openAs({ role: 'Owner' }, '/rooms/r1');

    expect(await screen.findByText('Bed A · Rahul Sharma', {}, FIND)).toBeTruthy();
    // Once in the room's totals, once as bed A's status.
    expect(screen.getAllByText('Occupied')).toHaveLength(2);
    expect(screen.getByText('Bed B')).toBeTruthy();
  });
});
