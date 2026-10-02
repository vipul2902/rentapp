import * as SecureStore from 'expo-secure-store';
import * as Sharing from 'expo-sharing';
import { router } from 'expo-router';
import { act, fireEvent, renderRouter, screen } from 'expo-router/testing-library';

import { authApi } from '@/api/auth';
import { dashboardApi } from '@/api/dashboard';
import { type Payment, paymentsApi, type Receipt } from '@/api/payments';
import { propertiesApi } from '@/api/properties';
import { type RentCharge, rentApi } from '@/api/rent';
import { type TenantDetail, tenantsApi } from '@/api/tenants';
import { resetSessionForTests } from '@/auth/session';
import { authResponse } from '@/test-utils/fixtures';

import TabsLayout from '../app/(app)/(tabs)/_layout';
import AppIndex from '../app/(app)/(tabs)/index';
import MoreTab from '../app/(app)/(tabs)/more';
import PropertiesTab from '../app/(app)/(tabs)/properties';
import RentTab from '../app/(app)/(tabs)/rent';
import TenantsTab from '../app/(app)/(tabs)/tenants';
import AppLayout from '../app/(app)/_layout';
import PaymentRoute from '../app/(app)/payments/[id]/index';
import VoidPaymentRoute from '../app/(app)/payments/[id]/void';
import RecordPaymentRoute from '../app/(app)/payments/new';
import ReceiptRoute from '../app/(app)/receipts/[id]';
import ChargeRoute from '../app/(app)/rent/[id]/index';
import TenantRoute from '../app/(app)/tenants/[id]/index';
import AuthLayout from '../app/(auth)/_layout';
import SignIn from '../app/(auth)/sign-in';
import RootLayout from '../app/_layout';

jest.mock('@/api/auth', () => ({
  authApi: { login: jest.fn(), register: jest.fn(), refresh: jest.fn(), logout: jest.fn(), me: jest.fn() },
}));
jest.mock('@/api/rent', () => ({
  rentApi: { summary: jest.fn(), overdue: jest.fn(), charges: jest.fn(), charge: jest.fn(), generate: jest.fn(), waive: jest.fn() },
}));
jest.mock('@/api/payments', () => ({
  ...jest.requireActual('@/api/payments'),
  paymentsApi: { record: jest.fn(), get: jest.fn(), list: jest.fn(), void: jest.fn(), receipt: jest.fn(), receiptPdfPath: (id: string) => `/api/v1/receipts/${id}/pdf` },
}));
jest.mock('@/api/dashboard', () => ({ dashboardApi: { get: jest.fn() } }));
jest.mock('@/api/properties', () => ({ propertiesApi: { list: jest.fn() }, roomsApi: {}, bedsApi: {} }));
jest.mock('@/api/tenants', () => ({ tenantsApi: { list: jest.fn(), get: jest.fn() } }));
jest.mock('@/api/config', () => ({ apiBaseUrl: 'https://api.test' }));
jest.mock('expo-sharing', () => ({ isAvailableAsync: jest.fn(async () => true), shareAsync: jest.fn(async () => undefined) }));
jest.mock('expo-file-system', () => {
  class File {
    uri: string;
    constructor(...parts: unknown[]) {
      this.uri = `file:///cache/${String(parts[parts.length - 1])}`;
    }
    static downloadFileAsync = jest.fn(async (_url: string, destination: File) => destination);
  }
  return { File, Paths: { cache: 'cache' } };
});

const auth = jest.mocked(authApi);
const rent = jest.mocked(rentApi);
const payments = jest.mocked(paymentsApi);
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
  '(app)/tenants/[id]/index': TenantRoute,
  '(app)/payments/new': RecordPaymentRoute,
  '(app)/payments/[id]/index': PaymentRoute,
  '(app)/payments/[id]/void': VoidPaymentRoute,
  '(app)/receipts/[id]': ReceiptRoute,
};

const charge: RentCharge = {
  id: 'c1', rentAgreementId: 'a1', tenantId: 't1', tenantName: 'Rahul Sharma', tenantPhone: '+91 98765 43210',
  propertyId: 'p1', propertyName: 'Green Valley PG', roomNumber: '201', bedLabel: 'A',
  periodStart: '2026-10-01', periodEnd: '2026-10-31', dueDate: '2026-10-05',
  amount: 8500, paidAmount: 0, adjustedAmount: 0, balance: 8500, status: 'Overdue', daysOverdue: 7,
};

const tenant: TenantDetail = {
  id: 't1', fullName: 'Rahul Sharma', phone: '+91 98765 43210', email: null, status: 'Active',
  currentTenancy: {
    id: 'a1', propertyId: 'p1', propertyName: 'Green Valley PG', roomId: 'r1', roomNumber: '201', bedId: 'b1', bedLabel: 'A',
    monthlyRent: 8500, securityDeposit: 0, rentDueDay: 5, startDate: '2026-08-20', endDate: null, status: 'Active', state: 'Current', endReason: null,
  },
  outstandingAmount: 25500, overdueAmount: 25500, createdAt: '', emergencyContactName: null, emergencyContactPhone: null,
  permanentAddress: null, history: [], updatedAt: '',
};

const receipt: Receipt = {
  id: 'rc1', paymentId: 'pay1', receiptNumber: 'REC-2026-000123', generatedAt: '', issuedOn: '2026-10-12',
  organizationName: 'Sunrise Living', propertyName: 'Green Valley PG', propertyAddress: '14 MG Road, Bengaluru', propertyContactPhone: null,
  tenantName: 'Rahul Sharma', tenantPhone: '+91 98765 43210', roomNumber: '201', bedLabel: 'A', periodLabel: 'October 2026',
  amount: 8500, paymentDate: '2026-10-12', method: 'Upi', referenceNumber: 'UPI-1', isVoid: false,
};

const payment: Payment = {
  id: 'pay1', tenantId: 't1', tenantName: 'Rahul Sharma', paymentDate: '2026-10-12', amount: 8500, method: 'Upi', referenceNumber: 'UPI-1',
  notes: null, status: 'Recorded', recordedByName: 'Asha Owner', createdAt: '', voidedAt: null, voidReason: null,
  receiptId: 'rc1', receiptNumber: 'REC-2026-000123', allocations: [{ rentChargeId: 'c1', periodStart: '2026-10-01', dueDate: '2026-10-05', amount: 8500 }],
};

const page = <T,>(items: T[]) => ({ items, page: 1, pageSize: 25, totalCount: items.length, totalPages: items.length ? 1 : 0 });

async function openAs(user: NonNullable<Parameters<typeof authResponse>[0]>['user'], go?: () => void) {
  await SecureStore.setItemAsync('rentapp.refreshToken', 'saved');
  auth.refresh.mockResolvedValue(authResponse({ user }));
  await renderRouter(routes, { initialUrl: '/' });
  await screen.findByText(/Hi, /, {}, FIND);
  if (go) await act(go);
}

describe('payments', () => {
  beforeEach(() => {
    resetSessionForTests();
    secureStore.__reset();
    jest.clearAllMocks();
    rent.summary.mockResolvedValue({
      today: '2026-10-12', outstanding: { amount: 25500, count: 3 }, overdue: { amount: 25500, count: 3 },
      dueToday: { amount: 0, count: 0 }, dueThisWeek: { amount: 0, count: 0 }, billedThisMonth: { amount: 8500, count: 1 },
    });
    rent.overdue.mockResolvedValue(page([charge]));
    rent.charges.mockResolvedValue(page([charge]));
    rent.charge.mockResolvedValue({ charge, adjustments: [], payments: [] });
    rent.generate.mockResolvedValue({ created: 0 });
    jest.mocked(dashboardApi.get).mockResolvedValue({ today: '2026-10-12', generatedAt: '', occupancy: null, rent: null, remindersToSend: null });
    jest.mocked(tenantsApi.get).mockResolvedValue(tenant);
    jest.mocked(propertiesApi.list).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
    payments.record.mockResolvedValue({ payment, receipt });
    payments.receipt.mockResolvedValue(receipt);
    payments.get.mockResolvedValue(payment);
    payments.list.mockResolvedValue(page([]));
    payments.void.mockResolvedValue({ ...payment, status: 'Voided', voidReason: 'Entered twice' });
  });

  it('records a payment for a due in a few taps and celebrates with the receipt', async () => {
    await openAs({ role: 'Owner' }, () => router.push({ pathname: '/rent/[id]', params: { id: 'c1' } }));

    await fireEvent.press(await screen.findByRole('button', { name: 'Record payment of ₹8,500' }, FIND));
    // Prefilled with the due's balance; UPI is preselected.
    expect((await screen.findByLabelText('Amount received', {}, FIND)).props.value).toBe('8500');
    expect(screen.getByRole('tab', { name: 'UPI' }).props.accessibilityState).toEqual(expect.objectContaining({ selected: true }));

    await fireEvent.press(screen.getByRole('tab', { name: 'Cash' }));
    await fireEvent.press(screen.getByRole('button', { name: 'Record ₹8,500' }));

    expect(payments.record).toHaveBeenCalledWith(
      expect.objectContaining({ tenantId: 't1', amount: 8500, method: 'Cash', chargeIds: ['c1'] }),
      expect.stringMatching(/^pay-/),
    );
    expect(await screen.findByText('Payment recorded successfully', {}, FIND)).toBeTruthy();
    expect(screen.getByText('Receipt #REC-2026-000123')).toBeTruthy();
    expect(screen.getByLabelText('Rent period: October 2026')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Done' })).toBeTruthy();
  });

  it('the amount must be valid before anything is sent', async () => {
    await openAs({ role: 'Owner' }, () => router.push({ pathname: '/payments/new', params: { tenantId: 't1' } }));

    // From the tenant (no specific due) the whole outstanding amount is suggested.
    const amount = await screen.findByLabelText('Amount received', {}, FIND);
    expect(amount.props.value).toBe('25500');
    await fireEvent.changeText(amount, '12.345');
    await fireEvent.press(screen.getByRole('button', { name: 'Record payment' }));

    expect(await screen.findByText('Enter an amount like 8500 or 8500.50.')).toBeTruthy();
    expect(payments.record).not.toHaveBeenCalled();
  });

  it('a retry after a failure reuses the same idempotency key', async () => {
    payments.record.mockRejectedValueOnce(new Error('timeout'));
    await openAs({ role: 'Owner' }, () => router.push({ pathname: '/payments/new', params: { tenantId: 't1' } }));

    const save = await screen.findByRole('button', { name: 'Record ₹25,500' }, FIND);
    await fireEvent.press(save);
    expect(await screen.findByText('Unable to record the payment.', {}, FIND)).toBeTruthy();
    await fireEvent.press(screen.getByRole('button', { name: 'Record ₹25,500' }));

    expect(await screen.findByText('Payment recorded successfully', {}, FIND)).toBeTruthy();
    const [first, second] = payments.record.mock.calls;
    expect(first?.[1]).toBe(second?.[1]);
  });

  it('shares the receipt as a PDF downloaded with the session token', async () => {
    await openAs({ role: 'Owner' }, () => router.push({ pathname: '/receipts/[id]', params: { id: 'rc1' } }));

    await fireEvent.press(await screen.findByRole('button', { name: 'Share receipt (PDF)' }, FIND));

    const { File } = jest.requireMock<{ File: { downloadFileAsync: jest.Mock } }>('expo-file-system');
    expect(File.downloadFileAsync).toHaveBeenCalledWith(
      'https://api.test/api/v1/receipts/rc1/pdf',
      expect.anything(),
      expect.objectContaining({ headers: expect.objectContaining({ Authorization: expect.stringMatching(/^Bearer /) }) }),
    );
    expect(Sharing.shareAsync).toHaveBeenCalledWith('file:///cache/REC-2026-000123.pdf', expect.objectContaining({ mimeType: 'application/pdf' }));
  });

  it('owners can void a payment with a reason; the receipt then shows VOID', async () => {
    await openAs({ role: 'Owner' }, () => router.push({ pathname: '/payments/[id]', params: { id: 'pay1' } }));

    await fireEvent.press(await screen.findByRole('button', { name: 'Void this payment' }, FIND));
    await fireEvent.press(await screen.findByRole('button', { name: 'Void payment' }, FIND));
    expect(await screen.findByText('Enter why, e.g. "Entered twice".')).toBeTruthy();
    expect(payments.void).not.toHaveBeenCalled();

    await fireEvent.changeText(screen.getByLabelText('Reason'), 'Entered twice');
    await fireEvent.press(screen.getByRole('button', { name: 'Void payment' }));
    expect(payments.void).toHaveBeenCalledWith('pay1', 'Entered twice');

    payments.receipt.mockResolvedValue({ ...receipt, isVoid: true });
    await act(() => router.push({ pathname: '/receipts/[id]', params: { id: 'rc1' } }));
    expect(await screen.findByText('VOID: this payment was reversed. The receipt is no longer valid.', {}, FIND)).toBeTruthy();
  });

  it('staff without Record payments do not see the button, and cannot void', async () => {
    await openAs({ role: 'Staff', name: 'Ravi Staff', permissions: ['ViewTenants'] }, () =>
      router.push({ pathname: '/rent/[id]', params: { id: 'c1' } }));

    expect(await screen.findByLabelText('Balance: ₹8,500', {}, FIND)).toBeTruthy();
    expect(screen.queryByRole('button', { name: /Record payment/ })).toBeNull();

    await act(() => router.push({ pathname: '/payments/[id]', params: { id: 'pay1' } }));
    expect(await screen.findByText('Paid towards', {}, FIND)).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Void this payment' })).toBeNull();
  });

  it('from Home, Record payment first asks who paid', async () => {
    jest.mocked(tenantsApi.list).mockResolvedValue(page([tenant]));
    await openAs({ role: 'Owner' });

    await fireEvent.press(await screen.findByRole('button', { name: 'Record payment' }, FIND));
    expect(await screen.findByText('Who paid?', {}, FIND)).toBeTruthy();
    expect(tenantsApi.list).toHaveBeenCalledWith(expect.objectContaining({ filter: 'Overdue' }), expect.anything());

    await fireEvent.press(await screen.findByRole('button', { name: /Rahul Sharma/ }, FIND));
    expect((await screen.findByLabelText('Amount received', {}, FIND)).props.value).toBe('25500');
  });

  it('a tenant shows their payments and a Record payment button', async () => {
    payments.list.mockResolvedValue(page([{
      id: 'pay1', tenantId: 't1', tenantName: 'Rahul Sharma', paymentDate: '2026-10-12', amount: 8500, method: 'Upi',
      status: 'Recorded', receiptId: 'rc1', receiptNumber: 'REC-2026-000123', createdAt: '',
    }]));
    await openAs({ role: 'Owner' }, () => router.push({ pathname: '/tenants/[id]', params: { id: 't1' } }));

    expect(await screen.findByRole('button', { name: 'Record payment' }, FIND)).toBeTruthy();
    expect(await screen.findByText('₹8,500 · UPI', {}, FIND)).toBeTruthy();
    expect(payments.list).toHaveBeenCalledWith(expect.objectContaining({ tenantId: 't1' }), expect.anything());
  });
});
