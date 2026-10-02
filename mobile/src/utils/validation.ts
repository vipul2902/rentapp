/**
 * Client-side checks mirroring the API's rules, for instant feedback. The API validates again and its
 * field errors are shown the same way, so the two never need to agree perfectly.
 */

export const PASSWORD_MIN = 8;
export const PASSWORD_MAX = 128;

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const PHONE_PATTERN = /^\+?[0-9(][0-9 ()-]{6,18}$/;

export type Validator = (value: string) => string | undefined;

export const required =
  (message: string): Validator =>
  (value) =>
    value.trim() ? undefined : message;

export const lengthBetween =
  (min: number, max: number, message: string): Validator =>
  (value) => {
    const length = value.trim().length;
    return length >= min && length <= max ? undefined : message;
  };

export const email: Validator = (value) => (EMAIL_PATTERN.test(value.trim()) ? undefined : 'Enter a valid email address.');

export const password: Validator = (value) =>
  value.length >= PASSWORD_MIN && value.length <= PASSWORD_MAX ? undefined : 'Password must be 8 to 128 characters.';

export const optionalPhone: Validator = (value) =>
  !value.trim() || PHONE_PATTERN.test(value.trim()) ? undefined : 'Enter a valid phone number.';

/** Runs each field's validators in order and returns the first message per field. */
export function validate<T extends Record<string, string>>(
  values: T,
  rules: Partial<Record<keyof T, Validator[]>>,
): Partial<Record<keyof T, string>> {
  const errors: Partial<Record<keyof T, string>> = {};
  for (const field of Object.keys(rules) as (keyof T)[]) {
    for (const rule of rules[field] ?? []) {
      const message = rule(values[field] ?? '');
      if (message) {
        errors[field] = message;
        break;
      }
    }
  }
  return errors;
}

export function hasErrors(errors: Partial<Record<string, string>>): boolean {
  return Object.values(errors).some(Boolean);
}
