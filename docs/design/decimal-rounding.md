# `decimal` version 2: `round` and `quantize`

From version 2.0 the set `decimal` has two versions. Version 1 keeps its 18 overloads; version 2 has them and four more: `x.round(n)`, `x.round(n, mode)`, `x.quantize(q)` and `x.quantize(q, mode)`. The semantics, the modes and the examples are on the page `docs/decimal.md` ("Rounding"). This page is how one piece of code serves both targets.

## Versions of the set

`Library/Decimals/DecimalExtension.cs` makes two `CelExtension` values once, `VersionOne` and `VersionTwo`: the same 18 bindings, and for version 2 the four new ones; the source of the definition is `extension decimal v1` or `extension decimal v2`. `Get(1)` and `Get(2)` return them; another version is a set that the build rejects. Two versions of the set in one environment are `ExtensionVersionConflict` (4003), as for every set. The overload identifiers are `decimal_round_int64`, `decimal_round_int64_string`, `decimal_quantize_decimal`, `decimal_quantize_decimal_string`; all four are receiver-style, so `round` does not meet `math.round` of the set `math`.

## One rounding routine

`Values/DecimalRoundingRules.cs` works on `DecimalParts` (sign, coefficient as `UInt128`, exponent), like the text and the conversions, and does not call the arithmetic of the platform:

```
Round(parts, target exponent, mode):
  if exponent >= target: the value is exact at that exponent
  drop d = target − exponent digits: q, r = divmod(coefficient, 10^d)   (d ≥ 39: q = 0, r compared by digits)
  compare r with half of 10^d: below, half, above
  increment q by the mode, the sign and the last digit of q
  result: sign, q, target
```

- `round(n)`: target = −n, but never below the exponent of `x`, so no zeros are added; when −n is at or below the exponent the value is returned unchanged.
- `quantize(q)`: target = the exponent of `q`; when it is below the exponent of `x` the coefficient is multiplied by 10 to the difference (exact, or `Overflow` when it does not fit).
- The modes: `half_even`, `half_up` (away from zero), `half_down`, `down` (toward zero), `up` (away from zero), `floor`, `ceiling`; `floor` and `ceiling` read the sign. A zero result keeps the sign of `x`.

The result then goes through `DecimalPlatform.TryFromRounded`, the precision check of the target: more digits than the platform holds (34 or 29) or a coefficient above the largest one (10³⁴ − 1 or 2⁹⁶ − 1) is `Overflow` (3006). An exponent above the largest one that the platform keeps — 6111 on `net11.0`, 0 on `net10.0` — is folded into the coefficient when the coefficient stays within the platform (`1234m.round(-2)` is stored as 1200 with exponent 0 on `net10.0`; `(1.5m).round(-6111, 'up')` is `1E+6111`), and is `Overflow` otherwise. A zero takes the largest exponent: `(1.5m).round(-7000)` is `0E+6111` on `net11.0` and `0` on `net10.0`. This is the clamping that the arithmetic of the set already does. An exponent below −28 cannot occur on `net10.0`, because no `q` has one. A target exponent outside the range of `int` is clamped first: a huge `n` returns `x`, a huge negative one rounds to zero or, for a mode away from zero, overflows.

## The mode

The mode string is compared ordinally with the seven names; a string longer than nine UTF-16 units is rejected before the comparison, so the cost stays constant. Another string is `InvalidArgument` (3008) naming it; the subject is the function, `round` or `quantize`. A constant mode is resolved once when the program is planned (`Declarations/ConstantArgument.cs`, the mechanism of the pattern of `matches`, argument index 2), and a constant that is not a mode gives no error of type checking: the string is read again when the call is evaluated and is an evaluation error, as for `decimal('x')`.

## Checks

The reference over `BigInteger` of the tests (`tests/Great.Parts.Cel.Tests/Values/DecimalRoundingRulesTests.cs`) covers `round` and `quantize` in the seven modes, for both targets; the test of the set checks that version 1 still has exactly the 18 overloads of 1.0. Each overload costs 1 (`docs/cost.md`).
