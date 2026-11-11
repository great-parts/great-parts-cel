using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Library.Extensions;

// The table of the extension set math, version 3: 58 overloads with their identifiers as in cel-go, each with its implementation
// and its cost from the cost page. A call is 1 unit; the list forms of math.@max and math.@min add one unit for each element,
// which the call node charges before the function runs. The functions math.@max and math.@min are those that the macros
// math.greatest and math.least expand to: no text can write their names. The macros are in Syntax.
internal static class MathExtension
{
    internal const string SetName = "math";
    internal const uint Version = 3;
    private const string Source = "extension math v3";

    private static readonly CelType DoubleType = CelType.Double;
    private static readonly CelType IntType = CelType.Int;
    private static readonly CelType UIntType = CelType.UInt;

    // The definition of the version, or null for a version the set does not have. The set brings the macros math.greatest and
    // math.least.
    internal static CatalogExtension? Create(uint version) =>
        version == Version ? new CatalogExtension(SetName, Version, Overloads(), macros: ExtensionMacros.Math) : null;

    private static ImmutableArray<OverloadBinding> Overloads()
    {
        ImmutableArray<OverloadBinding>.Builder table = ImmutableArray.CreateBuilder<OverloadBinding>(58);
        AddExtremes(table, "math.@max", "math_@max", MathFunctions.Max);
        AddExtremes(table, "math.@min", "math_@min", MathFunctions.Min);
        table.Add(Unary("math.ceil", "math_ceil_double", DoubleType, DoubleType, MathFunctions.Ceil));
        table.Add(Unary("math.floor", "math_floor_double", DoubleType, DoubleType, MathFunctions.Floor));
        table.Add(Unary("math.round", "math_round_double", DoubleType, DoubleType, MathFunctions.Round));
        table.Add(Unary("math.trunc", "math_trunc_double", DoubleType, DoubleType, MathFunctions.Trunc));
        table.Add(Unary("math.isInf", "math_isInf_double", DoubleType, CelType.Bool, MathFunctions.IsInf));
        table.Add(Unary("math.isNaN", "math_isNaN_double", DoubleType, CelType.Bool, MathFunctions.IsNaN));
        table.Add(Unary("math.isFinite", "math_isFinite_double", DoubleType, CelType.Bool, MathFunctions.IsFinite));
        table.Add(Unary("math.abs", "math_abs_double", DoubleType, DoubleType, MathFunctions.Abs));
        table.Add(Unary("math.abs", "math_abs_int", IntType, IntType, MathFunctions.Abs));
        table.Add(Unary("math.abs", "math_abs_uint", UIntType, UIntType, MathFunctions.Abs));
        table.Add(Unary("math.sign", "math_sign_double", DoubleType, DoubleType, MathFunctions.Sign));
        table.Add(Unary("math.sign", "math_sign_int", IntType, IntType, MathFunctions.Sign));
        table.Add(Unary("math.sign", "math_sign_uint", UIntType, UIntType, MathFunctions.Sign));
        AddBinary(table, "math.bitAnd", "math_bitAnd", MathFunctions.BitAnd);
        AddBinary(table, "math.bitOr", "math_bitOr", MathFunctions.BitOr);
        AddBinary(table, "math.bitXor", "math_bitXor", MathFunctions.BitXor);
        table.Add(Unary("math.bitNot", "math_bitNot_int_int", IntType, IntType, MathFunctions.BitNot));
        table.Add(Unary("math.bitNot", "math_bitNot_uint_uint", UIntType, UIntType, MathFunctions.BitNot));
        table.Add(Binary("math.bitShiftLeft", "math_bitShiftLeft_int_int", IntType, IntType, IntType, MathFunctions.BitShiftLeft));
        table.Add(Binary("math.bitShiftLeft", "math_bitShiftLeft_uint_int", UIntType, IntType, UIntType, MathFunctions.BitShiftLeft));
        table.Add(Binary("math.bitShiftRight", "math_bitShiftRight_int_int", IntType, IntType, IntType, MathFunctions.BitShiftRight));
        table.Add(Binary("math.bitShiftRight", "math_bitShiftRight_uint_int", UIntType, IntType, UIntType, MathFunctions.BitShiftRight));
        table.Add(Unary("math.sqrt", "math_sqrt_double", DoubleType, DoubleType, MathFunctions.Sqrt));
        table.Add(Unary("math.sqrt", "math_sqrt_int", IntType, DoubleType, MathFunctions.Sqrt));
        table.Add(Unary("math.sqrt", "math_sqrt_uint", UIntType, DoubleType, MathFunctions.Sqrt));
        return table.MoveToImmutable();
    }

    // The 15 overloads of math.@max or of math.@min: one number, two numbers of one kind, two numbers of different kinds with the
    // result dyn, and a list of numbers. The list forms read the elements themselves, so the planner marks no argument for the
    // check at depth, and they cost 1 + n.
    private static void AddExtremes(ImmutableArray<OverloadBinding>.Builder table, string function, string prefix, OverloadImplementation implementation)
    {
        table.Add(Unary(function, prefix + "_double", DoubleType, DoubleType, implementation));
        table.Add(Unary(function, prefix + "_int", IntType, IntType, implementation));
        table.Add(Unary(function, prefix + "_uint", UIntType, UIntType, implementation));
        table.Add(Binary(function, prefix + "_double_double", DoubleType, DoubleType, DoubleType, implementation));
        table.Add(Binary(function, prefix + "_int_int", IntType, IntType, IntType, implementation));
        table.Add(Binary(function, prefix + "_uint_uint", UIntType, UIntType, UIntType, implementation));
        table.Add(Binary(function, prefix + "_int_uint", IntType, UIntType, CelType.Dyn, implementation));
        table.Add(Binary(function, prefix + "_int_double", IntType, DoubleType, CelType.Dyn, implementation));
        table.Add(Binary(function, prefix + "_double_int", DoubleType, IntType, CelType.Dyn, implementation));
        table.Add(Binary(function, prefix + "_double_uint", DoubleType, UIntType, CelType.Dyn, implementation));
        table.Add(Binary(function, prefix + "_uint_int", UIntType, IntType, CelType.Dyn, implementation));
        table.Add(Binary(function, prefix + "_uint_double", UIntType, DoubleType, CelType.Dyn, implementation));
        table.Add(List(function, prefix + "_list_double", DoubleType, implementation));
        table.Add(List(function, prefix + "_list_int", IntType, implementation));
        table.Add(List(function, prefix + "_list_uint", UIntType, implementation));
    }

    // The bit operation on two ints and on two uints.
    private static void AddBinary(ImmutableArray<OverloadBinding>.Builder table, string function, string prefix, OverloadImplementation implementation)
    {
        table.Add(Binary(function, prefix + "_int_int", IntType, IntType, IntType, implementation));
        table.Add(Binary(function, prefix + "_uint_uint", UIntType, UIntType, UIntType, implementation));
    }

    // A global overload of 1 unit.
    private static OverloadBinding Unary(string function, string overloadId, CelType parameter, CelType result, OverloadImplementation implementation) =>
        new(function, new CelOverloadDecl(overloadId, false, [parameter], result), implementation, 1, null, Source);

    private static OverloadBinding Binary(
        string function,
        string overloadId,
        CelType first,
        CelType second,
        CelType result,
        OverloadImplementation implementation) =>
        new(function, new CelOverloadDecl(overloadId, false, [first, second], result), implementation, 1, null, Source);

    // A list form: 1 unit and the number of elements, and the elements are checked by the implementation.
    private static OverloadBinding List(string function, string overloadId, CelType element, OverloadImplementation implementation) =>
        new(
            function,
            new CelOverloadDecl(overloadId, false, [CelType.ListOf(element)], element),
            implementation,
            1,
            Costs.ListSizes,
            Source,
            shallowArguments: true);
}
