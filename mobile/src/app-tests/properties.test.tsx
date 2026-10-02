import * as SecureStore from 'expo-secure-store';
import { act, renderRouter, screen } from 'expo-router/testing-library';
import { router } from 'expo-router';

import { authApi } from '@/api/auth';
import type { Property, Room } from '@/api/properties';
import { propertiesApi } from '@/api/properties';
import { resetSessionForTests } from '@/auth/session';
import { authResponse } from '@/test-utils/fixtures';

import AppLayout from '../app/(app)/_layout';
import AppIndex from '../app/(app)/index';
import PropertyRoute from '../app/(app)/properties/[id]/index';
import PropertyIndex from '../app/(app)/properties/index';
import AuthLayout from '../app/(auth)/_layout';
import SignIn from '../app/(auth)/sign-in';
import RootLayout from '../app/_layout';

jest.mock('@/api/auth', () => ({
  authApi: { login: jest.fn(), register: jest.fn(), refresh: jest.fn(), logout: jest.fn(), me: jest.fn() },
}));
jest.mock('@/api/properties', () => ({
  propertiesApi: { list: jest.fn(), get: jest.fn(), rooms: jest.fn() },
  roomsApi: {},
  bedsApi: {},
}));

const auth = jest.mocked(authApi);
const properties = jest.mocked(propertiesApi);
const secureStore = SecureStore as typeof SecureStore & { __reset: () => void };
const FIND = { timeout: 5_000 };

const routes = {
  _layout: RootLayout,
  '(auth)/_layout': AuthLayout,
  '(auth)/sign-in': SignIn,
  '(app)/_layout': AppLayout,
  '(app)/index': AppIndex,
  '(app)/properties/index': PropertyIndex,
  '(app)/properties/[id]/index': PropertyRoute,
};

const occupancy = { totalBeds: 3, occupied: 0, vacant: 2, reserved: 1, unavailable: 0 };

const sunrise: Property = {
  id: 'p1',
  name: 'Green Valley PG',
  address: '12, 5th Cross',
  city: 'Bengaluru',
  state: 'Karnataka',
  postalCode: '560034',
  contactPhone: null,
  status: 'Active',
  roomCount: 1,
  occupancy,
  createdAt: '2026-10-01T00:00:00Z',
  updatedAt: '2026-10-01T00:00:00Z',
};

const room201: Room = {
  id: 'r1',
  propertyId: 'p1',
  roomNumber: '201',
  roomType: 'AC',
  capacity: 3,
  status: 'Active',
  occupancy,
  beds: [
    { id: 'b1', roomId: 'r1', label: 'A', status: 'Available', occupancy: 'Vacant', defaultMonthlyRent: 8500, tenant: null },
    { id: 'b2', roomId: 'r1', label: 'B', status: 'Reserved', occupancy: 'Reserved', defaultMonthlyRent: 8500, tenant: null },
    { id: 'b3', roomId: 'r1', label: 'C', status: 'Available', occupancy: 'Vacant', defaultMonthlyRent: null, tenant: null },
  ],
};

/**
 * Deep links are applied before the saved session is restored (signed-in routes are still guarded),
 * so tests resume the session on Home first and then navigate, as a user would.
 */
async function openAs(user: NonNullable<Parameters<typeof authResponse>[0]>['user'], path?: '/properties' | '/properties/p1') {
  await SecureStore.setItemAsync('rentapp.refreshToken', 'saved');
  auth.refresh.mockResolvedValue(authResponse({ user }));
  await renderRouter(routes, { initialUrl: '/' });
  await screen.findByText('Sign out', {}, FIND);
  if (path === '/properties') {
    await act(() => router.push('/properties'));
  } else if (path) {
    await act(() => router.push({ pathname: '/properties/[id]', params: { id: 'p1' } }));
  }
}

describe('properties', () => {
  beforeEach(() => {
    resetSessionForTests();
    secureStore.__reset();
    jest.clearAllMocks();
    properties.list.mockResolvedValue({ items: [sunrise], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 });
    properties.get.mockResolvedValue(sunrise);
    properties.rooms.mockResolvedValue([room201]);
  });

  it('lists properties with their occupancy for the owner', async () => {
    await openAs({ role: 'Owner' }, '/properties');

    expect(await screen.findByLabelText('Green Valley PG, Bengaluru, 2 vacant of 3 beds', {}, FIND)).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Add property' })).toBeTruthy();
  });

  it('shows rooms and beds in the spec layout: "A · Vacant"', async () => {
    await openAs({ role: 'Owner' }, '/properties/p1');

    expect(await screen.findByText('Room 201', {}, FIND)).toBeTruthy();
    expect(screen.getByText('Bed A')).toBeTruthy();
    expect(screen.getAllByText('Vacant').length).toBeGreaterThanOrEqual(2);
    expect(screen.getAllByText('Reserved').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByRole('button', { name: 'Add room' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Archive property' })).toBeTruthy();
  });

  it('lets staff with View properties look but not change anything', async () => {
    await openAs({ role: 'Staff', name: 'Ravi Staff', permissions: ['ViewProperties'] }, '/properties/p1');

    expect(await screen.findByText('Room 201', {}, FIND)).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Add room' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Edit details' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Archive property' })).toBeNull();
  });

  it('hides properties from staff without View properties', async () => {
    await openAs({ role: 'Staff', name: 'Meena Staff', permissions: ['RecordPayments'] });

    expect(await screen.findByText('Hi, Meena', {}, FIND)).toBeTruthy();
    expect(screen.queryByLabelText('Properties, PGs, rooms, beds and vacancies')).toBeNull();

    await act(() => router.push('/properties'));
    expect(properties.list).not.toHaveBeenCalled();
  });
});
