import * as Clipboard from 'expo-clipboard';
import * as SecureStore from 'expo-secure-store';
import { router } from 'expo-router';
import { act, fireEvent, renderRouter, screen } from 'expo-router/testing-library';
import { Linking } from 'react-native';

import { authApi } from '@/api/auth';
import { dashboardApi } from '@/api/dashboard';
import { type RentCharge, rentApi } from '@/api/rent';
import { type Reminder, remindersApi } from '@/api/reminders';
import { resetSessionForTests } from '@/auth/session';
import { authResponse } from '@/test-utils/fixtures';

import TabsLayout from '../app/(app)/(tabs)/_layout';
import AppIndex from '../app/(app)/(tabs)/index';
import MoreTab from '../app/(app)/(tabs)/more';
import PropertiesTab from '../app/(app)/(tabs)/properties';
import RentTab from '../app/(app)/(tabs)/rent';
import TenantsTab from '../app/(app)/(tabs)/tenants';
import AppLayout from '../app/(app)/_layout';
import RemindersRoute from '../app/(app)/reminders/index';
import ComposeReminderRoute from '../app/(app)/reminders/new';
import ChargeRoute from '../app/(app)/rent/[id]/index';
import AuthLayout from '../app/(auth)/_layout';
import SignIn from '../app/(auth)/sign-in';
import RootLayout from '../app/_layout';

jest.mock('@/api/auth', () => ({
  authApi: { login: jest.fn(), register: jest.fn(), refresh: jest.fn(), logout: jest.fn(), me: jest.fn() },
}));
jest.mock('@/api/dashboard', () => ({ dashboardApi: { get: jest.fn() } }));
jest.mock('@/api/rent', () => ({
  rentApi: { summary: jest.fn(), overdue: jest.fn(), charges: jest.fn(), charge: jest.fn(), generate: jest.fn(), waive: jest.fn() },
}));
jest.mock('@/api/reminders', () => ({
  remindersApi: { queue: jest.fn(), preview: jest.fn(), create: jest.fn(), markSent: jest.fn(), list: jest.fn() },
}));
jest.mock('@/api/properties', () => ({ propertiesApi: { list: jest.fn() }, roomsApi: {}, bedsApi: {} }));
jest.mock('@/api/tenants', () => ({ tenantsApi: { list: jest.fn() } }));
jest.mock('expo-clipboard', () => ({ setStringAsync: jest.fn(async () => true) }));

const auth = jest.mocked(authApi);
const reminders = jest.mocked(remindersApi);
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
  '(app)/reminders/index': RemindersRoute,
  '(app)/reminders/new': ComposeReminderRoute,
};

const charge: RentCharge = {
  id: 'c1', rentAgreementId: 'a1', tenantId: 't1', tenantName: 'Rahul Sharma', tenantPhone: '98765 43210',
  propertyId: 'p1', propertyName: 'Sunrise PG', roomNumber: '201', bedLabel: 'A',
  periodStart: '2026-10-01', periodEnd: '2026-10-31', dueDate: '2026-10-12',
  amount: 8500, paidAmount: 0, adjustedAmount: 0, balance: 8500, status: 'DueToday', daysOverdue: 0,
};
const MESSAGE = 'Hi Rahul, your rent of ₹8,500 for October 2026 is due today. Please make the payment at your earliest convenience. Thank you! – Sunrise PG';

function reminder(overrides: Partial<Reminder> = {}): Reminder {
  return {
    id: 'r1', tenantId: 't1', tenantName: 'Rahul Sharma', rentChargeId: 'c1', periodStart: '2026-10-01', type: 'DueToday',
    channel: 'WhatsApp', status: 'Sent', message: MESSAGE, createdAt: '2026-10-12T06:30:00Z', sentAt: '2026-10-12T06:30:00Z',
    createdByName: 'Asha Owner', ...overrides,
  };
}

async function openAs(user: NonNullable<Parameters<typeof authResponse>[0]>['user'], go?: () => void) {
  await SecureStore.setItemAsync('rentapp.refreshToken', 'saved');
  auth.refresh.mockResolvedValue(authResponse({ user }));
  await renderRouter(routes, { initialUrl: '/' });
  await screen.findByText(/Hi, /, {}, FIND);
  if (go) await act(go);
}

describe('reminders', () => {
  beforeEach(() => {
    resetSessionForTests();
    secureStore.__reset();
    jest.clearAllMocks();
    jest.spyOn(Linking, 'openURL').mockResolvedValue(true);
    jest.mocked(dashboardApi.get).mockResolvedValue({ today: '2026-10-12', generatedAt: '', occupancy: null, rent: null, remindersToSend: 1 });
    jest.mocked(rentApi.charge).mockResolvedValue({ charge, adjustments: [], payments: [] });
    reminders.queue.mockResolvedValue({
      today: '2026-10-12', upcoming: 0, dueToday: 1, overdue: 0, longOverdue: 0,
      items: [{ charge, type: 'DueToday', message: MESSAGE, lastRemindedAt: null }],
    });
    reminders.preview.mockResolvedValue({ charge, type: 'DueToday', message: MESSAGE, history: [] });
    reminders.create.mockImplementation(async (input) => reminder({ channel: input.channel, status: input.channel === 'Copy' ? 'Prepared' : 'Sent' }));
    reminders.markSent.mockResolvedValue(reminder({ channel: 'Copy' }));
    reminders.list.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
  });

  it('home shows how many reminders are waiting', async () => {
    await openAs({ role: 'Owner' });

    expect(await screen.findByRole('button', { name: 'Reminders (1)' }, FIND)).toBeTruthy();
  });

  it('the queue sends a ready message through WhatsApp click-to-chat and records it', async () => {
    await openAs({ role: 'Owner' }, () => router.push('/reminders'));

    expect(await screen.findByText(MESSAGE, {}, FIND)).toBeTruthy();
    await fireEvent.press(screen.getByRole('button', { name: 'Send reminder by WhatsApp to Rahul Sharma' }));

    expect(Linking.openURL).toHaveBeenCalledWith(`https://wa.me/919876543210?text=${encodeURIComponent(MESSAGE)}`);
    expect(reminders.create).toHaveBeenCalledWith({ rentChargeId: 'c1', channel: 'WhatsApp', message: MESSAGE });
  });

  it('an edited, copied message asks to be marked as sent', async () => {
    await openAs({ role: 'Owner' }, () => router.push({ pathname: '/reminders/new', params: { chargeId: 'c1' } }));

    const field = await screen.findByLabelText('Message', {}, FIND);
    expect(field.props.value).toBe(MESSAGE);
    await fireEvent.changeText(field, 'Hi Rahul, rent reminder for October. Thanks!');
    await fireEvent.press(screen.getByRole('button', { name: 'Copy reminder for Rahul Sharma' }));

    expect(Clipboard.setStringAsync).toHaveBeenCalledWith('Hi Rahul, rent reminder for October. Thanks!');
    expect(reminders.create).toHaveBeenCalledWith({ rentChargeId: 'c1', channel: 'Copy', message: 'Hi Rahul, rent reminder for October. Thanks!' });
    await fireEvent.press(await screen.findByRole('button', { name: 'Mark as sent' }, FIND));
    expect(reminders.markSent).toHaveBeenCalledWith('r1');
  });

  it('history shows what was sent and lets copied reminders be confirmed', async () => {
    reminders.list.mockResolvedValue({ items: [reminder({ channel: 'Copy', status: 'Prepared' })], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 });
    await openAs({ role: 'Owner' }, () => router.push('/reminders'));

    await fireEvent.press(await screen.findByRole('radio', { name: 'History' }, FIND));
    expect(await screen.findByText('Not sent yet', {}, FIND)).toBeTruthy();
    await fireEvent.press(screen.getByRole('button', { name: 'Mark reminder to Rahul Sharma as sent' }));

    expect(reminders.markSent).toHaveBeenCalledWith('r1');
  });

  it('staff without Send reminders do not see the Remind actions', async () => {
    await openAs({ role: 'Staff', name: 'Ravi Staff', permissions: ['ViewTenants'] }, () =>
      router.push({ pathname: '/rent/[id]', params: { id: 'c1' } }));

    expect(await screen.findByLabelText('Balance: ₹8,500', {}, FIND)).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Send reminder' })).toBeNull();
    expect(reminders.queue).not.toHaveBeenCalled();
  });
});
