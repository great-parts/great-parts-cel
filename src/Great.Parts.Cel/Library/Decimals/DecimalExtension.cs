#if NET11_0_OR_GREATER
using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Library.Decimals;

// The definition of the set decimal, version 1: the type decimal and 18 overloads. It is not a CatalogExtension: the set has no
// macros and no optional syntax, and the flag of the literal is a flag of the builder, not of a set. The source is the text that
// build errors name, the same form as for the sets of the catalog.
internal sealed class DecimalDefinition(
    string name,
    uint version,
    string source,
    ImmutableArray<OverloadBinding> overloads,
    ImmutableArray<AbstractTypeDeclaration> abstractTypes)
    : ExtensionDefinition(name, version, source, overloads, abstractTypes);

// The table of the set decimal, version 1, in the order of its page: the four operators and the negation, the four orderings and the
// nine conversions. A call is 1 unit; decimal(double) is 4, since it reads the shortest text of a double and lays it out; the
// overload that reads a string adds ⌈Lₐ/10⌉ for its length, and the one that writes a string adds ⌈Lᵣ/10⌉ while it runs. The set is
// not a row of the catalog of the library: that would put its name among the names a description of an environment accepts, and a
// description cannot add it. This class keeps what a row keeps for one family, the one CelExtension of the one version.
internal static class DecimalExtension
{
    internal const string SetName = "decimal";
    internal const uint Version = 1;
    private const string Source = "extension decimal v1";

    // The cost of decimal(double): the shortest text of a double is read and laid out on the stack.
    private const ulong DoubleToDecimalCost = 4;

    private static readonly CelExtension VersionOne = new(
        SetName,
        Version,
        new DecimalDefinition(SetName, Version, Source, Overloads(), [new AbstractTypeDeclaration(SetName, CelType.Decimal)]),
        [Version]);

    // The set of the version: always the same instance, so that adding it twice is one set. A version the set does not have is a set
    // without a definition, which the build reports as UnknownExtensionVersion with the versions that exist.
    internal static CelExtension Get(uint version) =>
        version == Version ? VersionOne : new CelExtension(SetName, version, null, [Version]);

    private static ImmutableArray<OverloadBinding> Overloads()
    {
        CelType number = CelType.Decimal;
        return
        [
            Global("_+_", "add_decimal", [number, number], number, DecimalFunctions.Add),
            Global("_-_", "subtract_decimal", [number, number], number, DecimalFunctions.Subtract),
            Global("_*_", "multiply_decimal", [number, number], number, DecimalFunctions.Multiply),
            Global("_/_", "divide_decimal", [number, number], number, DecimalFunctions.Divide),
            Global("-_", "negate_decimal", [number], number, DecimalFunctions.Negate),
            Global("_<_", "less_decimal", [number, number], CelType.Bool, DecimalFunctions.Less),
            Global("_<=_", "less_equals_decimal", [number, number], CelType.Bool, DecimalFunctions.LessEquals),
            Global("_>_", "greater_decimal", [number, number], CelType.Bool, DecimalFunctions.Greater),
            Global("_>=_", "greater_equals_decimal", [number, number], CelType.Bool, DecimalFunctions.GreaterEquals),
            Global("decimal", "int64_to_decimal", [CelType.Int], number, DecimalFunctions.Int64ToDecimal),
            Global("decimal", "uint64_to_decimal", [CelType.UInt], number, DecimalFunctions.UInt64ToDecimal),
            Global("decimal", "double_to_decimal", [CelType.Double], number, DecimalFunctions.DoubleToDecimal, DoubleToDecimalCost),
            Global("decimal", "string_to_decimal", [CelType.String], number, DecimalFunctions.StringToDecimal, 1, DecimalCosts.Text),
            Global("decimal", "decimal_to_decimal", [number], number, DecimalFunctions.DecimalIdentity),
            Global("int", "decimal_to_int64", [number], CelType.Int, DecimalFunctions.DecimalToInt64),
            Global("uint", "decimal_to_uint64", [number], CelType.UInt, DecimalFunctions.DecimalToUInt64),
            Global("double", "decimal_to_double", [number], CelType.Double, DecimalFunctions.DecimalToDouble),
            Global("string", "decimal_to_string", [number], CelType.String, DecimalFunctions.DecimalToString),
        ];
    }

    // A function called as f(arguments), or an operator; none is called on a receiver.
    private static OverloadBinding Global(
        string function,
        string overloadId,
        ImmutableArray<CelType> parameterTypes,
        CelType resultType,
        OverloadImplementation implementation,
        ulong fixedCost = 1,
        CelCostFunction? argumentCost = null) =>
        new(function, new CelOverloadDecl(overloadId, false, parameterTypes, resultType), implementation, fixedCost, argumentCost, Source);
}
#endif
