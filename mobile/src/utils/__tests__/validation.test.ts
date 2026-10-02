import { email, hasErrors, optionalPhone, password, required, validate } from '../validation';

describe('validation', () => {
  it('returns the first failing message per field', () => {
    const errors = validate(
      { name: '', email: 'bad', password: 'short' },
      { name: [required('Enter your name.')], email: [required('Enter email.'), email], password: [password] },
    );

    expect(errors).toEqual({
      name: 'Enter your name.',
      email: 'Enter a valid email address.',
      password: 'Password must be 8 to 128 characters.',
    });
    expect(hasErrors(errors)).toBe(true);
  });

  it('accepts valid input', () => {
    const errors = validate({ email: ' asha@example.com ', password: 'Owner-Pass-123', phone: '' }, { email: [email], password: [password], phone: [optionalPhone] });

    expect(hasErrors(errors)).toBe(false);
  });

  it.each(['+91 98765 43210', '9876543210', '(080) 1234-5678'])('accepts phone %s', (phone) => {
    expect(optionalPhone(phone)).toBeUndefined();
  });

  it.each(['12', 'call me', '+91-98765-43210-12345-678'])('rejects phone %s', (phone) => {
    expect(optionalPhone(phone)).toBe('Enter a valid phone number.');
  });
});
