import * as SecureStore from 'expo-secure-store';
import { router } from 'expo-router';
import { act, fireEvent, renderRouter, screen } from 'expo-router/testing-library';
import { Alert } from 'react-native';

import { authApi } from '@/api/auth';
import { dashboardApi } from '@/api/dashboard';
import { ApiClientError } from '@/api/errors';
import { rentApi } from '@/api/rent';
import { tenantsApi } from '@/api/tenants';
import { resetSessionForTests } from '@/auth/session';
import { authResponse } from '@/test-utils/fixtures';

import TabsLayout from '../app/(app)/(tabs)/_layout';
import AppIndex from '../app/(app)/(tabs)/index';
import MoreTab from '../app/(app)/(tabs)/more';
import PropertiesTab from '../app/(app)/(tabs)/properties';
import RentTab from '../app/(app)/(tabs)/rent';
import TenantsTab from '../app/(app)/(tabs)/tenants';
import AppLayout from '../app/(app)/_layout';
import DeleteAccountRoute from '../app/(app)/account/delete';
import AuthLayout from '../app/(auth)/_layout';
import SignIn from '../app/(auth)/sign-in';
import RootLayout from '../app/_layout';

jest.mock('@/api/auth', () => ({
  authApi: { login: jest.fn(), register: jest.fn(), refresh: jest.fn(), logout: jest.fn(), me: jest.fn(), deleteAccount: jest.fn() },
}));
jest.mock('@/api/dashboard', () => ({ dashboardApi: { get: jest.fn() } }));
jest.mock('@/api/rent', () => ({
  rentApi: { summary: jest.fn(), overdue: jest.fn(), charges: jest.fn(), charge: jest.fn(), generate: jest.fn(), waive: jest.fn() },
}));
jest.mock('@/api/properties', () => ({ propertiesApi: { list: jest.fn() }, roomsApi: {}, bedsApi: {} }));
jest.mock('@/api/tenants', () => ({ tenantsApi: { list: jest.fn() } }));

const auth = jest.mocked(authApi);
const secureStore = SecureStore as typeof SecureStore & { __reset: () => void };
const FIND = { timeout: 5_000 };
const empty = { items: [], page: 1, pageSize: 25, totalCount: 0, totalPages: 0 };

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
  '(app)/account/delete': DeleteAccountRoute,
};

async function openAs(user: NonNullable<Parameters<typeof authResponse>[0]>['user'], go?: () => void) {
  await SecureStore.setItemAsync('rentapp.refreshToken', 'saved');
  auth.refresh.mockResolvedValue(authResponse({ user }));
  await renderRouter(routes, { initialUrl: '/' });
  await screen.findByText(/Hi, /, {}, FIND);
  if (go) await act(go);
}

/** Presses the destructive button of the next confirmation dialog. */
function confirmAlerts() {
  jest.spyOn(Alert, 'alert').mockImplementation((_title, _message, buttons) => {
    buttons?.find((b) => b.style === 'destructive')?.onPress?.();
  });
}

describe('polish', () => {
  beforeEach(() => {
    resetSessionForTests();
    secureStore.__reset();
    jest.clearAllMocks();
    jest.mocked(dashboardApi.get).mockResolvedValue({ today: '2026-10-12', generatedAt: '', occupancy: null, rent: null, remindersToSend: null });
    jest.mocked(rentApi.charges).mockResolvedValue(empty);
    jest.mocked(rentApi.summary).mockResolvedValue({
      today: '2026-10-12', outstanding: { amount: 0, count: 0 }, overdue: { amount: 0, count: 0 }, dueToday: { amount: 0, count: 0 },
      dueThisWeek: { amount: 0, count: 0 }, billedThisMonth: { amount: 0, count: 0 },
    });
    jest.mocked(tenantsApi.list).mockResolvedValue(empty);
    auth.logout.mockResolvedValue(undefined);
  });

  it('the owner can delete their account after confirming with the password', async () => {
    confirmAlerts();
    auth.deleteAccount.mockResolvedValue(undefined);
    await openAs({ role: 'Owner' }, () => router.push('/account/delete'));

    expect(await screen.findByText('This closes your whole business account', {}, FIND)).toBeTruthy();
    await fireEvent.press(screen.getByRole('button', { name: 'Close business and delete account' }));
    expect(await screen.findByText('Enter your password to confirm.')).toBeTruthy();
    expect(auth.deleteAccount).not.toHaveBeenCalled();

    await fireEvent.changeText(screen.getByLabelText('Password'), 'Owner-Pass-123');
    await fireEvent.press(screen.getByRole('button', { name: 'Close business and delete account' }));

    expect(auth.deleteAccount).toHaveBeenCalledWith('Owner-Pass-123');
    expect(await screen.findByRole('button', { name: 'Sign in' }, FIND)).toBeTruthy();
  });

  it('a wrong password shows on the field and keeps the account', async () => {
    confirmAlerts();
    auth.deleteAccount.mockRejectedValue(new ApiClientError({
      kind: 'http', status: 400, code: 'PASSWORD_INCORRECT', serverMessage: 'That password is not correct.',
      fieldErrors: { password: ['That password is not correct.'] },
    }));
    await openAs({ role: 'Staff', name: 'Ravi Staff', permissions: [] }, () => router.push('/account/delete'));

    expect(await screen.findByText('This deletes your account', {}, FIND)).toBeTruthy();
    await fireEvent.changeText(screen.getByLabelText('Password'), 'wrong');
    await fireEvent.press(screen.getByRole('button', { name: 'Delete my account' }));

    expect(await screen.findByText('That password is not correct.', {}, FIND)).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Sign in' })).toBeNull();
  });

  it('rent dues are searched as you type', async () => {
    await openAs({ role: 'Owner' }, () => router.push('/rent'));

    await fireEvent.changeText(await screen.findByLabelText('Search', {}, FIND), '  rahul ');

    expect(await screen.findByText('No rent dues match “rahul”.', {}, FIND)).toBeTruthy();
    expect(rentApi.charges).toHaveBeenLastCalledWith(expect.objectContaining({ search: 'rahul' }), expect.anything());
  });

  it('tenants are searched as you type and the search can be cleared', async () => {
    await openAs({ role: 'Owner' }, () => router.push('/tenants'));

    await fireEvent.changeText(await screen.findByLabelText('Search', {}, FIND), '98765');
    expect(await screen.findByText('No tenant matches “98765”.', {}, FIND)).toBeTruthy();
    await fireEvent.press(screen.getByRole('button', { name: 'Clear search' }));

    expect(await screen.findByText('No tenants yet', {}, FIND)).toBeTruthy();
    // Clearing goes back to the first (cached) result instead of fetching again.
    expect(tenantsApi.list).toHaveBeenCalledTimes(2);
  });
});
