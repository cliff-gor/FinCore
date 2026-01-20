/**
 * ISO-4217 minor units for the currencies FinCore seeds (spec 001: KES, USD, EUR, GBP, UGX, TZS).
 * Unknown currencies fall back to Intl's own default fraction digits.
 */
export const minorUnits: Readonly<Record<string, number>> = {
  KES: 2,
  USD: 2,
  EUR: 2,
  GBP: 2,
  UGX: 0,
  TZS: 2,
};

/** Number of decimal places for a currency code (case-insensitive). */
export function getMinorUnits(currency: string): number {
  const code = currency.toUpperCase();
  const known = minorUnits[code];
  if (known !== undefined) return known;
  return (
    new Intl.NumberFormat('en', { style: 'currency', currency: code }).resolvedOptions()
      .maximumFractionDigits ?? 2
  );
}
