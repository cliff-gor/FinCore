import Decimal from 'decimal.js';
import { getMinorUnits } from './currencies.ts';

/** Money never touches JavaScript `number` arithmetic (master spec §30.4). Amounts travel as strings. */
export type MoneyInput = string | Decimal;

export class InvalidAmountError extends Error {
  constructor(value: string) {
    super(`"${value}" is not a valid decimal amount.`);
    this.name = 'InvalidAmountError';
  }
}

const decimalPattern = /^[-+]?\d+(\.\d+)?$/;

/** Parses an API amount string (e.g. "1250.00") into a Decimal. Rejects floats-in-disguise like "1e3". */
export function parseAmount(value: MoneyInput): Decimal {
  if (value instanceof Decimal) return value;
  const trimmed = value.trim();
  if (!decimalPattern.test(trimmed)) throw new InvalidAmountError(value);
  return new Decimal(trimmed);
}

export function add(...values: MoneyInput[]): Decimal {
  return values.reduce<Decimal>((sum, v) => sum.plus(parseAmount(v)), new Decimal(0));
}

export function subtract(a: MoneyInput, b: MoneyInput): Decimal {
  return parseAmount(a).minus(parseAmount(b));
}

/** Rounds to the currency's minor units using banker's rounding (matches the backend default policy). */
export function roundToMinorUnits(value: MoneyInput, currency: string): Decimal {
  return parseAmount(value).toDecimalPlaces(getMinorUnits(currency), Decimal.ROUND_HALF_EVEN);
}

/** Serialises to the API wire format: fixed minor units, no grouping. */
export function toApiString(value: MoneyInput, currency: string): string {
  return roundToMinorUnits(value, currency).toFixed(getMinorUnits(currency));
}

export interface FormatMoneyOptions {
  locale?: string;
  currencyDisplay?: 'symbol' | 'narrowSymbol' | 'code' | 'name';
  /** Always show a sign (`+` / `−`) — positive/negative amounts are never colour-only (§32.2). */
  signDisplay?: 'auto' | 'always' | 'exceptZero' | 'never';
}

/**
 * Formats an amount with Intl using the currency's minor units.
 * The decimal string is passed to Intl directly so no precision is lost to `number`.
 */
export function formatMoney(
  value: MoneyInput,
  currency: string,
  options: FormatMoneyOptions = {},
): string {
  const digits = getMinorUnits(currency);
  const rounded = roundToMinorUnits(value, currency).toFixed(digits);
  const formatter = new Intl.NumberFormat(options.locale ?? 'en-KE', {
    style: 'currency',
    currency: currency.toUpperCase(),
    currencyDisplay: options.currencyDisplay ?? 'code',
    signDisplay: options.signDisplay ?? 'auto',
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  });
  // Intl.NumberFormat v3 (ES2023) formats decimal strings exactly.
  return formatter.format(rounded as Intl.StringNumericLiteral);
}
