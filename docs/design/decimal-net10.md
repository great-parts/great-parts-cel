# `decimal` on .NET 10: `System.Decimal` as the platform type

From version 2.0 the set `decimal`, the literal flag and the `decimal` members of the API exist on both targets. On `net11.0` nothing changes: the value is `Decimal128` ([extension-decimal](extension-decimal.md)). On `net10.0` the value is `System.Decimal`: 96 bits of coefficient, exponent −28 to 0. What is the same and what differs on the two targets is the table of `docs/decimal.md` ("Same and different on the two targets"); this page is how one code of rules serves both platform types.

## One code, two platform types

The rules — reading, text, conversions, cohort, equality and order — work on `DecimalParts` (sign, coefficient as `UInt128`, exponent) and are compiled for both targets. What differs is the platform type and a few operations on it, gathered in one file per target:

| File | `net11.0` | `net10.0` |
| :- | :- | :- |
| `Values/PlatformDecimal.cs` | `global using PlatformDecimal = System.Numerics.Decimal128;` | `global using PlatformDecimal = decimal;` |
| `Values/DecimalPlatform.Decimal128.cs` (`#if NET11_0_OR_GREATER`) | `Decimal128`: parts by the BID bits | — |
| `Values/DecimalPlatform.SystemDecimal.cs` (`#if !NET11_0_OR_GREATER`) | — | `decimal`: parts by `decimal.GetBits` and `new decimal(lo, mid, hi, negative, scale)` |

Each platform file declares `internal static partial class DecimalPlatform` with the same members: the parts of a value and a value of parts, the arithmetic, the comparison, the zero test, the precision constants (`MaxDigits`: 34 or 29; `MinExponent`: −6176 or −28; the largest exponent: 6111 or 0; the largest coefficient: 10³⁴ − 1 or 2⁹⁶ − 1) and the payload of `CelValue` (two `ulong` words; on `net10.0` the four `int` of `GetBits` packed). The rest of the code names the platform type through the alias. `Library/Decimals/**`, `DecimalParts`, `DecimalTextRules`, `DecimalNumberRules`, `ConversionRules.Decimal.cs` have no whole-file `#if`; a block stays only where a member names `Decimal128` (the API members `TryGetDecimal128`, `FromDecimal128`, the input and result rows of `Decimal128`, the reading of the BID bits) or where the rule depends on the precision of the target.

## Arithmetic on `net10.0`

| Operation | Code |
| :- | :- |
| `+`, `−`, `*` | the `decimal` operator inside `try`; `OverflowException` is `Overflow` (3006) `decimal overflow` at the call. The exponent of an exact result is min(e₁, e₂) and e₁ + e₂, as IEEE; an inexact one is rounded by the platform, to the nearest, half to even (checked by the reference over `BigInteger`) |
| `/` | a zero divisor is `DivisionByZero` (3004) first; then `a / b` inside `try` (`OverflowException` → `Overflow`); then the cohort below |
| unary `−` | `decimal.Negate`, keeps the scale and gives a negative zero |
| order | `decimal.Compare`: by value, `-0 == 0`, cohorts equal |

**The cohort of a quotient.** `System.Decimal` division does not keep the ideal exponent of IEEE (`6.0 / 2` is `3`, not `3.0`). When the platform quotient `q` is exact — `q * b == a` exactly, checked on the parts: the product of the coefficients of `q` and `b` scaled to the exponent of `a` equals the coefficient of `a` — the exact value has a range of exponents `[lo, hi]` at which it can be stored: `hi` is the exponent of `q` with its trailing zeros dropped, but not above 0; `lo` is the smallest exponent, not below −28, at which the coefficient still fits in 96 bits. The code stores `q` with the exponent `t = clamp(e₁ − e₂, lo, hi)`, the ideal exponent of IEEE or the nearest one the platform holds. So `1.00m / 4m` is `0.25`, `6.0m / 2m` is `3.0`, `6m / 2m` is `3`, `1m / 4m` is `0.25` (the ideal 0 is not exact), `1m / 0.01m` is `100` (the ideal 2 is above 0).

An inexact quotient is the platform's: rounded to 28 places at most, and the platform drops the trailing zeros of the rounded quotient, so its text can be one digit or more shorter than 28 places (`1m / 3m` is `0.3333333333333333333333333333`, `2m / 3m` is `0.6666666666666666666666666667`; a rounded quotient that ends in a zero loses it). The value is the nearest one the platform holds either way, and equality and order do not depend on the cohort.

**Zeros.** A result that underflows rounds to the exponent −28 and may become a zero, without an error: `string(0.0000000000000000000000000001m * 0.1m)` is `"0E-28"`, and so is an inexact quotient that rounds to zero (`1e-28m / 10m`). The sign of a zero sum or difference and the exponent of a zero product are those of IEEE, as on `net11.0`, where the platform differs: `-0m + 0m` is `0` (the platform gives `-0`), `-0m + -0m` is `-0`; the code corrects them after the operator.

## Reading and the literal on `net10.0`

`DecimalTextRules.TryParse` reads the digits as on `net11.0`, into a coefficient of up to 34 digits and an exponent, with a flag that more digits were dropped. Then:

- **literal** (exact): an error when the coefficient has more than 29 digits, is at least 2⁹⁶, the exponent is below −28, or the exponent is above 0 and `c · 10ᵉ ≥ 2⁹⁶`; otherwise a positive exponent is multiplied into the coefficient (`1e2m` is 100 with exponent 0). The errors are `InvalidLiteral` (1001) with `{detail}` `more significant digits than System.Decimal holds` for more than 29 digits or a coefficient of 2⁹⁶ and more, `exponent out of the range of System.Decimal` for an exponent below −28 (`1.0e-28m` too: the written cohort counts, as on `net11.0`), `value out of the range of System.Decimal` for a positive exponent whose value does not fit;
- **string** (rounding): the digits of the text are rounded once, half to even, to the precision of `System.Decimal` by the algorithm of `ToSystemDecimal` ([decimal-text](decimal-text.md#systemdecimal)) applied to the original digits, so there is no double rounding through 34 digits; a value whose integer part does not fit 96 bits is `ConversionRange` (3007); a value below the exponent −28 rounds and can be zero.
- **double**: the shortest digits of `FromDouble` go through the same rounding: `decimal(1e300)` is `ConversionRange`, `decimal(1e-30)` is a zero with the exponent −28, `string()` `"0E-28"`.

## Text and conversions

`Format` is the same code: with an exponent never above 0, the scientific form appears only for a small value (`string(0.0000005m)` is `"5E-7"`), never with `E+`. `int()`, `uint()`, `double()` read the parts as on `net11.0`. `TryGetDecimal(out decimal)` is always `true` for the kind on `net10.0`; `FromDecimal(decimal)` stores the value as it is, no translation. The result row `T = decimal` is exact; `T = object` gives `System.Decimal`.

## The public API and the generator

The API on both targets: `CelExtensions.Decimal`, `EnableDecimalLiterals`, `CelType.Decimal`, `CelTypeKind.Decimal`, `CelValueKind.Decimal`, `CelValue.FromDecimal(decimal)`, `TryGetDecimal(out decimal)`, `CelObjectType.FromDecimal(decimal)`. On `net11.0` only: `CelValue.FromDecimal128`, `TryGetDecimal128`, `CelObjectType.FromDecimal128(int, Decimal128)`. The snapshot files: the six members of `decimal` that version 1.0 shipped for `net11.0` stay in `PublicAPI/net11.0/PublicAPI.Shipped.txt` and are, for `net10.0`, in `PublicAPI/net10.0/PublicAPI.Unshipped.txt` until the release of 2.0 puts them into the common file; `net11.0/PublicAPI.Unshipped.txt` has the `*REMOVED*` lines of the renamed `Decimal128` members and their new names. The generator describes a `System.Decimal` member as `decimal` on both targets and writes `FromDecimal(value)`; a `Decimal128` member, `FromDecimal128(number, value)`. `GPCEL007` is no longer reported: its descriptor, its line of `AnalyzerReleases.Unshipped.md` and its section of `docs/generator-diagnostics.md` are gone, and its number is not used again. `System.Decimal.Parse`, `TryParse`, `ToString`, `TryFormat` are in `eng/BannedSymbols.txt`.

## Tests

The reference over `BigInteger` has a second precision: coefficient below 2⁹⁶, exponent −28 to 0, rounding half to even, the quotient rule above and the zeros of IEEE; every property of the set runs on both targets with the precision of the target. The examples fix the rows that differ by target (`Examples/Library/DecimalTargetTests.cs`); the examples whose outcome is the same have no `#if`, and the rows that need `Decimal128`, 34 digits or an exponent that `System.Decimal` does not hold are `net11.0` only. The tests of the bits of `Decimal128` stay under `#if NET11_0_OR_GREATER`.

Related pages: [extension-decimal](extension-decimal.md), [decimal-text](decimal-text.md), [values](values.md), [tests](tests.md#tests-of-decimal).
