# ADR-0004: Money and rounding

- **Status:** Accepted
- **Decision:**
  - Money is `Money(decimal Amount, Currency Currency)` — never `double`. Arithmetic across currencies throws
    `CurrencyMismatchException` (a programming error, not a business outcome).
  - Amounts are stored as `numeric(19,4)` and rounded to the currency's minor units at posting time.
  - Default rounding is `MidpointRounding.ToEven` (banker's rounding), configurable per product via `RoundingPolicy`.
  - Interest is calculated at full precision daily and rounded only when posted; the final installment absorbs residue.
  - Currency minor units follow ISO-4217 (e.g. KES 2, UGX 0).
  - APIs send amounts as strings (`"1250.00"`); front ends use `decimal.js`.
- **Consequences:** Deterministic, auditable amounts; every calculator has golden-file tests (from M3 onward).
