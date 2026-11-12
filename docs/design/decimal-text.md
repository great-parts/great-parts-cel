# Decimal text and numbers: the parts of a value, reading, writing, conversions

The set [extension-decimal](extension-decimal.md) computes with `Decimal128` and does everything else with the code on this page: the reading of a literal and of a string, the text of a value, the conversions to and from `int`, `uint`, `double` and `System.Decimal`. It is three files of `Great.Parts.Cel.Values`, all under `#if NET11_0_OR_GREATER`, and they use no `BigInteger`, no culture, no `Decimal128.Parse`, `TryParse`, `TryParsePartial`, `ToString` or `TryFormat`, no conversion from `double` to `Decimal128`, and no allocation: a string is made only for a result, and the work buffers are `stackalloc` of at most 48 characters. A coefficient has at most 34 digits and is held in a `UInt128`; the file names `DecimalTextRules` and `DecimalNumberRules` end with `Rules` because the rule logic of a value belongs to rule files ([core](core.md#rule-files-of-decimal)).

| File | Content |
| :- | :- |
| `Values/DecimalParts.cs` | `readonly struct DecimalParts(bool Negative, UInt128 Coefficient, int Exponent)`: the reading and the writing of the BID bits, the powers of ten, the count of digits; no rule of the language |
| `Values/DecimalTextRules.cs` | `TryParse`, `Format`, `FromDouble`, `ToDouble` |
| `Values/DecimalNumberRules.cs` | `FromInt64`, `FromUInt64`, `ToInt64`, `ToUInt64`, `FromSystemDecimal`, `ToSystemDecimal` |

## The parts of a value

A value is its sign, its coefficient and its exponent: `Coefficient × 10^Exponent`, `Coefficient` from 0 to 10³⁴ − 1 (`MaxCoefficient`) and `Exponent` from −6176 (`MinExponent`) to 6111 (`MaxExponent`). The coefficient with its trailing zeros is the cohort: `1.50` is 150 and −2, `1.5` is 15 and −1.

- **Reading.** `DecimalParts.From(Decimal128 value)` takes the bits with `Decimal128.EncodeBinary`. The sign is bit 127. When bits 125 and 126 are not both set, the exponent is bits 113 to 126 minus 6176 and the coefficient is bits 0 to 112; a coefficient above `MaxCoefficient` is read as 0. When both are set the value is, by IEEE 754, a zero whose exponent is bits 111 to 124 minus 6176 (the two forms that infinity and NaN also use are never passed: every caller has tested `Decimal128.IsFinite`). `Decimal128` reads these bits the same way, so the arithmetic and this reading agree on every encoding, canonical or not.
- **Writing.** `ToDecimal128()` is `Decimal128.DecodeBinary` of `sign << 127 | (Exponent + 6176) << 113 | Coefficient`. The caller keeps the parts in range; the result is canonical.
- **Storage.** `CelValue.FromDecimal` stores `EncodeBinary` in the two payload words, the low 64 bits in the first, and `TryGetDecimal` takes them back with `DecodeBinary` ([values](values.md#decimal)); nothing converts through parts on the hot path of arithmetic, only the functions on this page do.
- **Powers and digits.** `Powers` is a static array of `UInt128`, `10⁰` to `10³⁸`. `DigitCount(coefficient)` is the number of decimal digits, 1 for 0, found by comparing with `Powers`.

## Reading a number

`DecimalTextRules.TryParse(ReadOnlySpan<char> text, bool exact, out DecimalParts value)` has two modes and four results: `Parsed`, `NotANumber`, `TooManyDigits` (only when `exact`) and `ExponentOutOfRange`. The literal and `decimal(string)` share the grammar, the rounding and the range; they differ in one place.

**Grammar.** `[+|-] ( digits [ . [digits] ] | . digits ) [ (e|E) [+|-] digits ]`, ASCII digits only, the whole text and nothing else: no spaces, no `_`, no group separators, no `Infinity` or `NaN`, no hexadecimal form. So `1.`, `.5`, `+1`, `1e2` and `00012` are numbers, and `.`, ``, `1e`, `e2`, `1.2.3`, ` 1`, `1_0`, `0x10` and the Arabic-Indic digits are not. The literal passes the text of its token without the suffix and without a sign, and gives the sign itself, since it is the unary minus that the parser folded in; a `string` may carry one.

**Value.** The digits of the integer part and of the fraction, with the point removed and the leading zeros skipped, are the coefficient, `n` digits; the exponent is the written exponent minus the number of digits of the fraction. A value whose digits are all zeros has the coefficient 0 and keeps its exponent: `0.00` is 0 and −2, `000` is 0 and 0. The written exponent is read into a `long`, and a magnitude above 10⁹ stops growing, since it is out of range whatever the digits are.

**More than 34 digits.** With `exact`, `n > 34` is `TooManyDigits`: a literal keeps all its digits or is an error. Without it, the string is rounded to 34 digits, half to even: the first 34 digits are the coefficient `c`, `d` digits are dropped, `d = n − 34`; the first dropped digit decides — above 5 up, below 5 down, and 5 up when any later dropped digit is not zero, otherwise a tie, which goes up exactly when `c` is odd. A carry that makes `c` equal to 10³⁴ gives 10³³ and one more exponent. The exponent grows by `d` and the dropped digits are not read into a number. The cohort of a string of 34 digits or fewer is kept as written.

**Range.** After the rounding, an exponent below `MinExponent` or above `MaxExponent` is `ExponentOutOfRange`, a zero included; there is no clamp and no rounding of the digits away below −6176: `decimal('1e-6200')` is an error, and `0e6112` is one. This is stricter than the arithmetic, where a result that vanishes is a zero: a text that cannot be stored as written is refused rather than changed.

| Text | Mode | Result |
| :- | :- | :- |
| `1.50` | either | 150, −2 |
| `.5`, `5.` | either | 5, −1; 5, 0 |
| `-0`, `-0.0` | either | negative zero, exponent 0; −1 |
| `1e2`, `1E+2`, `100` | either | 1, 2; 1, 2; 100, 0 (three cohorts of one value) |
| `0.000` | either | 0, −3 |
| 35 digits `12345678901234567890123456789012345` | `exact` | `TooManyDigits` |
| the same | rounding | tie, `c` ends in 4: 1234567890123456789012345678901234, 1 |
| 35 digits `12345678901234567890123456789012355` | rounding | tie, `c` ends in 5: 1234567890123456789012345678901236, 1 |
| 35 digits `12345678901234567890123456789012346` | rounding | above the tie: …35, 1 |
| 35 nines | rounding | carry: 10³³, 2 |
| `1` and 39 zeros | `exact` | `TooManyDigits`; written `1e39` it is 1, 39 |
| the same | rounding | 10³³, 6 |
| `1e-6176`, `1e6111` | either | 1, −6176; 1, 6111 |
| `1e-6177`, `1e6112` | either | `ExponentOutOfRange` |
| `Infinity`, `NaN`, `0x10`, `1_0`, ` 1` | either | `NotANumber` |

**Errors.** For the literal ([syntax](syntax.md#decimal-literals)): `TooManyDigits` is `InvalidLiteral` (1001) `invalid decimal literal: more than 34 significant digits`, `ExponentOutOfRange` is `invalid decimal literal: exponent out of the range of decimal128`; the place is the literal. For `decimal(string)` ([extension-decimal](extension-decimal.md#the-18-overloads)): `NotANumber` is `InvalidArgument` (3008), `ExponentOutOfRange` is `ConversionRange` (3007). The grammar is checked before the range, so `decimal('1e')` is `InvalidArgument` and `decimal('1e99999999999')` is `ConversionRange`.

## The text of a value

`DecimalTextRules.Format(DecimalParts value, Span<char> destination)` writes the to-scientific-string of the decimal arithmetic specification, with a `-` for a negative value, a zero included. With `n` the number of digits of the coefficient (1 for zero), `e` the exponent and `a = e + n − 1`:

- **Plain.** When `e ≤ 0` and `a ≥ −6`: with `e = 0` the digits; with `n > −e` the first `n + e` digits, a `.`, and the rest; otherwise `0.`, `−e − n` zeros and the digits.
- **Scientific.** Otherwise the first digit, then a `.` and the rest when `n > 1`, then `E`, a `+` or a `-` by the sign of `a`, and `|a|` in decimal without leading zeros.

So `150` with −2 is `1.50`, `1000` with 0 is `1000`, `1` with 3 is `1E+3`, `5` with −6 is `0.000005`, `5` with −7 is `5E-7`, a negative zero with 0 is `-0`, `0` with −1 is `0.0`, `0` with 3 is `0E+3`, `0` with −8 is `0E-8`, and the largest value is `9.999999999999999999999999999999999E+6144`. The text is at most 42 characters, 48 is the size of the buffer, and the digits are written from the `UInt128` in two steps of at most 17 digits, each a `ulong` formatted with the invariant culture, since a `UInt128` division per digit is too slow for a text that a cost counts. The reading of this text by `TryParse` gives the same cohort back, which the property `decimal(string(x))` equals `x` in coefficient, exponent and sign checks.

## From and to double

**`FromDouble(double value, out DecimalParts parts)`** for a finite value; a NaN and an infinity are refused by the caller as `ConversionRange` before the call. The digits are those of `string(double)` — the shortest that read back to the same `double`, from `DoubleTextRules.ShortestDigits` of `Values`, `nd` digits and the position `point` of the decimal point counted from the first digit — laid out as `string(double)` lays them out, so that `decimal(x)` and `decimal(string(x))` are the same value with the same cohort. The layout of Go `%g` has the exponent `x = point − 1` of the first digit:

- `x < −4` or `x ≥ 6`, the exponent form `d.ddde±XX`: the coefficient is the `nd` digits and the exponent `point − nd`. `1.5e10` is 15 and 9; `1e6` is 1 and 6; `1e-5` is 1 and −5.
- Otherwise the plain form. With `point ≥ nd` the digits are followed by `point − nd` zeros, the coefficient is that integer and the exponent 0: `100.0` is 100 and 0, `123456.0` is 123456 and 0. With `0 < point < nd` the coefficient is the `nd` digits and the exponent `point − nd`: `1.25` is 125 and −2. With `point ≤ 0` the coefficient is the `nd` digits and the exponent `point − nd`: `0.001` is 1 and −3.
- Zero is 0 and 0, negative when `double.IsNegative`: `decimal(-0.0)` is a negative zero, as `string(-0.0)` is `"-0"`.

At most 17 digits and an exponent from −340 to 308 come out, so no rounding and no range error is possible. `DecimalTextRules` does not use `NumberText` of `Library`, which the layer table does not allow it to reference; the layout is these three lines, and the test of the part compares `FromDouble(x)` with `TryParse(string(x))` on random doubles and on the edges of the two forms.

**`ToDouble(DecimalParts value, out double result)`** writes the text of the value into 48 characters on the stack and reads it with `double.Parse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture)`, which gives the nearest `double`, half to even, since .NET Core 3.0; the text is the one above, so it is a number of that syntax. An infinite result is `ConversionRange`; a result that underflows is a zero or a subnormal value without an error, as `double('1e-400')` is. A negative zero gives `-0.0`. A faster path for a coefficient below 2⁵³ and an exponent of magnitude up to 22, where one multiplication or division of two exact `double` values is correctly rounded, may come first; it must give the same bits.

## Integers

`DecimalNumberRules.FromInt64(long)` and `FromUInt64(ulong)` give the magnitude as the coefficient, exponent 0, negative for a negative `long`; `long.MinValue` is 9223372036854775808. They are exact.

**`ToInt64` and `ToUInt64`** truncate toward zero, then check the range. The magnitude `m` of the truncated value is, as a `UInt128`: 0 for a zero coefficient; for an exponent `e ≥ 0`, too large when `DigitCount(c) + e > 20` (the value is at least 10²⁰) and `c × 10^e` otherwise; for `e < 0`, 0 when `−e ≥ 39` and `c / 10^−e` otherwise. Then:

- `int`: a non-negative value needs `m ≤ long.MaxValue`; a negative one `m ≤ 2⁶³`, and `-9223372036854775808.9m` is `long.MinValue`. Else `ConversionRange` with the value as written by `Format`: `int(9223372036854775808m)` and `int(1e19m)`.
- `uint`: `m ≤ ulong.MaxValue`, and a negative value needs `m = 0`: `uint(-0.5m)` and `uint(-0.0m)` are `0u`, `uint(-1.5m)` is `ConversionRange`. The sign is tested after the truncation, unlike `uint(double)`, which tests it before.

The magnitude `m` is never found by a loop over digits: at most one multiplication or one division of `UInt128`.

## `System.Decimal`

**To a value.** `FromSystemDecimal(decimal)` reads `decimal.GetBits`: the coefficient is the 96 bits (`hi`, `mid`, `lo` in that order), the exponent is minus the scale (bits 16 to 23 of the flags) and the sign is bit 31. It is exact — 29 digits at most, which `MaxCoefficient` holds — and keeps the cohort and the sign of a zero: `1.50m` is 150 and −2, a negative `0.00m` is a negative zero with −2. It does not use the implicit conversion of `Decimal128`.

**From a value.** `ToSystemDecimal(DecimalParts value, out decimal result)` is exact when the value fits and rounded half to even when it does not; a value out of the range is `false`, which the caller makes `ResultConversion` (3015) at the result and `ArgumentConversion` (3024) for a parameter ([values](values.md#conversion-of-decimal-values)). The coefficient must be at most 2⁹⁶ − 1 and the scale from 0 to 28:

- `e ≥ 0`: `c × 10^e` when `DigitCount(c) + e ≤ 29`, else out of range, and out of range when the product exceeds 2⁹⁶ − 1; scale 0.
- `e < 0`: scale `s = −e`; drop `k = max(s − 28, 0)` digits with the rounding of "Reading a number"; while the result exceeds 2⁹⁶ − 1, drop one more digit from the original coefficient; when `k` would exceed `s` the integer part is too large, out of range. For `k ≥ 39` the result is 0. The new scale is `s − k`.
- The result is `new decimal(lo, mid, hi, negative, scale)`, which keeps a negative zero and the scale of a zero.

So `1.5E-28` is `0.0000000000000000000000000002` (tie, to the even 2), `0.5E-28` is `0.0000000000000000000000000000`, `1E-30` the same, `1E+29` is out of range, and `79228162514264337593543950335` is `decimal.MaxValue`.

Related pages: [extension-decimal](extension-decimal.md), [values](values.md), [syntax](syntax.md), [library](library.md), [tests](tests.md#tests-of-decimal).
