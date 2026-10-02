import { addDays, addYears, formatDate, ordinal, parseIsoDate, toIsoDate } from '../dates';

describe('dates', () => {
  it('round-trips calendar dates without time-zone drift', () => {
    expect(toIsoDate(parseIsoDate('2026-10-02'))).toBe('2026-10-02');
    expect(toIsoDate(parseIsoDate('2028-02-29'))).toBe('2028-02-29');
  });

  it('adds days across month and year boundaries', () => {
    expect(addDays('2026-10-31', 1)).toBe('2026-11-01');
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
    expect(addDays('2028-03-01', -1)).toBe('2028-02-29');
    expect(addYears('2026-10-02', -10)).toBe('2016-10-02');
  });

  it('formats for people', () => {
    expect(formatDate('2026-10-02')).toBe('2 Oct 2026');
    expect(formatDate('2027-01-15')).toBe('15 Jan 2027');
  });

  it.each([
    [1, '1st'],
    [2, '2nd'],
    [3, '3rd'],
    [4, '4th'],
    [11, '11th'],
    [12, '12th'],
    [13, '13th'],
    [21, '21st'],
    [22, '22nd'],
    [31, '31st'],
  ])('ordinal(%p) is %s', (n, expected) => {
    expect(ordinal(n)).toBe(expected);
  });
});
