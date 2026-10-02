/**
 * Money helpers. Amounts are validated as text (max 2 decimals) and only converted to a number to be
 * sent as JSON; the app never adds or multiplies money, so floating-point rounding cannot creep in.
 * All calculations happen on the server with decimal arithmetic.
 */

const AMOUNT_PATTERN = /^\d{1,10}(\.\d{1,2})?$/;

export const MONEY_MESSAGE = 'Enter an amount like 8500 or 8500.50.';

/** Empty input means "not provided". Commas and spaces are ignored ("8,500" is fine). */
export function parseMoney(text: string): { value?: number; error?: string } {
  const cleaned = text.replace(/[,\s₹]/g, '');
  if (!cleaned) {
    return {};
  }
  if (!AMOUNT_PATTERN.test(cleaned) || Number(cleaned) <= 0) {
    return { error: MONEY_MESSAGE };
  }
  return { value: Number(cleaned) };
}

/** Indian digit grouping: 842000 → "₹8,42,000"; 8500.5 → "₹8,500.50". */
export function formatRupees(amount: number): string {
  const negative = amount < 0;
  const [whole = '0', fraction = '00'] = Math.abs(amount).toFixed(2).split('.');
  const lastThree = whole.slice(-3);
  const rest = whole.slice(0, -3);
  const grouped = rest ? `${rest.replace(/\B(?=(\d{2})+(?!\d))/g, ',')},${lastThree}` : lastThree;
  return `${negative ? '-' : ''}₹${grouped}${fraction === '00' ? '' : `.${fraction}`}`;
}

/** For pre-filling an input from an API amount. */
export function moneyToInput(amount: number | null | undefined): string {
  if (amount === null || amount === undefined) {
    return '';
  }
  const [whole, fraction = '00'] = amount.toFixed(2).split('.');
  return fraction === '00' ? (whole ?? '') : `${whole}.${fraction}`;
}
