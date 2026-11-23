using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Library.Decimals;

// The definition of the set decimal: the type decimal and the overloads of one version. It is not a CatalogExtension: the set has no
// macros and no optional syntax, and the flag of the literal is a flag of the builder, not of a set. The source is the text that
// build errors name, the same form as for the sets of the catalog.
internal sealed class DecimalDefinition(
    string name,
    uint version,
    string source,
    ImmutableArray<OverloadBinding> overloads,
    ImmutableArray<AbstractTypeDeclaration> abstractTypes)
    : ExtensionDefinition(name, version, source, FunctionDescriptions.Describe(overloads), abstractTypes);

// The table of the set decimal in the order of its page. Version 1 has 18 overloads: the four operators and the negation, the four
// orderings and the nine conversions. Version 2 has them and four more, round and quantize with and without a mode. A call is 1
// unit; decimal(double) is 4, since it reads the shortest text of a double and lays it out; the overload that reads a string adds
// ⌈Lₐ/10⌉ for its length, and the one that writes a string adds ⌈Lᵣ/10⌉ while it runs. The set is not a row of the catalog of the
// library: that would put its name among the names a description of an environment accepts, and a description cannot add it. This
// class keeps what a row keeps for one family, the CelExtension of each version.
internal static class DecimalExtension
{
    internal const string SetName = "decimal";
    private const uint VersionOneNumber = 1;
    private const uint VersionTwoNumber = 2;

    // The cost of decimal(double): the shortest text of a double is read and laid out on the stack.
    private const ulong DoubleToDecimalCost = 4;

    private static readonly ImmutableArray<uint> Versions = [VersionOneNumber, VersionTwoNumber];

    private static readonly CelExtension VersionOne = Create(VersionOneNumber, rounding: false);

    private static readonly CelExtension VersionTwo = Create(VersionTwoNumber, rounding: true);

    // The set of the version: always the same instance, so that adding it twice is one set. A version the set does not have is a set
    // without a definition, which the build reports as UnknownExtensionVersion with the versions that exist.
    internal static CelExtension Get(uint version) => version switch
    {
        VersionOneNumber => VersionOne,
        VersionTwoNumber => VersionTwo,
        _ => new CelExtension(SetName, version, null, Versions),
    };

    private static CelExtension Create(uint version, bool rounding)
    {
        string source = "extension decimal v" + (version == VersionOneNumber ? "1" : "2");
        ImmutableArray<OverloadBinding> overloads = Overloads(source, rounding);
        return new CelExtension(
            SetName,
            version,
            new DecimalDefinition(SetName, version, source, overloads, [new AbstractTypeDeclaration(SetName, CelType.Decimal)]),
            Versions);
    }

    private static ImmutableArray<OverloadBinding> Overloads(string source, bool rounding)
    {
        CelType number = CelType.Decimal;
        ImmutableArray<OverloadBinding> first =
        [
            Global(source, "_+_", "add_decimal", [number, number], number, DecimalFunctions.Add),
            Global(source, "_-_", "subtract_decimal", [number, number], number, DecimalFunctions.Subtract),
            Global(source, "_*_", "multiply_decimal", [number, number], number, DecimalFunctions.Multiply),
            Global(source, "_/_", "divide_decimal", [number, number], number, DecimalFunctions.Divide),
            Global(source, "-_", "negate_decimal", [number], number, DecimalFunctions.Negate),
            Global(source, "_<_", "less_decimal", [number, number], CelType.Bool, DecimalFunctions.Less),
            Global(source, "_<=_", "less_equals_decimal", [number, number], CelType.Bool, DecimalFunctions.LessEquals),
            Global(source, "_>_", "greater_decimal", [number, number], CelType.Bool, DecimalFunctions.Greater),
            Global(source, "_>=_", "greater_equals_decimal", [number, number], CelType.Bool, DecimalFunctions.GreaterEquals),
            Global(source, "decimal", "int64_to_decimal", [CelType.Int], number, DecimalFunctions.Int64ToDecimal),
            Global(source, "decimal", "uint64_to_decimal", [CelType.UInt], number, DecimalFunctions.UInt64ToDecimal),
            Global(source, "decimal", "double_to_decimal", [CelType.Double], number, DecimalFunctions.DoubleToDecimal, DoubleToDecimalCost),
            Global(source, "decimal", "string_to_decimal", [CelType.String], number, DecimalFunctions.StringToDecimal, 1, DecimalCosts.Text),
            Global(source, "decimal", "decimal_to_decimal", [number], number, DecimalFunctions.DecimalIdentity),
            Global(source, "int", "decimal_to_int64", [number], CelType.Int, DecimalFunctions.DecimalToInt64),
            Global(source, "uint", "decimal_to_uint64", [number], CelType.UInt, DecimalFunctions.DecimalToUInt64),
            Global(source, "double", "decimal_to_double", [number], CelType.Double, DecimalFunctions.DecimalToDouble),
            Global(source, "string", "decimal_to_string", [number], CelType.String, DecimalFunctions.DecimalToString),
        ];

        if (!rounding)
        {
            return first;
        }

        return
        [
            .. first,
            Member(source, "round", "decimal_round_int64", [number, CelType.Int], number, DecimalFunctions.Round),
            Member(source, "round", "decimal_round_int64_string", [number, CelType.Int, CelType.String], number, DecimalFunctions.RoundWithMode, DecimalFunctions.ModeArgument),
            Member(source, "quantize", "decimal_quantize_decimal", [number, number], number, DecimalFunctions.Quantize),
            Member(source, "quantize", "decimal_quantize_decimal_string", [number, number, CelType.String], number, DecimalFunctions.QuantizeWithMode, DecimalFunctions.ModeArgument),
        ];
    }

    // A function called as f(arguments), or an operator.
    private static OverloadBinding Global(
        string source,
        string function,
        string overloadId,
        ImmutableArray<CelType> parameterTypes,
        CelType resultType,
        OverloadImplementation implementation,
        ulong fixedCost = 1,
        CelCostFunction? argumentCost = null) =>
        new(function, new CelOverloadDecl(overloadId, false, parameterTypes, resultType), implementation, fixedCost, argumentCost, source);

    // A function called on its receiver, which is the first parameter; a mode that is a string constant is prepared once.
    private static OverloadBinding Member(
        string source,
        string function,
        string overloadId,
        ImmutableArray<CelType> parameterTypes,
        CelType resultType,
        OverloadImplementation implementation,
        ConstantArgument? constantArgument = null) =>
        new(function, new CelOverloadDecl(overloadId, true, parameterTypes, resultType), implementation, 1, null, source, constantArgument: constantArgument);
}
