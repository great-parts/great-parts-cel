# Extension set `network`, version 1

The set adds two abstract types, `net.IP` and `net.CIDR`, and 21 overloads over them. This page is also where the abstract types are defined: how a type and a value of that kind are represented in the type system and in `CelValue`, the public members that need, and the rules that the checker and the value rules apply to them. Identifiers and signatures are on the `docs/extensions.md` page, the costs are the rows of [library](library.md#cost-of-the-extension-sets), the set itself on [extensions](extensions.md). The behavior is that of `ext/network.go` of cel-go v0.32.0, which follows `net/netip` of Go 1.27.3 and Kubernetes.

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Values/AbstractValue.cs` | the internal base class of the values of an abstract type |
| `src/Great.Parts.Cel/Types/CelType.cs`, `CelTypeKind.cs` | the kind `Abstract` and an internal factory |
| `src/Great.Parts.Cel/Values/CelValueKind.cs`, `CelValue.cs` | the kind `Abstract` of a value |
| `src/Great.Parts.Cel/Library/Extensions/Network/NetworkExtension.cs` | the table: the two abstract types, 21 overloads |
| `src/Great.Parts.Cel/Library/Extensions/Network/NetworkAddress.cs`, `NetworkPrefix.cs` | the values of `net.IP` and `net.CIDR` |
| `src/Great.Parts.Cel/Library/Extensions/Network/AddressParser.cs`, `AddressText.cs` | the strict parser and the text forms |
| `src/Great.Parts.Cel/Library/Extensions/Network/NetworkFunctions.cs` | the implementations |
| `src/Great.Parts.Cel/Library/Extensions/Network/AddressBits.cs` | the 128 bits of an address and the flag of IPv4, a value type with no references |
| `src/Great.Parts.Cel/Library/Extensions/Network/NetworkTypes.cs` | the two instances of the abstract types of the set |
| `src/Great.Parts.Cel/Library/Extensions/Network/ParseFailure.cs` | the reasons of a parse failure as codes, from which the text is built when the error is read |

## Abstract types

**The public enumerations get one member each.** The kind of a type and of a value cannot be `Object`: `Object` means a type with fields, described by a source, and a program reads `CelType.Kind` to learn that; `optional_type` and `decimal` have kinds of their own for the same reason, and `checked.proto`, which the kinds follow, has `abstract_type`. A new kind is a non-breaking change that the documentation of both enumerations already announces:

```text
CelTypeKind.Abstract = 19     // numbers 17 and 18 are Optional and, on .NET 11 only, Decimal
CelValueKind.Abstract = 16    // 12 is Optional, 15 is Decimal on .NET 11 only
```

Both are in `PublicAPI/PublicAPI.Unshipped.txt`, the file common to both targets, not in the file of `net11.0`:

```text
Great.Parts.Cel.Types.CelTypeKind.Abstract = 19 -> Great.Parts.Cel.Types.CelTypeKind
Great.Parts.Cel.Values.CelValueKind.Abstract = 16 -> Great.Parts.Cel.Values.CelValueKind
```

The XML documentation of the members says: `CelTypeKind.Abstract` — "An abstract type of an extension set, such as <c>net.IP</c> of the <c>network</c> set: a named type that only the functions of its set create and read. <see cref="CelType.Name"/> is its full name."; `CelValueKind.Abstract` — "A value of an abstract type of an extension set, such as <c>net.IP</c>. This release has no <c>TryGet…</c> method for it: an expression reads it through the functions of its set, and an application receives it as a <see cref="CelValue"/>."; the remarks of `CelValue.Kind` name the kind next to `Decimal`, and the row of `CelValueKind` in `docs/api-concepts.md` adds `abstract` to the kinds `error` and `optional`. Nothing else is public: there is no factory of a `CelType` of this kind, no `CelValue.From…` or `TryGet…` for it, and no change of `CelExtension.Create`, which takes no abstract types in this release. A later minor version may add them; the kinds above are what it would use.

**The type.** `CelType` of the kind `Abstract` has `Name` the full name (`net.IP`), `Parameters` empty in this release, and the text form of its name; an abstract type with parameters, later, follows the rule of `optional_type(T)`: parameter by parameter. The internal factory `CelType.AbstractNamed(name, parameters)` creates it; the two types of the set are static instances made by the set. Equality, hash and text form are those of the existing model.

**Rules of the type** (`AssignabilityRules`; the numbers are those of [types](types.md)):

- Rule 4, `null`: an abstract type takes `null` and is taken by it, as `optional_type(T)` does; `ip('1.2.3.4') == null` compiles and is `false`, as in cel-go.
- Rule 8, equal types: two abstract types are assignable when their names are equal and their parameters are, one by one. An abstract type is assignable to no other kind and the other way round; `net.IP` and `net.CIDR` are not assignable, and `ip('::1') == '::1'` has no overload.
- `MostGeneral` and erasure follow the same rules; the join of a literal of `net.IP` values is `list(net.IP)`.

**Identifiers.** `ExtensionDefinition.AbstractTypes` of the set gives the declaration index the type identifiers `net.IP` and `net.CIDR`, each the way `int` is: a full name with the static type `type(T)` and the type value as its value ([checking](checking.md#extension-sets-in-the-index)). So `type(ip('1.2.3.4')) == net.IP` is `true`, and `net.IP` as an expression is a type value. A variable of the same name hides the identifier, as for a type of the application.

**Names in declarations.** A type record of a description, `ObjectNamed(name)` in code, is resolved at `Build` ([configuration](configuration.md#type-records)): a name that an abstract type of an added set carries gives that type, so `{"name": "a", "type": "net.IP"}` declares a variable of the abstract type once the environment has `network`. Without the set the name is `TypeNotDeclared` (4008) with the hint `; add the extension set 'network'`, which the code takes from a table of the catalog that maps each abstract type name to its set. An object type of a provider with the same name is `DuplicateType` (4001), the source `extension network v1` named next to the provider.

**The value.** `CelValue` of the kind `Abstract` holds an `AbstractValue` in its reference field. `AbstractValue` is an internal abstract class of `Values`:

```text
internal abstract class AbstractValue
    CelType Type { get; }
    abstract bool ContentEquals(AbstractValue other)    // same type and same content
    abstract int ContentHash()
```

The values of the set are two sealed subclasses in `Library.Extensions.Network`, one object each, at most 48 bytes: `NetworkAddress` with the 128 bits as two `ulong`s and a flag for IPv4, whose address sits in the low 32 bits; `NetworkPrefix` with the same address as written, with its host bits, the prefix length in a `byte` and the flag. An object of at most 64 bytes is within the budget of the one unit of `cidr_ip` and `cidr_masked`, the cheapest overloads that create one. The rest of the code treats it as a value of the kind `Abstract`:

| Rule | For the kind `Abstract` |
| :- | :- |
| `ValueTypeRules.TypeOf` | `AbstractValue.Type`, whose runtime name is the full name; `type(x) == net.IP` compares type values by kind and name |
| `ValueTypeRules.Fits` | an abstract type takes `null` and an abstract value whose type has the same name and parameters; nothing else |
| `EqualityRules` | equal when `ContentEquals`; a value of another kind, of the other abstract type and `null` are not equal, without an error; eq(a, b) is 1 |
| `OrderingRules` | none: `ip('1.1.1.1') < ip('1.1.1.2')` has no overload, and a `dyn` pair is `EvalNoMatchingOverload` |
| `KeyHashRules` | an abstract value is no map key: `UnsupportedMapKeyType` (3027), the type named by its full name |
| `CelValue.Equals`, `GetHashCode` | the same kind and `ContentEquals`; the hash from `ContentHash` |
| conversion to .NET | `T = CelValue`, and `T = object`, which gives the value as a boxed `CelValue`: the natural mapping has nothing else to give. Other `T` are `ResultConversion` (3015) |
| conversion from .NET | none: no .NET type maps to an abstract type in this release, so a variable cannot be bound to a value of this kind, and a variable of that type can only be declared. A `System.Net.IPAddress` is `TypeNotDeclared`, as any type outside the input rows |
| `string()`, `json.encode`, `format` | `string` has overloads in the set; `json.encode` and `%s` refuse the kind ([extension-library](extension-library.md#encoders), [extension-format](extension-format.md)) |
| `dyn` | an abstract value passes `dyn()` as any value does |

An application function that declares a parameter or a result of an abstract type gets `CelValue` arguments and returns `CelValue`; a typed delegate has no .NET type for it. The binding of `System.Net.IPAddress` and `System.Net.IPNetwork` is a candidate for a later minor version.

## Parsing

`AddressParser` is the strict parser, a port of `netip.ParseAddr` and `netip.ParsePrefix` with the three refusals of the set. It reads UTF-16 units; a unit above U+007F is an error anywhere. The kind of an address is decided by the first of `.`, `:` and `%` in the text: `.` — IPv4, `:` — IPv6, `%` or none — an error.

- **IPv4:** four decimal fields separated by `.`; each 1 to 3 digits, no leading zero unless the field is exactly `0`, at most 255; nothing before, between or after. `1.2.3`, `01.2.3.4`, `1.2.3.256`, `1.2.3.4.`, ` 1.2.3.4`, `1.2.3.4%eth0` and `0x1.2.3.4` are errors.
- **IPv6:** groups of 1 to 4 hexadecimal digits, either case, separated by `:`; at most one `::`, standing for at least one group of zeros, so that the groups make 8: `::` alone is the unspecified address, `1:2:3:4:5:6:7::` and `1:2:3:4:5:6::8` are valid, and `1:2:3:4:5:6:7:8::` is not; a single `:` at either end is an error; the last 32 bits may be written as a dotted IPv4 that follows the IPv4 rule, after a `:` and in place of the last two groups. A `%` anywhere is an error, whether a zone follows or not; brackets are errors. `1::2::3`, `12345::1`, `g::1`, `:1`, `1:`, `[::1]`, `1:2:3:4:5:6:7:8:9` are errors.
- **IPv4-mapped IPv6** — the first 80 bits zero and the next 16 ones, in any written form (`::ffff:1.2.3.4`, `::ffff:c0a8:1`, `::FFFF:0:0`) — is an error after parsing. The IPv4-compatible `::1.2.3.4` is valid and stays IPv6.
- **A prefix** is split at the last `/`; the part before it is an address by the rules above, the part after is 1 or more ASCII digits with no sign and no leading zero unless it is exactly `0`, at most 32 for IPv4 and 128 for IPv6. A prefix keeps the address as written with its host bits: `10.0.0.1/8` is valid. `10.0.0.0/33`, `/08`, `/+8`, `10.0.0.0/`, `10.0.0.0`, `::ffff:1.2.3.4/96`, `fe80::1%eth0/64`, `1.2.3.4/8/8` and `/8` are errors.

An error is `InvalidArgument` (3008), `invalid argument to '{function}': {detail}`, where the detail names the text and the reason of Go — `IP Address "1.2.3" parse error during conversion from string: IPv4 address too short`, `IPv4-mapped IPv6 address "…" is not allowed`, `CIDR "…" parse error during conversion from string: prefix length out of range`. The error value holds the text by reference and the reason as a code; the detail is formed when it is read, so a failed parse allocates one object. The functions `isIP` and `isCIDR` return `false` for any text that fails.

The `{reason}` of an address — `IP Address "…" parse error during conversion from string: {reason}`; for a prefix the text is `CIDR "…" …` — is one of the reasons of Go (`netip.ParseAddr`, `netip.ParsePrefix`), word for word: `unable to parse IP` (no `.`, `:` or `%`); `missing IPv6 address`; `IPv4 field must have at least one digit`; `IPv4 field has value >255`; `IPv4 field has octet with leading zero`; `IPv4 address too long`; `IPv4 address too short`; `unexpected character`; `each colon-separated field must have at least one digit`; `each colon-separated field must have at most 4 hex digits`; `embedded IPv4 address must replace the final 2 fields of the address`; `too many hex fields to fit an embedded IPv4 at the end of the address`; `unexpected character, want colon`; `colon must be followed by more characters`; `multiple :: in address`; `trailing garbage after address`; `address string too short`; `the :: must expand to at least one field of zeros`; `zone must be a non-empty string`; for a prefix also `no '/'`, `bad bits after slash` (followed by `: "{the text after /}"`) and `prefix length out of range`. The refusals of the set have no `parse error` prefix: `IPv4-mapped IPv6 address "…" is not allowed` and `IP Address "…" with zone value is not allowed` (`CIDR "…" with zone value is not allowed`).

**No check of constants at compilation.** cel-go rejects `ip('1.2.3')` and `cidr('x')` at compile time because it validates literal arguments; the library does not, so the call fails at evaluation, as the cel-spec cases of `network_ext` with constant arguments require. The records `string_to_ip(string) stage-mismatch` and `string_to_cidr(string) stage-mismatch` of the registry of known differences are this difference. A variable, `dyn('…')` or any other argument fails at evaluation on both sides and agrees.

## Text forms

`AddressText` writes an address as `netip.Addr.String()` does: IPv4 as four decimal fields; IPv6 in lower case with no leading zeros in a group, the longest run of two or more zero groups replaced by `::` — the first run when two are equally long, and none for a single zero group: `2001:0db8:0:0:0:0:0:1` is `2001:db8::1`, `1:0:0:2:0:0:0:3` is `1:0:0:2::3`, `1:2:3:4:5:6:7:0` is itself, `::1.2.3.4` is `::102:304`. A prefix is the address with `/` and the length: `string(cidr('10.0.0.1/8'))` is `10.0.0.1/8`.

## Overloads

| Overload | Behavior |
| :- | :- |
| `string_to_ip`, `string_to_cidr` | strict parse; an error as above |
| `ip_to_string`, `cidr_to_string` | the text form |
| `is_ip`, `is_cidr` | `true` when the strict parse succeeds; never an error; `isCIDR('10.0.0.1/8')` is `true` |
| `ip_is_canonical` | the strict parse, an error when it fails, then ordinal equality of the text with the text form of the result: `ip.isCanonical('2001:db8::1')` is `true`, `'2001:DB8::1'`, `'1:0:0:0:0:0:0:1'` and `'0:0:0:0:0:0:0:0'` are `false`, `'::'`, `'1::'` and `'1.2.3.4'` are `true`, `'::ffff:1.2.3.4'` and `'x'` are errors. Called as `ip.isCanonical(s)`, the qualified function name |
| `ip_family` | 4 or 6 |
| `ip_is_loopback` | IPv4 with the first byte 127; IPv6 `::1` |
| `ip_is_unspecified` | `0.0.0.0` or `::` |
| `ip_is_link_local_unicast` | IPv4 `169.254.0.0/16`; IPv6 `fe80::/10` |
| `ip_is_link_local_multicast` | IPv4 `224.0.0.0/24`; IPv6 when the first 16 bits `& 0xff0f` are `0xff02`: `ff02::`, `ff12::`, `ff32::` are, `ff01::` and `ff0e::` are not |
| `ip_is_global_unicast` | false for `0.0.0.0` and `255.255.255.255`, for the unspecified and loopback addresses, for multicast (IPv4 first byte `224`–`239`, IPv6 first byte `0xff`) and for link-local unicast; true otherwise, so `10.1.1.1`, `fc00::1`, `fec0::1` and `2001:db8::1` are true |
| `cidr_ip` | the address of the prefix with its host bits: `cidr('192.168.1.5/24').ip()` is `192.168.1.5` |
| `cidr_prefix_length` | the length |
| `cidr_is_mask` | true when the address has no host bits |
| `cidr_masked` | the prefix with the host bits of its address cleared, the same length |
| `cidr_contains_ip_ip` | true when the address is of the family of the prefix and its first `length` bits equal those of the prefix; another family is `false`, not an error |
| `cidr_contains_ip_string` | the strict parse of the string, an error when it fails, then as above |
| `cidr_contains_cidr` | true when the other prefix is of the same family, its length is not smaller, and its first `length` bits equal those of the prefix; the host bits of both are ignored: `cidr('10.0.0.5/8').containsCIDR('10.9.0.1/16')` is `true`; another family is `false` |
| `cidr_contains_cidr_string` | the strict parse of the string as a prefix, an error when it fails, then as above |

Equality of two values of `net.IP` is the same family and the same bits; of `net.CIDR`, the same family, the same address as written and the same length, so `cidr('10.0.0.1/8') == cidr('10.0.0.0/8')` is `false` and `cidr('10.0.0.1/8').masked() == cidr('10.0.0.0/8')` is `true`. An address of IPv4 and one of IPv6 are never equal.

**Cost.** 1 + ⌈Lₐ/10⌉ for the overloads that parse a string — `string_to_ip`, `string_to_cidr`, `is_ip`, `is_cidr`, `ip_is_canonical`, `cidr_contains_ip_string`, `cidr_contains_cidr_string` — and for `ip_to_string` and `cidr_to_string` 1 + ⌈Lᵣ/10⌉ with Lᵣ known before the text is made, from the family and the digits; 1 for the rest. A value is one object of at most 48 bytes, so no row needs more than the unit it has.

## Records and the list of skips

The registry of known differences has fourteen records for this increment: `string_index_of_string_int`, `string_last_index_of_string_int` ([extension-strings](extension-strings.md)), `string_to_cidr` and `string_to_ip`; `string_format` ([extension-format](extension-format.md)); `list_double_sort` and eight `list_<U>_sortByAssociatedKeys` ([extension-library](extension-library.md)). The case `network_ext/ip_type/is_ip_cidr_compile_error`, `isIP(cidr('192.168.0.0/24'))`, expects an evaluation error, while type checking rejects `isIP` applied to a `net.CIDR` in every implementation; it is the one entry of the skip list of the cel-spec suite that this increment keeps, with its reason, and the other 68 cases of `network_ext` pass ([tests](tests.md#conformance-runner-and-the-extension-sets)).

Related pages: [extensions](extensions.md), [checking](checking.md), [types](types.md), [values](values.md), [library](library.md), [tests](tests.md).
