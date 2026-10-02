import { formatRupees, MONEY_MESSAGE, moneyToInput, parseMoney } from '../money';

describe('formatRupees', () => {
  it.each([
    [0, '₹0'],
    [500, '₹500'],
    [8500, '₹8,500'],
    [8500.5, '₹8,500.50'],
    [117500, '₹1,17,500'],
    [842000, '₹8,42,000'],
    [12345678.9, '₹1,23,45,678.90'],
    [-3500, '-₹3,500'],
  ])('formats %p as %s', (amount, expected) => {
    expect(formatRupees(amount)).toBe(expected);
  });
});

describe('parseMoney', () => {
  it('treats empty input as not provided', () => {
    expect(parseMoney('  ')).toEqual({});
  });

  it.each([
    ['8500', 8500],
    ['8,500', 8500],
    ['₹ 8,500.50', 8500.5],
    ['0.01', 0.01],
  ])('accepts %p', (text, value) => {
    expect(parseMoney(text)).toEqual({ value });
  });

  it.each(['0', '-5', '8500.555', 'abc', '12345678901', '1e3'])('rejects %p', (text) => {
    expect(parseMoney(text)).toEqual({ error: MONEY_MESSAGE });
  });
});

describe('moneyToInput', () => {
  it('drops a zero fraction and keeps a real one', () => {
    expect(moneyToInput(8500)).toBe('8500');
    expect(moneyToInput(8500.5)).toBe('8500.50');
    expect(moneyToInput(null)).toBe('');
  });
});
