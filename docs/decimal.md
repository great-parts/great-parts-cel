# Decimal

The type `decimal` is an IEEE 754-2019 decimal128 number: up to 34 significant digits and an exponent from -6176 to 6111, in .NET `System.Numerics.Decimal128`. It is for money and other quantities whose digits matter: `0.1m + 0.2m == 0.3m` is `true`, and the sum of prices has exactly the digits of its terms. The type is part of this library and not of the CEL specification; cel-go has none, so nothing on this page is compared with it. This page is the whole contract of the type, the literal and the extension set `decimal`.

## The type

- **It exists only on .NET 11.** There is no `decimal` on .NET 10: the package for `net10.0` has no `Decimal128` (a type of .NET 11), no extension set `decimal`, no literal option and no member of the public API for the type. The code of an application that uses it builds for `net11.0` only, under `#if NET11_0_OR_GREATER` when it also targets `net10.0`. A `System.Decimal` variable on .NET 10 is `TypeNotDeclared` (4008) with the hint that the extension set `decimal` exists only on .NET 11.
- **A value is a sign, a coefficient and an exponent.** `Coefficient × 10^Exponent`, the coefficient an integer from 0 to 10³⁴ − 1 (34 digits). The digits with their trailing zeros are the cohort of the value: `1.5` is the coefficient 15 with the exponent -1, `1.50` is 150 with -2, and `string()` writes them as they are, so the digits are kept through arithmetic and conversion. A zero has a sign too: `-0` is a negative zero.
- **The values are finite.** There is no infinity and no NaN. A result that would be infinite is `Overflow` (3006) and a division by zero is `DivisionByZero` (3004), so a decimal that reaches an expression is always a number.
- **The type is named `decimal`.** It is a type identifier, as `int` is: `type(1.5m) == decimal` is `true` where the set is added. It is not a map key (`UnsupportedMapKeyType`, 3027), and the static `null` can be compared with it (`price == null` is `false`).

## Enabling it

An environment gets the type from the extension set and, for the literal, from an option of the builder:

```text
new CelEnvironmentBuilder(CelStandardLibrary.V1)
    .AddExtension(CelExtensions.Decimal(1))
    .EnableDecimalLiterals()
    .Build()
```

- **The set.** `CelExtensions.Decimal(1)` adds the type and the 18 overloads below. The set has one version, 1; another number is the build error `UnknownExtensionVersion` (4004), `extension 'decimal' has no version 2; available versions: 1`. It needs no other set, and the same set added twice is one set. The set cannot be named in an [environment description in JSON](environment-json.md): the name `decimal` is not the name of a set of the catalog (`InvalidConfiguration`, 4009); add the set in code.
- **The literal option.** `EnableDecimalLiterals()` lets the expressions of the environment write `12.34m`. It needs the set: the option without it is the build error `MissingExtensionDependency` (4005), `the decimal literal option requires extension 'decimal', which is not added to the environment`. The function `decimal('12.34')` needs the set only. The option has no key in a description in JSON.
- **The order of the calls does not matter**, as for every call of the builder, and `ToBuilder` keeps the set and the option.

## Literals

A decimal literal is a number with the suffix `m` or `M`, written in either of two forms:

- the fractional form with a point or an exponent: `12.34m`, `.5m`, `1e3m`, `1e-3m`, `2.5E+3M`;
- the integer form: `100m`, `0m`, `100M`.

There is no sign in the token, no `_` and no hexadecimal form. The value is exact: its digits and its exponent are those written.

| Text | `string()` of it | Digits |
| :- | :- | :- |
| `100m` | `"100"` | coefficient 100, exponent 0 |
| `0m` | `"0"` | 0, exponent 0 |
| `100M` | `"100"` | the same as `100m` |
| `1e2m` | `"1E+2"` | coefficient 1, exponent 2 |
| `-0m` | `"-0"` | a negative zero |
| `-0.0m` | `"-0.0"` | a negative zero with the exponent -1 |
| `12.34m` | `"12.34"` | 1234 and -2 |

`100m == 1e2m` is `true`, since the values are equal, and their texts are different. A minus before the literal belongs to it, so `-0m` is a negative zero; `-(0m)` is the negation of a zero at run time and gives the same value.

- **Without the option** both forms are `SyntaxNotEnabled` (1007), at the literal: `decimal literal is not enabled: enable decimal literals in the environment builder`. On .NET 10 it is always `SyntaxNotEnabled`, with `decimal literal is not enabled: decimal requires .NET 11`.
- **Too many digits.** A literal with more than 34 significant digits, or with an exponent outside -6176 to 6111, is `InvalidLiteral` (1001), at the literal: `invalid decimal literal: more than 34 significant digits` and `invalid decimal literal: exponent out of the range of decimal128`. A literal keeps all its digits or is an error; only `decimal(string)` rounds. Leading zeros are not significant: `1` and 39 zeros is 40 digits and an error, and `1e39m` is the same value written with an exponent.
- **What is not a literal.** `0x10m` is the number `0x10` and the identifier `m`, a `SyntaxError` at `m`, with or without the option. `12.5mx`, `100mx`, `12.34mm` and `1_000m` are `SyntaxError` too, and `1.m` is the selection of a field `m` of the value `1`, which type checking refuses with `NoSuchField`.

## Operators and conversions

The set has 18 overloads. All are global functions or operators, and each answers `EvalNoMatchingOverload` for a value of another kind, whatever the static types say. The arithmetic and the comparison are those of `Decimal128`.

| Overload | Function | Signature | What it does | Errors | Units |
| :- | :- | :- | :- | :- | :- |
| `add_decimal` | `_+_` | `(decimal, decimal) -> decimal` | the sum | `Overflow` | 1 |
| `subtract_decimal` | `_-_` | `(decimal, decimal) -> decimal` | the difference | `Overflow` | 1 |
| `multiply_decimal` | `_*_` | `(decimal, decimal) -> decimal` | the product | `Overflow` | 1 |
| `divide_decimal` | `_/_` | `(decimal, decimal) -> decimal` | the quotient | `DivisionByZero`, `Overflow` | 1 |
| `negate_decimal` | `-_` | `(decimal) -> decimal` | the negation | none | 1 |
| `less_decimal` | `_<_` | `(decimal, decimal) -> bool` | by value | none | 1 |
| `less_equals_decimal` | `_<=_` | `(decimal, decimal) -> bool` | by value | none | 1 |
| `greater_decimal` | `_>_` | `(decimal, decimal) -> bool` | by value | none | 1 |
| `greater_equals_decimal` | `_>=_` | `(decimal, decimal) -> bool` | by value | none | 1 |
| `int64_to_decimal` | `decimal` | `(int) -> decimal` | exact, exponent 0 | none | 1 |
| `uint64_to_decimal` | `decimal` | `(uint) -> decimal` | exact, exponent 0 | none | 1 |
| `double_to_decimal` | `decimal` | `(double) -> decimal` | the digits of `string(double)`, see below | `ConversionRange` | 4 |
| `string_to_decimal` | `decimal` | `(string) -> decimal` | the grammar below, rounded to 34 digits | `InvalidArgument`, `ConversionRange` | 1 + ⌈Lₐ/10⌉ |
| `decimal_to_decimal` | `decimal` | `(decimal) -> decimal` | the identity | none | 1 |
| `decimal_to_int64` | `int` | `(decimal) -> int` | truncated toward zero | `ConversionRange` | 1 |
| `decimal_to_uint64` | `uint` | `(decimal) -> uint` | truncated toward zero | `ConversionRange` | 1 |
| `decimal_to_double` | `double` | `(decimal) -> double` | the nearest `double`, half to even | `ConversionRange` | 1 |
| `decimal_to_string` | `string` | `(decimal) -> string` | the text of the value | none | 1 + ⌈Lᵣ/10⌉ |

Lₐ is the length of the string argument and Lᵣ the length of the text that is made, in UTF-16 code units; the page [Cost](cost.md#set-decimal) has the rule.

- **The quantum of a result is that of IEEE 754.** The sum and the difference take the smaller exponent, the product the sum of the exponents, and the quotient the preferred exponent (the exponent of the dividend minus that of the divisor) when the quotient is exact and 34 digits otherwise. So `1.50m + 1.5m` is `3.00`, `1.0m * 1.0m` is `1.00`, `6m / 2m` is `3`, `1.00m / 4m` is `0.25`, and `1m / 3m` is `0.3333333333333333333333333333333333`.
- **Rounding** is to 34 digits, half to even: a result of 35 digits whose last digit is 5 goes to the even digit before it.
- **The range.** A result whose exponent is above 6111 and whose coefficient can be padded with zeros is clamped (`1e6111m * 1e1m` is `1.0E+6112`, with the coefficient 10 and the exponent 6111); a result that cannot is `Overflow`, `decimal overflow`. Below the exponent -6176 the digits are rounded away, and a result that vanishes is a zero without an error: `1e-6176m * 0.1m` is `0E-6176`.
- **Zero.** `1.5m - 1.5m` is `0.0` with a plus sign; `-0m + -0m` is a negative zero and `-0m + 0m` a positive one; the product and the quotient follow the signs of the operands; the negation of a zero is a negative zero with the same exponent.
- **Division by zero.** A divisor that is a zero of any sign or exponent is `DivisionByZero`, `division by zero`, before the division, so `0.0m / 0.0m` and `1m / -0.00m` are errors too.
- **The identity** `decimal(x)` returns `x` itself: its coefficient, its exponent and the sign of a zero, so `string(decimal(1.50m))` is `"1.50"` and `string(decimal(-0.0m))` is `"-0.0"`.
- **To an integer.** `int(x)` and `uint(x)` cut the digits after the point, toward zero, and then check the range: `int(1.9m)` is `1`, `int(-1.9m)` is `-1`, and a value outside the type is `ConversionRange`, `9223372036854775808 is out of the range of int`, with the text of the value as `string(x)` writes it. The sign of `uint` is checked after the cut, unlike `uint(double)`: `uint(-0.5m)` and `uint(-0.0m)` are `0u`, and `uint(-1.5m)` is `ConversionRange`.
- **To a double.** `double(x)` is the nearest `double`, half to even. A value beyond the range of `double` is `ConversionRange`; a value that vanishes is a zero or a subnormal number without an error; a negative zero gives `-0.0`.
- **From a double.** `decimal(d)` takes the digits of the shortest text that reads back to `d`, the digits of `string(d)`, and lays them out as `string(d)` does, so `decimal(d)` and `decimal(string(d))` are the same value with the same exponent: `decimal(100.0)` is `100`, `decimal(1e6)` is `1E+6`, `decimal(0.1)` is `0.1`. A NaN and an infinity are `ConversionRange`, `NaN is out of the range of decimal` (`+Inf`, `-Inf`).

## Text and parsing

`string(x)` writes the value in the scientific notation of the decimal arithmetic specification, with a `-` for a negative value and a negative zero. With `n` digits in the coefficient and the exponent `e`, the text is plain when `e ≤ 0` and the exponent of the first digit is at least -6, and scientific otherwise:

| Coefficient and exponent | Text |
| :- | :- |
| 150 and -2 | `1.50` |
| 1000 and 0 | `1000` |
| 1 and 3 | `1E+3` |
| 5 and -6 | `0.000005` |
| 5 and -7 | `5E-7` |
| 0 and -1 | `0.0` |
| 0 and 3 | `0E+3` |
| the largest value | `9.999999999999999999999999999999999E+6144` |

The text has at most 42 characters. The text does not depend on the culture of the system.

`decimal(string)` reads `[+|-] (digits [. [digits]] | . digits) [(e|E) [+|-] digits]`: ASCII digits only, the whole string and nothing else. `1.`, `.5`, `+1`, `1e2` and `00012` are numbers; `.`, the empty string, `1e`, `e2`, `1.2.3`, ` 1` (with a space), `1_0`, `0x10`, `Infinity` and `NaN` are not.

- **A string outside the grammar** is `InvalidArgument` (3008), `invalid argument to 'decimal': cannot parse abc as decimal`.
- **More than 34 digits** are rounded to 34, half to even: `decimal('12345678901234567890123456789012345')` is `1.234567890123456789012345678901234E+34`, where the dropped 5 is a tie and goes to the even 4, and 35 nines round up to `1.000000000000000000000000000000000E+35`. Only the cohort of 34 digits or fewer is kept as written.
- **An exponent out of -6176 to 6111** after the rounding, in a zero too, is `ConversionRange` (3007), `1e-6200 is out of the range of decimal`: unlike the arithmetic, which gives a zero for a vanishing result, a text that cannot be stored as written is refused. `decimal('0e6112')` is an error. The grammar is checked before the range, so `decimal('1e')` is `InvalidArgument`.

## Equality and order

- **Equal by value.** `1.50m == 1.5m` and `-0m == 0m` are `true`; the cohort and the sign of a zero do not take part. A decimal in a list, a map value or an `in` test is compared the same way.
- **Only with a decimal.** A decimal equals and orders only with another decimal. `dyn(1.0m) == 1` is `false` without an error, and `price > 0` is a type checking error, `NoMatchingOverload` (2001): write `price > 0m` or `price > decimal(0)`. With a `dyn` operand the ordering of a decimal with another kind is `EvalNoMatchingOverload` (3000), `no such overload: _<_(decimal, int)`.
- **The other sets.** `math.greatest`, `math.least`, `lists.sort`, `sortBy` and the functions of the other sets have no overload for a decimal in this version; with a decimal under `dyn` they are `EvalNoMatchingOverload`. `json.encode` and the clauses of `format` do not take a decimal, because a decimal is no JSON number; write `string(x)`.
- **Order of a set.** `CelList.CreateSorted` orders decimals by the number; two elements that are equal as numbers, such as `1.5` and `1.50`, are an `ArgumentException`.

## Data of the application

`Decimal128` and `System.Decimal` are the CEL type `decimal` in a variable, a binding, a field, a parameter and a result, and `Decimal128?` and `decimal?` have the type of the value. An input is exact, with its digits, its exponent and the sign of its zero; a result is a `Decimal128` exactly and a `System.Decimal` rounded half to even to 28 places, with `ResultConversion` (3015) outside its range. A NaN or an infinity of `Decimal128` is refused by `CelBindings.Add` and is `ConversionRange` where it is read lazily. The rows, the typed collections and the checks of a binding are on the page [Type mapping](type-mapping.md#decimal); `CelObjectType.FromDecimal` and `CelValue.FromDecimal` make a value in code.

## Without the set

The set is what gives an environment the type, so the same expressions and declarations have an outcome that names the set where it is missing:

| Expression or declaration | .NET 11 without the set | .NET 10 |
| :- | :- | :- |
| `12.34m`, `100m` | `SyntaxNotEnabled` (1007), the option is not enabled | `SyntaxNotEnabled` with `decimal requires .NET 11` |
| `decimal('1')`, `type(x) == decimal` | `UndeclaredReference` (2000) | the same |
| a member of the type `System.Decimal` or `Decimal128` selected | `FieldNotAccessible` (2007) at the selection | the source generator excludes the member with a warning |
| `AddVariable<System.Decimal>`, a delegate with such a parameter | `TypeNotDeclared` (4008), `add the extension set 'decimal'` | `TypeNotDeclared`, `the extension set 'decimal' exists only on .NET 11` |
| a boxed `System.Decimal` or `Decimal128` in a `dyn` place | `TypeNotAccessible` (3012) at the node that reads it | the same for `System.Decimal` |
| a `decimal` key in a map literal | `UnsupportedMapKeyType` (3027), with the set too | not reachable |

## What version 1 does not have

The set has no `%`, no `round` and no `quantize`, no function of the other sets and no overload that mixes a decimal with an `int` or a `double`; a later version of the set may add them, and the functions of version 1 do not change. The set is not in the manifest of the oracle runs, since cel-go has no `decimal`: its checks are the examples of this page and a property test against an exact reference over big integers.

## What is inside the set

The set is `CelExtensions.Decimal`. It holds the type `decimal`, the 18 overloads above and their costs, and no macro, no variable and no syntax; the literal is syntax of the environment, not of a set. The values are 128 bits in the 16 bytes of `CelValue`; the arithmetic and the comparison are `Decimal128`, and the text, the reading of a string, the conversions to the numbers and to `System.Decimal` are the code of the library, which uses no culture and does not use the parsing or the formatting of `Decimal128`. The set exists once for every environment that adds it: the definition is immutable and shared, so any number of environments and threads use it.

Related pages: [Extension sets](extensions.md#set-decimal-version-1), [Cost](cost.md#set-decimal), [Type mapping](type-mapping.md#decimal), [Error codes](error-codes.md), [Limits](limits.md).
