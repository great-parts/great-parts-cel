using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library.Decimals;
using Great.Parts.Cel.Library.Extensions;

namespace Great.Parts.Cel;

/// <summary>
/// The catalog of the extension sets built into the library: one method per set, each taking the version
/// of the set explicitly.
/// </summary>
/// <remarks>
/// <para>
/// The catalog has the extension sets that the conformance tests of the CEL specification use —
/// <c>strings</c>, <c>math</c>, <c>lists</c>, <c>sets</c>, <c>encoders</c>, <c>bindings</c>,
/// <c>optional</c>, the two-variable comprehensions, <c>network</c> and <c>protos</c> — and
/// the <c>decimal</c>, <c>calendar</c> and <c>formats</c> sets of this library. Each set has the version number of the same set in
/// cel-go, the CEL implementation in Go, and its functions are those of that version in cel-go; <c>decimal</c>,
/// <c>calendar</c> and <c>formats</c>, which cel-go does not have, have their own numbers. The functions of a version never change:
/// updating the library does not change an environment
/// that names the same versions. This release has one version of each set, the version the method documents,
/// except <c>decimal</c>, which has versions 1 and 2. The methods do not check the version: a version the set does
/// not have is a build error
/// <see cref="CelErrorCode.UnknownExtensionVersion"/> that lists the available versions, reported when
/// the environment is built.
/// </para>
/// <para>
/// The name of each set is also its name in an environment description in JSON, except that <c>decimal</c>,
/// <c>calendar</c> and <c>formats</c> are added only in code. The functions and overloads of each version, with their overload
/// identifiers, are on the
/// page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/extensions.md">Extension sets</see>.
/// </para>
/// </remarks>
public static partial class CelExtensions
{
    // The names of the sets that an environment description in JSON accepts: those the library implements.
    internal static FrozenSet<string> Names => ExtensionCatalog.Default.Names;

    // The set of an environment description: the member of this class that has the name, called with the version.
    internal static CelExtension Create(string name, uint version) => ExtensionCatalog.Default.Get(name, version);

    /// <summary>
    /// The <c>strings</c> extension set: string functions such as <c>charAt</c>, <c>indexOf</c>,
    /// <c>lowerAscii</c>, <c>replace</c>, <c>split</c>, <c>substring</c>, <c>trim</c>, <c>join</c>,
    /// <c>format</c> and <c>strings.quote</c>. Version 5.
    /// </summary>
    /// <remarks>
    /// Positions and lengths count Unicode code points, as all CEL string functions do. A call of
    /// <c>format</c> whose format string is a constant and whose argument is a list literal is checked at
    /// compilation: an invalid format is the compilation error <see cref="CelErrorCode.InvalidFormatString"/>.
    /// </remarks>
    /// <param name="version">The version of the set.</param>
    /// <returns>The extension set.</returns>
    public static CelExtension Strings(uint version) => ExtensionCatalog.Default.Get("strings", version);

    /// <summary>
    /// The <c>math</c> extension set: the macros <c>math.greatest</c> and <c>math.least</c>, rounding of
    /// <c>double</c>, <c>math.abs</c>, <c>math.sign</c>, <c>math.sqrt</c>, tests for NaN and infinity, and
    /// bitwise operations on <c>int</c> and <c>uint</c>. Version 3.
    /// </summary>
    /// <param name="version">The version of the set.</param>
    /// <returns>The extension set.</returns>
    public static CelExtension Math(uint version) => ExtensionCatalog.Default.Get("math", version);

    /// <summary>
    /// The <c>lists</c> extension set: <c>lists.range</c>, <c>slice</c>, <c>flatten</c>, <c>reverse</c>,
    /// <c>distinct</c>, <c>sort</c> and the macro <c>sortBy</c>. Version 4.
    /// </summary>
    /// <param name="version">The version of the set.</param>
    /// <returns>The extension set.</returns>
    public static CelExtension Lists(uint version) => ExtensionCatalog.Default.Get("lists", version);

    /// <summary>
    /// The <c>sets</c> extension set: <c>sets.contains</c>, <c>sets.equivalent</c> and
    /// <c>sets.intersects</c> over lists. Version 0.
    /// </summary>
    /// <param name="version">The version of the set.</param>
    /// <returns>The extension set.</returns>
    public static CelExtension Sets(uint version) => ExtensionCatalog.Default.Get("sets", version);

    /// <summary>
    /// The <c>encoders</c> extension set: <c>base64.encode</c>, <c>base64.decode</c> and
    /// <c>json.encode</c>. Version 1.
    /// </summary>
    /// <param name="version">The version of the set.</param>
    /// <returns>The extension set.</returns>
    public static CelExtension Encoders(uint version) => ExtensionCatalog.Default.Get("encoders", version);

    /// <summary>
    /// The <c>bindings</c> extension set: the macro <c>cel.bind</c>, which binds a name to a value inside an
    /// expression. Version 1.
    /// </summary>
    /// <remarks>
    /// As in cel-go, version 1 also declares the function <c>cel.@block</c> (overload <c>cel_block_list</c>),
    /// which evaluates each of its slots lazily and at most once. Expression text cannot call it in this
    /// release: <c>@block</c> is not an identifier of the CEL syntax, and an application cannot declare a
    /// function with such a name.
    /// </remarks>
    /// <param name="version">The version of the set.</param>
    /// <returns>The extension set.</returns>
    public static CelExtension Bindings(uint version) => ExtensionCatalog.Default.Get("bindings", version);

    /// <summary>
    /// The <c>optional</c> extension set: the type <c>optional_type(T)</c>, the functions
    /// <c>optional.of</c>, <c>optional.none</c>, <c>optional.ofNonZeroValue</c>, <c>hasValue</c>,
    /// <c>value</c>, <c>or</c>, <c>orValue</c>, <c>first</c> and <c>last</c> of a list,
    /// <c>optional.unwrap</c> and <c>unwrapOpt</c>, the macros <c>optMap</c> and <c>optFlatMap</c>, and the
    /// optional syntax. Version 2.
    /// </summary>
    /// <remarks>
    /// The set enables the syntax <c>x.?f</c>, <c>x[?k]</c>, <c>[?e]</c>, <c>{?k: v}</c> and
    /// <c>T{?f: v}</c>; without the set such syntax is the parse error
    /// <see cref="CelErrorCode.SyntaxNotEnabled"/>. Values of the type are
    /// <see cref="Values.CelOptional{T}"/> in .NET.
    /// </remarks>
    /// <param name="version">The version of the set.</param>
    /// <returns>The extension set.</returns>
    public static CelExtension Optional(uint version) => ExtensionCatalog.Default.Get("optional", version);

    /// <summary>
    /// The two-variable comprehension macros: <c>all</c>, <c>exists</c> and <c>existsOne</c> with an index or
    /// key and a value, <c>transformList</c>, <c>transformMap</c> and <c>transformMapEntry</c>. Version 0.
    /// The name of the set in an environment description is <c>two-var-comprehensions</c>.
    /// </summary>
    /// <param name="version">The version of the set.</param>
    /// <returns>The extension set.</returns>
    public static CelExtension TwoVarComprehensions(uint version) => ExtensionCatalog.Default.Get("two-var-comprehensions", version);

    /// <summary>
    /// The <c>network</c> extension set: the abstract types <c>net.IP</c> and <c>net.CIDR</c>, the functions
    /// <c>ip</c>, <c>cidr</c>, <c>isIP</c>, <c>isCIDR</c> and their methods, such as <c>family</c>,
    /// <c>isLoopback</c>, <c>containsIP</c> and <c>masked</c>. Version 1.
    /// </summary>
    /// <param name="version">The version of the set.</param>
    /// <returns>The extension set.</returns>
    public static CelExtension Network(uint version) => ExtensionCatalog.Default.Get("network", version);

    /// <summary>
    /// The <c>protos</c> extension set: the macros <c>proto.getExt</c> and <c>proto.hasExt</c>, which read
    /// and test proto2 extension fields. Version 0.
    /// </summary>
    /// <remarks>
    /// The macros only rewrite the call into a selection of the extension field by its full name; the
    /// field itself comes from the protobuf type provider of the <c>Great.Parts.Cel.Protobuf</c> package,
    /// where the application registers the extension.
    /// </remarks>
    /// <param name="version">The version of the set.</param>
    /// <returns>The extension set.</returns>
    public static CelExtension Protos(uint version) => ExtensionCatalog.Default.Get("protos", version);

    /// <summary>
    /// The <c>decimal</c> extension set: the CEL type <c>decimal</c>, a decimal number with decimal arithmetic, comparison
    /// and conversions. Versions 1 and 2, on .NET 10 and .NET 11; cel-go has no such set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Version 1 has the operators <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c>, unary minus and the comparisons
    /// between two decimals, and the conversions <c>decimal()</c> from <c>int</c>, <c>uint</c>, <c>double</c>,
    /// <c>string</c> and <c>decimal</c>, <c>int()</c>, <c>uint()</c>, <c>double()</c> and <c>string()</c> of a
    /// decimal: 18 overloads on both target frameworks. Values are finite, so overflow and division by zero are errors. A
    /// decimal equals and orders only with a decimal: <c>price &gt; 0</c> is a type checking error, write
    /// <c>price &gt; 0m</c> or <c>price &gt; decimal(0)</c>.
    /// </para>
    /// <para>
    /// Version 2 keeps the 18 overloads of version 1 and adds rounding, 22 overloads: <c>x.round(n)</c> rounds to
    /// <c>n</c> digits after the decimal point without adding zeros, <c>x.quantize(q)</c> gives <c>x</c> the exponent of
    /// <c>q</c> (<c>(1.5m).quantize(0.01m)</c> is <c>1.50</c>; the value of <c>q</c> does not matter, so
    /// <c>quantize(0.05m)</c> rounds to hundredths, not to a step of 0.05). Both take an optional rounding mode:
    /// <c>'half_even'</c>, the default, <c>'half_up'</c> (half away from zero), <c>'half_down'</c>, <c>'down'</c>
    /// (toward zero), <c>'up'</c> (away from zero), <c>'floor'</c> and <c>'ceiling'</c>; another string is an evaluation
    /// error <see cref="CelErrorCode.InvalidArgument"/>. A result that needs more digits or a larger exponent than the
    /// platform has is <see cref="CelErrorCode.Overflow"/>. Version 1 does not change.
    /// </para>
    /// <para>
    /// The precision depends on the target framework. On .NET 11 a value is an IEEE 754 decimal128 number,
    /// <c>System.Numerics.Decimal128</c>, with 34 significant digits; on .NET 10 it is a <see cref="decimal"/>, with
    /// 28–29 significant digits and an exponent from −28 to 0. An inexact result is rounded to the precision of the
    /// platform, half to even; a literal or a result out of the precision or range of the platform is an error, not a
    /// rounded or infinite value. Equal values compare equal on both, but their text can differ: <c>string(1e2m)</c> is
    /// <c>"1E+2"</c> on .NET 11 and <c>"100"</c> on .NET 10. The page
    /// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/decimal.md">decimal</see> gives the rules.
    /// </para>
    /// <para>
    /// The set also makes members of the .NET type <see cref="decimal"/> and, on .NET 11,
    /// <c>System.Numerics.Decimal128</c> available to expressions. Decimal literals such as
    /// <c>12.34m</c> need the option <see cref="CelEnvironmentBuilder.EnableDecimalLiterals"/> as well. The set
    /// cannot be named in an environment description in JSON; add it in code. The set declares the type name
    /// <c>decimal</c>, so a variable or a type of the application with that name is the build error
    /// <see cref="CelErrorCode.DuplicateType"/> that names both sources.
    /// </para>
    /// </remarks>
    /// <param name="version">The version of the set.</param>
    /// <returns>The extension set.</returns>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifier contains type name",
        Justification = "The method is named after the CEL type decimal that the extension set adds.")]
    public static CelExtension Decimal(uint version) => DecimalExtension.Get(version);
}
