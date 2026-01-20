import { describe, expect, it } from 'vitest';
import {
  add,
  formatMoney,
  getMinorUnits,
  InvalidAmountError,
  parseAmount,
  roundToMinorUnits,
  subtract,
  toApiString,
} from './index.ts';

describe('parseAmount', () => {
  it('parses API amount strings exactly', () => {
    expect(parseAmount('1250.00').toFixed(2)).toBe('1250.00');
    expect(parseAmount(' -0.10 ').toString()).toBe('-0.1');
  });

  it.each(['', 'abc', '1e3', '1,000.00', 'NaN', '12.'])('rejects %j', (value) => {
    expect(() => parseAmount(value)).toThrow(InvalidAmountError);
  });
});

describe('arithmetic', () => {
  it('adds without floating-point drift', () => {
    expect(add('0.1', '0.2').toString()).toBe('0.3');
    expect(add('1250.00', '0.05', '-250.05').toFixed(2)).toBe('1000.00');
  });

  it('subtracts', () => {
    expect(subtract('100.00', '0.01').toFixed(2)).toBe('99.99');
  });
});

describe('minor units', () => {
  it('knows the seeded currencies', () => {
    expect(getMinorUnits('KES')).toBe(2);
    expect(getMinorUnits('ugx')).toBe(0);
  });

  it("rounds with banker's rounding like the backend", () => {
    expect(roundToMinorUnits('10.005', 'KES').toFixed(2)).toBe('10.00');
    expect(roundToMinorUnits('10.015', 'KES').toFixed(2)).toBe('10.02');
    expect(toApiString('1500.5', 'UGX')).toBe('1500');
    expect(toApiString('7', 'USD')).toBe('7.00');
  });
});

describe('formatMoney', () => {
  it('formats with Intl and the currency minor units', () => {
    expect(formatMoney('1250', 'USD', { locale: 'en-US', currencyDisplay: 'symbol' })).toBe(
      '$1,250.00',
    );
    expect(formatMoney('1234567.891', 'KES', { locale: 'en-US' })).toMatch(/KES\s1,234,567\.89/);
    expect(formatMoney('1500', 'UGX', { locale: 'en-US' })).toMatch(/UGX\s1,500$/);
  });

  it('keeps precision beyond Number.MAX_SAFE_INTEGER', () => {
    expect(formatMoney('90071992547409931.25', 'USD', { locale: 'en-US' })).toMatch(
      /90,071,992,547,409,931\.25/,
    );
  });

  it('can always show the sign', () => {
    expect(
      formatMoney('80.89', 'USD', {
        locale: 'en-US',
        currencyDisplay: 'symbol',
        signDisplay: 'always',
      }),
    ).toBe('+$80.89');
  });
});
