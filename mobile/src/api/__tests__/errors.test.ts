import { ApiClientError, fieldErrorsFrom, userMessageFor } from '../errors';

describe('userMessageFor', () => {
  it('uses the spec wording for network failures', () => {
    expect(userMessageFor(new ApiClientError({ kind: 'network' }))).toEqual({
      title: 'Unable to connect.',
      detail: 'Please check your internet connection.',
    });
  });

  it('names the action and shows the safe server message for 4xx', () => {
    const error = new ApiClientError({
      kind: 'http',
      status: 400,
      code: 'PAYMENT_AMOUNT_INVALID',
      serverMessage: 'Please check the payment amount and try again.',
    });

    expect(userMessageFor(error, 'record payment')).toEqual({
      title: 'Unable to record payment.',
      detail: 'Please check the payment amount and try again.',
    });
  });

  it('never shows raw status codes or server internals for 5xx', () => {
    const message = userMessageFor(new ApiClientError({ kind: 'http', status: 500, serverMessage: 'NullReferenceException' }));

    expect(message.detail).not.toMatch(/500|Exception/);
  });

  it('asks the user to sign in again when the session is gone', () => {
    expect(userMessageFor(new ApiClientError({ kind: 'http', status: 401 })).title).toBe('Your session has ended.');
    expect(userMessageFor(new ApiClientError({ kind: 'http', status: 401, code: 'SESSION_EXPIRED' })).title).toBe(
      'Your session has ended.',
    );
  });

  it('shows the server message for a wrong password instead of "session ended"', () => {
    const error = new ApiClientError({ kind: 'http', status: 401, code: 'INVALID_CREDENTIALS', serverMessage: 'Incorrect email or password.' });

    expect(userMessageFor(error, 'sign in')).toEqual({ title: 'Unable to sign in.', detail: 'Incorrect email or password.' });
  });

  it('extracts the first message for each field', () => {
    const error = new ApiClientError({ kind: 'http', status: 400, fieldErrors: { email: ['Enter a valid email address.', 'x'] } });

    expect(fieldErrorsFrom(error)).toEqual({ email: 'Enter a valid email address.' });
    expect(fieldErrorsFrom(new Error('x'))).toEqual({});
  });

  it('handles non-API errors', () => {
    expect(userMessageFor(new Error('boom'), 'load tenants').title).toBe('Unable to load tenants.');
  });
});
