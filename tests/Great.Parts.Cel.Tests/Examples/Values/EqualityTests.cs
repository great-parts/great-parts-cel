using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// CEL equality by the language definition, "Equality": int, uint and double on one number line, an int
// compared with a double as the int rounded to the nearest double; NaN equal to nothing; values of different
// non-numeric kinds unequal without an error; strings by code units, without normalization; lists and maps
// by their elements, maps by the key lookup of map keys; type values by kind and name. Operands of different
// static types need dyn: "1 == 1u" is a type error. Every row is the value cel-go v0.32.0 gives.
public sealed class EqualityTests
{
    private const string Cases = """
        dyn(1) == 1u => true
        dyn(1) == 1.0 => true
        dyn(1u) == 1.0 => true
        dyn(-1) == 18446744073709551615u => false
        dyn(-0.0) == 0 => true
        dyn(0.0) == 0u => true
        dyn(9007199254740993) == 9007199254740992.0 => true
        dyn(9223372036854775807) == 9223372036854775808.0 => true
        dyn(-9223372036854775808) == -9223372036854775808.0 => true
        dyn(18446744073709551615u) == 18446744073709551616.0 => true
        0.0 / 0.0 == 0.0 / 0.0 => false
        0.0 / 0.0 != 0.0 / 0.0 => true
        dyn('a') == 1 => false
        dyn(null) == 0 => false
        null == null => true
        [dyn(1)] == ['a'] => false
        [1, 2] == [dyn(1u), 2.0] => true
        [1, 2] == [1, 2, 3] => false
        [] == [] => true
        {} == {} => true
        {1: 'a'} == {dyn(1u): 'a'} => true
        {'a': 1, 'b': 2} == {'b': 2, 'a': 1} => true
        {'a': 1} == {'a': 2} => false
        {'a': 1} == {'b': 1} => false
        'á' == 'á' => false
        b'abc' == b'abc' => true
        type(1) == int => true
        type([1]) == list => true
        type({}) == map => true
        type(null) == null_type => true
        type(type(1)) == type => true
        type(1u) == type(1) => false
        """;

    private readonly CelEnvironment environment =
        new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    public static TheoryData<string, bool> Equalities
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (string line in Cases.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0))
            {
                string[] parts = line.Split(" => ");
                data.Add(parts[0], parts[1] == "true");
            }

            return data;
        }
    }

    [Theory]
    [Trait("Requirement", "F-1")]
    [MemberData(nameof(Equalities))]
    public void Equality_FollowsTheLanguageDefinition(string expression, bool expected)
    {
        Assert.Equal(expected, Evaluate(expression).GetValueOrThrow());
    }

    // An error value as an operand is the result of the comparison.
    [Fact]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-4")]
    public void ErrorOperand_IsTheResult()
    {
        CelError error = Assert.Single(Evaluate("1 / 0 == 1").Errors);

        Assert.Equal((CelErrorCode.DivisionByZero, CelErrorStage.Evaluate, 2), (error.Code, error.Stage, error.Location.Offset));
    }

    // CelValue.CelEquals is the same equality for the application; its result is a bool value.
    [Fact]
    [Trait("Requirement", "F-1")]
    public void CelEquals_IsCelEquality()
    {
        Assert.True(Bool(CelValue.CelEquals(CelValue.FromInt64(1), CelValue.FromUInt64(1))));
        Assert.True(Bool(CelValue.CelEquals(CelValue.FromUInt64(1), CelValue.FromDouble(1.0))));
        Assert.False(Bool(CelValue.CelEquals(CelValue.FromDouble(double.NaN), CelValue.FromDouble(double.NaN))));
        Assert.False(Bool(CelValue.CelEquals(CelValue.FromString("1"), CelValue.FromInt64(1))));
        Assert.True(Bool(CelValue.CelEquals(CelValue.Null, CelValue.Null)));
        Assert.True(Bool(CelValue.CelEquals(
            CelValue.FromList(CelList.Create(CelValue.FromInt64(1), CelValue.FromString("a"))),
            CelValue.FromList(CelList.Create(CelValue.FromDouble(1.0), CelValue.FromString("a"))))));
    }

    // .NET equality of CelValue is not CEL equality: the same kind and content, NaN equal to itself, strings
    // ordinal, bytes by content; lists and maps by reference.
    [Fact]
    [Trait("Requirement", "F-1")]
    public void DotNetEquality_ComparesKindAndContent()
    {
        CelList list = CelList.Create(CelValue.FromInt64(1));

        Assert.NotEqual(CelValue.FromInt64(1), CelValue.FromUInt64(1));
        Assert.Equal(CelValue.FromDouble(double.NaN), CelValue.FromDouble(double.NaN));
        Assert.Equal(CelValue.FromString("ab"), CelValue.FromString(string.Concat("a", "b")));
        Assert.Equal(CelValue.FromBytes(new byte[] { 1, 2 }), CelValue.FromBytes(new byte[] { 1, 2 }));
        Assert.Equal(CelValue.FromBytes(new byte[] { 1, 2 }).GetHashCode(), CelValue.FromBytes(new byte[] { 1, 2 }).GetHashCode());
        Assert.Equal(CelValue.FromList(list), CelValue.FromList(list));
        Assert.NotEqual(CelValue.FromList(list), CelValue.FromList(CelList.Create(CelValue.FromInt64(1))));
        Assert.True(CelValue.FromInt64(7) == CelValue.FromInt64(7));
        Assert.True(CelValue.FromInt64(7) != CelValue.FromDouble(7.0));
    }

    private static bool Bool(CelValue value)
    {
        Assert.True(value.TryGetBoolean(out bool result));
        return result;
    }

    private CelEvalResult<bool> Evaluate(string expression) =>
        environment.Compile<bool>(expression).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);
}
