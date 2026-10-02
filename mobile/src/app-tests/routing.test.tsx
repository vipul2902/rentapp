import * as SecureStore from 'expo-secure-store';
import { router } from 'expo-router';
import { act, fireEvent, renderRouter, screen } from 'expo-router/testing-library';

import { authApi } from '@/api/auth';
import { ApiClientError } from '@/api/errors';
import { resetSessionForTests } from '@/auth/session';
import { authResponse } from '@/test-utils/fixtures';

import AppLayout from '../app/(app)/_layout';
import AppIndex from '../app/(app)/index';
import StaffIndex from '../app/(app)/staff/index';
import AuthLayout from '../app/(auth)/_layout';
import ForgotPassword from '../app/(auth)/forgot-password';
import Register from '../app/(auth)/register';
import SignIn from '../app/(auth)/sign-in';
import RootLayout from '../app/_layout';

jest.mock('@/api/auth', () => ({
  authApi: { login: jest.fn(), register: jest.fn(), refresh: jest.fn(), logout: jest.fn(), me: jest.fn() },
}));
jest.mock('@/api/users', () => ({
  usersApi: { list: jest.fn(async () => ({ items: [], page: 1, pageSize: 25, totalCount: 0, totalPages: 0 })) },
}));

const api = jest.mocked(authApi);
const secureStore = SecureStore as typeof SecureStore & { __reset: () => void };
const FIND = { timeout: 5_000 };

/** The real route files, mounted in memory. */
const routes = {
  _layout: RootLayout,
  '(auth)/_layout': AuthLayout,
  '(auth)/sign-in': SignIn,
  '(auth)/register': Register,
  '(auth)/forgot-password': ForgotPassword,
  '(app)/_layout': AppLayout,
  '(app)/index': AppIndex,
  '(app)/staff/index': StaffIndex,
};

/**
 * renderRouter attaches getPathname() to the object it returns; with RNTL 14 that object is the render
 * promise, so keep a reference to it before awaiting.
 */
async function mount(initialUrl: string) {
  const view = renderRouter(routes, { initialUrl });
  await view;
  return { pathname: () => view.getPathname() };
}

describe('routing', () => {
  beforeEach(() => {
    resetSessionForTests();
    secureStore.__reset();
    jest.clearAllMocks();
  });

  it('sends a signed-out user to sign in', async () => {
    await mount('/');

    // Assert on what is rendered: after a protected-route redirect the in-memory router keeps reporting
    // the originally requested URL, so the pathname is not a reliable signal here.
    expect(await screen.findByRole('button', { name: 'Sign in' }, FIND)).toBeTruthy();
    expect(screen.queryByText('Hi, Asha')).toBeNull();
  });

  it('resumes a saved session straight into the app', async () => {
    await SecureStore.setItemAsync('rentapp.refreshToken', 'saved');
    api.refresh.mockResolvedValue(authResponse());

    const view = await mount('/');

    expect(await screen.findByText('Hi, Asha', {}, FIND)).toBeTruthy();
    expect(view.pathname()).toBe('/');
  });

  it('signing in moves from the sign-in screen to home', async () => {
    api.login.mockResolvedValue(authResponse());
    await mount('/sign-in');

    await fireEvent.changeText(await screen.findByLabelText('Email', {}, FIND), 'asha@example.test');
    await fireEvent.changeText(screen.getByLabelText('Password'), 'Owner-Pass-123');
    await fireEvent.press(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Hi, Asha', {}, FIND)).toBeTruthy();
    expect(api.login).toHaveBeenCalledWith('asha@example.test', 'Owner-Pass-123');
  });

  it('shows the server message when the password is wrong', async () => {
    api.login.mockRejectedValue(
      new ApiClientError({ kind: 'http', status: 401, code: 'INVALID_CREDENTIALS', serverMessage: 'Incorrect email or password.' }),
    );
    const view = await mount('/sign-in');

    await fireEvent.changeText(await screen.findByLabelText('Email', {}, FIND), 'asha@example.test');
    await fireEvent.changeText(screen.getByLabelText('Password'), 'wrong-password');
    await fireEvent.press(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Incorrect email or password.', {}, FIND)).toBeTruthy();
    expect(view.pathname()).toBe('/sign-in');
  });

  it('validates the form before calling the server', async () => {
    await mount('/sign-in');

    await fireEvent.press(await screen.findByRole('button', { name: 'Sign in' }, FIND));

    expect(await screen.findByText('Enter a valid email address.')).toBeTruthy();
    expect(screen.getByText('Enter your password.')).toBeTruthy();
    expect(api.login).not.toHaveBeenCalled();
  });

  it('shows Staff to owners only', async () => {
    await SecureStore.setItemAsync('rentapp.refreshToken', 'saved');
    api.refresh.mockResolvedValue(authResponse({ user: { role: 'Staff', name: 'Ravi Staff', permissions: ['RecordPayments'] } }));

    const view = await mount('/');

    expect(await screen.findByText('Hi, Ravi', {}, FIND)).toBeTruthy();
    expect(screen.queryByLabelText('Staff, Add staff and choose what they can do')).toBeNull();

    await act(() => router.push('/staff'));
    expect(view.pathname()).not.toBe('/staff');
  });
});
