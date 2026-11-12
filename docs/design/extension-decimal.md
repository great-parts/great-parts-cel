# Extension set `decimal`, version 1

The set adds the CEL type `decimal`, an IEEE 754-2019 decimal128 number of 34 significant digits, with `+`, `-`, `*`, `/`, unary minus, the four orderings and 9 conversions: 18 overloads. It exists only on `net11.0`, because the arithmetic is `System.Numerics.Decimal128`, a type of .NET 11; on `net10.0` there is no set, no literal flag and no member of the public API for it. cel-go has no such type, so nothing here is compared with it: the checks are the examples of the specification and a property test against an exact reference over `BigInteger`. Text, rounding and number conversions are on [decimal-text](decimal-text.md); the value is in [values](values.md#decimal), the type in [types](types.md#decimal), the literal in [syntax](syntax.md#decimal-literals), the outcomes in an environment without the set in [checking](checking.md#decimal-without-the-set), [evaluation](evaluation.md#decimal-in-a-dyn-place) and [declarations](declarations.md#decimal-without-the-set-and-the-literal-option), the cost in [library](library.md#cost-of-decimal), the description in JSON in [configuration](configuration.md#decimal-in-a-description), the tests in [tests](tests.md#tests-of-decimal).

## What `Decimal128` does, checked on the SDK

The set trusts `Decimal128` for arithmetic and comparison, and the page "Extension sets" of the design forbade a decision before the facts. They were checked on the SDK of the build image, 11.0.100 with the runtime 11.0.0, with a probe program that compared the operators with a reference over `BigInteger` written from the rules of IEEE 754-2019 (the exact result, the quantum, rounding to 34 digits half to even, the range of the exponent, the clamp, subnormal results): 200 000 random pairs for each of `+`, `-`, `*`, `/` and for the comparison, with coefficients of 1 to 34 digits that include `9…9`, `10…0` and ties, exponents from −6176 to 6111 and near both ends, both zeros and many cohorts. There was no difference. These are the facts the rest of the design relies on:

| Fact | Result on the probe |
| :- | :- |
| the quantum of a result | IEEE: the sum and the difference take the smaller exponent, the product the sum of the exponents, the quotient the preferred exponent `e₁ − e₂` when it is exact and 34 digits otherwise; `1.50 + 1.5` is `3.00`, `1.0 * 1.0` is `1.00`, `6 / 2` is `3`, `1.00 / 4` is `0.25`, `1 / 3` is `0.3333333333333333333333333333333333` |
| rounding | to 34 digits, half to even; a result of 35 digits whose tail is 5 rounds to the even digit |
| the range | the exponent of a stored value is from −6176 to 6111; a result of a larger exponent whose coefficient can be padded with zeros is clamped (`1E+6111 * 1E+1` is `10E+6111`), otherwise infinite; below −6176 the digits are rounded away: `1E-6176 * 0.1` is `0E-6176`, without an error |
| infinity and NaN | the operators never throw: an overflow is ±∞, `x / 0` is ±∞ and `0 / 0` is a NaN |
| zero | `1.5 − 1.5` is `0E-1` with a plus sign; `-0 + -0` is a negative zero; `-0 * 5` is a negative zero; the negation of a zero is a negative zero and keeps the exponent |
| equality and order | by value: `1.5 == 1.50`, `-0 == 0`; `CompareTo`, the six operators and `Equals` agree; `GetHashCode` is the same for the cohorts of a value and for both zeros, as `IEquatable<T>` requires |
| the encoding | `Decimal128.EncodeBinary` and `DecodeBinary(UInt128)` give and take the BID bits: the sign in bit 127, the biased exponent (bias 6176) in bits 113 to 126 and the coefficient in bits 0 to 112; the encoding of a result is canonical; a coefficient above 10³⁴ − 1, or a combination field that starts with `11` that is not infinity or NaN, is read by `Decimal128` as a zero, as IEEE says |

So the set needs no arithmetic of its own, and its volume does not grow. The reading and the writing of the parts of a value — the sign, the coefficient and the exponent — go through the BID bits and nothing else: `Decimal128` has no public member for the coefficient or the exponent, and its constructors are private. The property test of the part repeats the comparison with its own reference on every platform of the CI, so a difference in a later SDK is a failing test.

**Members of `Decimal128` that the library may use**, on every path: `EncodeBinary`, `DecodeBinary`, the operators `+`, `-`, `*`, `/`, unary `-`, `<`, `<=`, `>`, `>=`, `==`, `!=`, `IsFinite`, `Equals`, `GetHashCode`. Parsing, formatting and the conversion from `double` are banned, and so, by this design, are the conversions from `Decimal128` to a number type (`(double)`, `(long)`, `(decimal)`, `Truncate`, `Round`): their rounding and their overflow are not documented, and the library has its own, from the parts of the value ([decimal-text](decimal-text.md)). The conversions from and to `System.Decimal` are the library's own too.

## Groups of files and who writes them

The part is written in three groups of changes, in this order: values, then the set and the literal together as two parallel changes that share no file. A change belongs to the group of the file it edits; a file of another group is edited only as these tables say. Paths are relative to the repository root, and every new file of the code is under `#if NET11_0_OR_GREATER` as a whole.

**The values (group 1)**

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Values/DecimalParts.cs` (new) | the internal struct of a value taken apart: sign, coefficient as `UInt128`, exponent; reading and writing the BID bits ([decimal-text](decimal-text.md#the-parts-of-a-value)); the static mark `SystemDecimalOrigin` |
| `src/Great.Parts.Cel/Values/DecimalTextRules.cs` (new, rule file) | the reading of a literal and of a string, rounding, the text of a value, `double` to a decimal and back |
| `src/Great.Parts.Cel/Values/DecimalNumberRules.cs` (new, rule file) | `int`, `uint` and `System.Decimal` to and from a value, the digits of a coefficient, the powers of ten |
| `src/Great.Parts.Cel/Values/CelValue.cs` | `FromDecimal`, `TryGetDecimal`, `Equals`, `GetHashCode` ([values](values.md#decimal)) |
| `src/Great.Parts.Cel/Values/CelObjectType.cs` | the bodies of the two `FromDecimal` |
| `src/Great.Parts.Cel/Values/ValueTypeRules.cs`, `ValueTypeNames.cs`, `EqualityRules.cs`, `OrderingRules.cs` | the rows of the kind `Decimal`, the ordering row that `CelList.CreateSorted` uses ([values](values.md#decimal)) |
| `src/Great.Parts.Cel/Values/ConversionRules.cs`, `ConversionRules.Result.cs`, `ConversionRules.Rows.cs`, `ConversionRules.Collections.cs` and the new `ConversionRules.Decimal.cs` | the input rows and the result rows of `Decimal128`, `System.Decimal` and their `Nullable<>`, and the boxed values of the `object` row ([evaluation](evaluation.md#decimal-in-a-dyn-place)); the markers "of a later part" for `decimal` go |
| `src/Great.Parts.Cel/CelBindings.cs` | the `ArgumentException` for a NaN or an infinity |
| `src/Great.Parts.Cel/Types/CelType.cs`, `Types/AssignabilityRules.cs` | `CelType.Decimal`; `Decimal` among the types that take `null` |
| `PublicAPI/**` | nothing: no member beyond the skeleton |
| `tests/Great.Parts.Cel.Tests/Values/`, `docs/type-mapping.md`, `CHANGELOG.md` | the tests of this group; the rows of `decimal`; one line |

**The set (group 2)**

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Library/Decimals/DecimalExtension.cs` (new) | the definition, the table of the 18 overloads, the one `CelExtension` of version 1 and the others |
| `src/Great.Parts.Cel/Library/Decimals/DecimalFunctions.cs` (new) | the implementations |
| `src/Great.Parts.Cel/Library/Decimals/DecimalCosts.cs` (new) | the cost function of the two overloads that read a string, and the charge of the text |
| `src/Great.Parts.Cel/CelExtensions.cs` | the body of `Decimal(uint)`, which asks `DecimalExtension` |
| `tests/Great.Parts.Cel.Tests/Library/Decimals/`, `docs/decimal.md` (new), `docs/extensions.md`, `docs/cost.md` | the tests; the pages |

`ExtensionCatalog.cs`, `OverloadIds.cs`, `Costs.cs` and `eng/layers.txt` are not touched: see "The set" and "Layers".

**The literal and the cases without the set (group 3)**

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Syntax/SyntaxOptions.cs`, `Parser.cs`, `Literals.cs`, `Printer.cs`, `SyntaxTreeComparer.cs` | the flag, the value of a literal, the printing, the comparison ([syntax](syntax.md#decimal-literals)); `Lexer.cs` and `TokenKind.cs` already have both forms and change only if a test shows a gap |
| `src/Great.Parts.Cel/CelEnvironmentBuilder.cs`, `Declarations/DeclarationSet.cs`, `Declarations/ExtensionChecks.cs`, `Declarations/BuildErrors.cs`, `Declarations/DeclarationChecks.cs` | `EnableDecimalLiterals`, the code 4005 for it, 4008 for a variable and an overload with the hint, the fields of the kind `Decimal` that are not an error at `Build` ([declarations](declarations.md#decimal-without-the-set-and-the-literal-option)) |
| `src/Great.Parts.Cel/Checking/**` | 2007 for a field of the kind `Decimal` ([checking](checking.md#decimal-without-the-set)) |
| `src/Great.Parts.Cel/Values/ObjectRules.cs`, `Values/TypeIndex.cs`, `Evaluation/VariablePlanNode.cs`, `Evaluation/Invocation.cs` | the bare `System.Decimal` and `Decimal128` in a `dyn` place: 3012 without the set ([evaluation](evaluation.md#decimal-in-a-dyn-place)); the map key 3027 needs no change |
| `src/Great.Parts.Cel/Configuration/**` | none expected: the name `decimal` is not in `Names` ([configuration](configuration.md#decimal-in-a-description)) |
| `tests/Great.Parts.Cel.Tests/{Syntax,Declarations,Checking,Evaluation,Configuration}/`, `docs/error-codes.md`, `docs/environment-json.md` | the tests; the pages |

Groups 2 and 3 share no file: `CHANGELOG.md`, the one file that every group would touch, is edited by group 1 and by the closing change only, so the two parallel branches never meet in it. The snapshot of the declarations of the set and the unit tests of the layers and of the public documents are in the closing change ([tests](tests.md#tests-of-decimal)).

### What is under `#if NET11_0_OR_GREATER`

| How | Files |
| :- | :- |
| the whole file | `Values/DecimalParts.cs`, `DecimalTextRules.cs`, `DecimalNumberRules.cs`, `ConversionRules.Decimal.cs`; `Library/Decimals/*.cs`; the test files of the part, except where a test of the common code adds a row ([tests](tests.md#tests-of-decimal)) |
| a block in a file that exists on both targets | the members and the cases that name the kind or the type: `CelValue.cs` (`FromDecimal`, `TryGetDecimal`, the cases of `Equals` and `GetHashCode`), `CelObjectType.cs` (the two `FromDecimal`), `CelType.cs` (`Decimal`), `CelExtensions.cs` (`Decimal`), `CelEnvironmentBuilder.cs` (`EnableDecimalLiterals` and its field), `CelBindings.cs`, `ValueTypeRules.cs`, `ValueTypeNames.cs`, `EqualityRules.cs` (the kind `Decimal`), `ConversionRules*.cs` (the rows and the rows of the table), `AssignabilityRules.cs` (`TakesNull`), `Literals.cs` and `Parser.cs` (the conversion of the literal; the branch of the message of `net10.0` is the `#else` that the skeleton has), `Declarations/DeclarationChecks.cs` and `ExtensionChecks.cs` (the kind `Decimal`, the option and step 5), `Checking/Checker.cs` (2007), `TypeIndex.cs` (`DecimalDeclared`), `ObjectRules.cs` and the two plan nodes (the refusal of the kind `Decimal`) |
| the other branch | `Declarations/DeclarationChecks.cs`: the hint and the unmapped `System.Decimal` that only `net10.0` reaches are in a `#if !NET11_0_OR_GREATER` block; `Syntax/Printer.cs` and `SyntaxTreeComparer.cs`: the case of the kind `Decimal` is a `#if` block |
| no `#if` | `SyntaxOptions.DecimalLiterals`, a positional member of a record: it exists on both targets and is always `false` on `net10.0` |

`PublicAPI/net11.0/PublicAPI.Unshipped.txt` already has every public member of the part, and `PublicAPI/net10.0` has none and stays so; a build for each target is the check. The snapshot of the declarations of the set, `decimal-v1.txt`, belongs to the closing change.

## The surface of the public API

Nothing is added. The skeleton has all the members the part needs: `CelExtensions.Decimal(uint)`, `CelEnvironmentBuilder.EnableDecimalLiterals()`, `CelValue.FromDecimal(Decimal128)` and `TryGetDecimal`, `CelValueKind.Decimal`, `CelType.Decimal`, `CelTypeKind.Decimal`, `CelObjectType.FromDecimal(int, Decimal128)` and `FromDecimal(decimal)`. `PublicAPI/net11.0/PublicAPI.Unshipped.txt` has them, `PublicAPI/net10.0` has none, and neither file changes. The part gives their bodies, and no `Decimal128` appears in a signature that exists in `net10.0`.

## The type `decimal`

`CelType.Decimal` is a static instance of the kind `Decimal`, the name `decimal`, no parameters; its text form is `decimal`. The set declares it with the one `AbstractTypeDeclaration("decimal", CelType.Decimal)`: as for `optional_type` the entry is a name and a type, and the kind need not be `Abstract`. The declaration gives the environment the identifier `decimal`, whose static type is `type(decimal)` and whose value is the type `decimal`, so `type(1.5m) == decimal` is `true` with the set and `UndeclaredReference` (2000) without it, as is `decimal('1')`. A provider that describes an object type named `decimal` is `DuplicateType` (4001), as for `net.IP`. Assignability and `null` are in [types](types.md#decimal).

## The set

The definition is `DecimalDefinition : ExtensionDefinition` in `Library/Decimals/`: the name `decimal`, the version 1, the source `extension decimal v1`, no dependencies, the 18 bindings below in this order, the one abstract type, no macros and no variables. It is not a `CatalogExtension`: the flag of the literal is not a flag of a set, and the macros and the optional syntax that `CatalogExtension` adds are empty.

**The catalog.** `decimal` is not a row of `ExtensionCatalog`. Two reasons: the layer table lets `Great.Parts.Cel` and `Library` reach `Library.Decimals` but not `Library.Extensions`, and a row would put the name in `ExtensionCatalog.Names`, so that a description could add the set, which the public documentation of `CelExtensions.Decimal` says it cannot. `DecimalExtension` therefore keeps what a row keeps, for one family: a `static readonly CelExtension` of version 1 made once, and `Get(uint version)`, which returns that instance for 1 and, for any other number, a new `CelExtension("decimal", version, null, [1])`. `CelExtensions.Decimal(version)` returns `DecimalExtension.Get(version)` and checks nothing, as its siblings. The build checks need no change: step 1 turns the missing definition into `UnknownExtensionVersion` (4004), `extension 'decimal' has no version 2; available versions: 1`; `IsSameSetAs` holds for the same instance, so adding the set twice is one set. `Get(0)` and `Get(2)` are 4004; there is no version 2 in this release.

The sentences of [extensions](extensions.md) that count `decimal` among the later parts "with their row" are superseded by this section.

### Layers

`Great.Parts.Cel.Library.Decimals` references `Declarations`, `Values`, `Types` and `Diagnostics`, as `eng/layers.txt` already says, and nothing else: it cannot use `Library` (`Costs`, `Conversions`, `NumberText`) or `Library.Extensions`. What it needs of them it writes itself, in a few lines: the cost of a string (`⌈Lₐ/10⌉` of the text arguments) in `DecimalCosts`; the error values through `CallSite`; the text of a number through `DecimalTextRules`. The `double` text of the library, `NumberText.FormatDouble`, lives in `Library` and is needed by `decimal(double)`: the part moves nothing, and `DecimalTextRules.FromDouble` takes the shortest digits from `DoubleTextRules` of `Values` and applies the layout rule of `string(double)` itself ([decimal-text](decimal-text.md#from-and-to-double)). `Great.Parts.Cel` — the facade, where `CelExtensions` is — may reference `Library.Decimals`, so no edit of `eng/layers.txt` is needed.

## The 18 overloads

The identifiers are local strings of `DecimalExtension`, as those of `network` are; `OverloadIds` is not changed. `FixedCost` is the first number of "Units"; "arg" is the `ArgumentCost` charged before the call; "text" is a charge made inside the call. A function with a bad argument gives its error value at the call node; `site.NoMatchingOverload` answers any argument of another kind, as every implementation does ([extensions](extensions.md#implementations-of-the-sets)).

| Overload | Function | Signature | Implementation | Errors | Units |
| :- | :- | :- | :- | :- | :- |
| `add_decimal` | `_+_` | `(decimal, decimal) -> decimal` | `a + b` | `Overflow` | 1 |
| `subtract_decimal` | `_-_` | `(decimal, decimal) -> decimal` | `a - b` | `Overflow` | 1 |
| `multiply_decimal` | `_*_` | `(decimal, decimal) -> decimal` | `a * b` | `Overflow` | 1 |
| `divide_decimal` | `_/_` | `(decimal, decimal) -> decimal` | `DivisionByZero` when `b` is zero of any sign or cohort, before the division; else `a / b` | `DivisionByZero`, `Overflow` | 1 |
| `negate_decimal` | `-_` | `(decimal) -> decimal` | `-a` | — | 1 |
| `less_decimal` | `_<_` | `(decimal, decimal) -> bool` | `a < b` | — | 1 |
| `less_equals_decimal` | `_<=_` | `(decimal, decimal) -> bool` | `a <= b` | — | 1 |
| `greater_decimal` | `_>_` | `(decimal, decimal) -> bool` | `a > b` | — | 1 |
| `greater_equals_decimal` | `_>=_` | `(decimal, decimal) -> bool` | `a >= b` | — | 1 |
| `int64_to_decimal` | `decimal` | `(int) -> decimal` | `DecimalNumberRules.FromInt64`: the magnitude, exponent 0 | — | 1 |
| `uint64_to_decimal` | `decimal` | `(uint) -> decimal` | `FromUInt64` | — | 1 |
| `double_to_decimal` | `decimal` | `(double) -> decimal` | `DecimalTextRules.FromDouble`; `ConversionRange` for a NaN and ±∞ | `ConversionRange` | 4 |
| `string_to_decimal` | `decimal` | `(string) -> decimal` | `DecimalTextRules.TryParse` in the rounding mode | `InvalidArgument`, `ConversionRange` | 1 + arg ⌈Lₐ/10⌉ |
| `decimal_to_decimal` | `decimal` | `(decimal) -> decimal` | the argument itself, the same `CelValue` | — | 1 |
| `decimal_to_int64` | `int` | `(decimal) -> int` | `DecimalNumberRules.ToInt64` | `ConversionRange` | 1 |
| `decimal_to_uint64` | `uint` | `(decimal) -> uint` | `ToUInt64` | `ConversionRange` | 1 |
| `decimal_to_double` | `double` | `(decimal) -> double` | `DecimalTextRules.ToDouble` | `ConversionRange` | 1 |
| `decimal_to_string` | `string` | `(decimal) -> string` | `DecimalTextRules.Format` on the stack, then the string | — | 1 + text ⌈Lᵣ/10⌉ |

**Arithmetic.** The four operators read the two parts with `TryGetDecimal`, compute with `Decimal128`, and give the result to `CelValue.FromDecimal` after `Decimal128.IsFinite` says it is finite; an infinity — the only non-finite result for finite operands once a zero divisor is refused — is `Overflow` (3006) `decimal overflow`, with the place of the call. The quantum, the rounding and the clamp are those of "What `Decimal128` does": nothing in the implementation adjusts them, so `string(1.50m + 1.5m)` is `"3.00"` and `decimal('1') / decimal('3')` has 34 digits. A result that disappears below the exponent −6176 is a zero or a subnormal value without an error. `0.0m / 0.0m` and `1m / 0m` are `DivisionByZero` (3004) `division by zero`, since the divisor test comes first; so is `1m / -0.00m`. `decimal(x)` for a `decimal` is `decimal_to_decimal`, the identity: the cohort and the sign of a zero are those of `x`, so `string(decimal(1.50m))` is `"1.50"` and `string(decimal(-0.0m))` is `"-0.0"`.

**Orderings and equality.** The four orderings are `Decimal128` comparisons, so the cohort and the sign of a zero do not matter. Equality is not an overload of the set: `equals` and `not_equals` of the standard library compare two `decimal` values by the rule of [values](values.md#decimal). A comparison of a `decimal` with another type has no overload: it is a check error `NoMatchingOverload` (2001) without `dyn` and `EvalNoMatchingOverload` (3000) with it, `no such overload: _<_(decimal, int)`.

**Conversions.** The errors have the text forms of the other conversions of the library:

| Code | Text | When |
| :- | :- | :- |
| `ConversionRange` (3007) | `{value} is out of the range of {target}` | `int(x)` and `uint(x)` with the truncated value outside the type — `{value}` the text of `x` as `string(x)` writes it, `{target}` `int` or `uint`; `double(x)` beyond the range of `double`, `{target}` `double`; `decimal(x)` of a `double` NaN or infinity, `{value}` as `string(x)` writes it (`NaN`, `+Inf`, `-Inf`), `{target}` `decimal`; `decimal(s)` with the exponent out of range after rounding, `{value}` the string itself, `{target}` `decimal` |
| `InvalidArgument` (3008) | `invalid argument to 'decimal': cannot parse {text} as decimal` | `decimal(s)` with a string outside the grammar; `{text}` the string as it is |

`{value}` of a `decimal` is the text of `DecimalTextRules.Format`, which the error value formats when the message is read, from the value it holds ([evaluation](evaluation.md#errors-as-values)). The truncation, the ranges, the order of the checks for a string and the text of the exponent are on [decimal-text](decimal-text.md).

## The generator part

A member of the type `System.Decimal` or `Decimal128` in a type that the source generator reads is the concern of two parts that are written side by side, and the line between them is this one. **The generator part** writes everything that is in the generator: the row of the type mapping for the member — the field type `CelType.Decimal`, the read `FromDecimal(number, value)` for a `Decimal128` and `FromDecimal(value)` for a `System.Decimal`, the `Nullable<>` forms with the null handling of the other rows — the finding of `System.Numerics.Decimal128` in the compilation of the application, the exclusion of the member with the warning `GPCEL007` and the text of `GetExcludedFieldReason` when it is not there (`net10.0`), and the snapshots of the created text. **This part** writes what the created code calls and what meets it in the environment: `CelType.Decimal`, the bodies of the two `FromDecimal` and of `CelValue.FromDecimal`, the refusal of a decimal field in an environment without the set ([checking](checking.md#decimal-without-the-set)) whoever described the type, and 3012 for a boxed value. The one function the two share is `CheckErrors.FieldNotAccessible`, whose signature is on the page of the checker.

Nothing of this part waits for the generator. Its tests build the types they need with `CelObjectTypeBuilder` and with hand-written `CelObjectType` classes that declare fields of the type `CelType.Decimal` and read them with `FromDecimal`, so the part closes without a generated type. The test that reads a decimal member of a type from the generator, in `Great.Parts.Cel.Generator.Samples` under `#if NET11_0_OR_GREATER`, is written by whichever of the two parts reaches `main` later: the earlier one leaves the sample with checks of the created text only. The generator part does not wait for this one either: its snapshot of the text of a decimal member needs no running code.

## Cost

The units are those of the table; [library](library.md#cost-of-decimal) has the rows and the part that is charged inside the call. A decimal literal costs what any literal costs; the value of a `decimal` takes no memory beyond the 16 bytes of `CelValue`, and `string()` is the only overload that allocates.

## The literal and the flag

[syntax](syntax.md#decimal-literals): the two forms, the value, `InvalidLiteral`, `SyntaxNotEnabled`, the flag in `SyntaxOptions`, the printer. `EnableDecimalLiterals` without the set is `MissingExtensionDependency` (4005) at `Build`: [declarations](declarations.md#decimal-without-the-set-and-the-literal-option). A literal needs the flag and the flag needs the set; the function `decimal('12.34')` needs only the set.

## An environment without the set

Both targets give the same class of outcome, an error that names the set, and the same codes except where `net10.0` has no type at all.

| Expression or declaration | `net11.0` without the set | `net10.0` | Where |
| :- | :- | :- | :- |
| `12.34m`, `100m` | `SyntaxNotEnabled` (1007), `decimal literal is not enabled: enable decimal literals in the environment builder`, at the literal | the same code with `decimal literal is not enabled: decimal requires .NET 11` | the parser, existing |
| `decimal('1')`, `type(x) == decimal` | `UndeclaredReference` (2000) | the same | the checker, existing: the identifier and the function do not exist |
| a member of type `System.Decimal` or `Decimal128` selected | `FieldNotAccessible` (2007) at the selection, with the name of the set | by the generator: the member is not a field and its reason is a `GetExcludedFieldReason` text that says `decimal` needs .NET 11 | the checker ([checking](checking.md#decimal-without-the-set)); the generator belongs to the generator part |
| `AddVariable<System.Decimal>`, `AddVariable<Decimal128>`, a delegate with such a parameter or result | `TypeNotDeclared` (4008) at `Build`, `…; add the extension set 'decimal'` | the same code for `System.Decimal`, `…; the extension set 'decimal' exists only on .NET 11` | `DeclarationChecks`, the hint |
| a variable of the type `decimal` in a description | `TypeNotDeclared` (4008) with the position of the variable and the first hint | the same, with the second hint | `Build`; the reader gives `ObjectNamed("decimal")` |
| a value `System.Decimal` or `Decimal128` in a `dyn` place | `TypeNotAccessible` (3012) at the node that reads it | the same code and the same text | the reading node ([evaluation](evaluation.md#decimal-in-a-dyn-place)) |
| a `decimal` key in a map literal | `UnsupportedMapKeyType` (3027), `unsupported map key type 'decimal'`, with the set too | not reachable | the planner, existing |
| the flag of the literal | `MissingExtensionDependency` (4005) | not reachable: no method | `Build` |

## Differences from cel-go

None to compare: cel-go has no `decimal`. The rules that stand where a cel-go rule would: an abstract type is nullable in the checker, as every opaque type is there; `decimal` equals and orders only with `decimal`, so `dyn(1.0m) == 1` is `false` without an error and `price > 0` is a check error; a map key of this type is refused as one of any type outside `int`, `uint`, `bool` and `string`.

## Public documentation

| Page | Gets |
| :- | :- |
| `docs/decimal.md` (new) | sections, in this order: "The type" (the rule that it exists only on .NET 11, with the sentence "There is no `decimal` on .NET 10"; 34 digits; the cohort and `-0`), "Enabling it" (the set, `CelExtensions.Decimal(1)`, the flag, the two ways of writing a value), "Literals", "Operators and conversions" (the 18 overloads), "Text and parsing", "Equality and order", "Data of the application", "Without the set", "What version 1 does not have". Their content: the type and the rule that it exists only on .NET 11 (the sentence "There is no `decimal` on .NET 10"); the literal and its flag with `100m`, `0m`, `100M`, `1e2m`, `-0m` and what `string()` writes for each; the 18 overloads with the identifiers, the errors and the units; the text and the parsing rules of [decimal-text](decimal-text.md) as a user reads them — the grammar, 34 digits, half to even, the cohort, `-0`; equality and order, and why `price > 0` is an error; the rows of the type mapping in short; the environment without the set in both targets; what the set does not have — `%`, `round`, `quantize`, functions of other sets; one example for each part, each compiled by the checker of the examples |
| `docs/extensions.md` | the set in the catalog list, the sentence that it is only on `net11.0`, cannot be added by a description and has one version, 1; a pointer to `docs/decimal.md` |
| `docs/cost.md` | the rows of the 18 overloads with the units and the two with a text charge |
| `docs/type-mapping.md` | the rows `decimal` ↔ `Decimal128` and `System.Decimal`, input and result, the expected type, `Nullable<>`, the outcome of a `NaN` or an infinity, the rule of a typed collection, the table of the environment without the set |
| `docs/error-codes.md` | the texts of 2007, 3012 and 4008 for `decimal` are in the rows already; added: the second hint of 4008 and the reason of 2007 |
| `docs/environment-json.md` | one sentence: a description cannot name the set `decimal` or carry its flag; a variable can have the type `decimal` when the code added the set |
| `CHANGELOG.md` | one line for the set and one for the literal and the flag, next to the line of group 1 for the values and the type |

Related pages: [decimal-text](decimal-text.md), [values](values.md), [types](types.md), [syntax](syntax.md), [checking](checking.md), [evaluation](evaluation.md), [library](library.md), [extensions](extensions.md), [declarations](declarations.md), [configuration](configuration.md), [tests](tests.md).
