using System.Linq;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Checking;

// The static type of an expression, which the program reports as its result type: type parameters of
// overloads and their substitution, receiver and global calls, indexes, selections on maps and dyn, the
// join of literal parts, type values, and the standard macros. A type parameter still free at the end is dyn.
//
// The type rules follow the language definition and the cases of type_deduction.textproto. cel-go v0.32.0
// keeps one of two type values where they meet ("true ? type(1) : type(1u)" is type(uint) there), gives
// list(int) for "[1, w]" and dyn for the results of cast, first_elem_type and unwrap_type; static types are
// not part of the comparison with cel-go. The other rows are the types cel-go gives (a probe with the
// environment flags of the oracle).
//
// The functions of the application are those of type_deduction.textproto: cast(dyn, type(T)) -> T,
// first_elem_type(dyn, type(list(T))) -> T and unwrap_type(type(type(T))) -> type(T); first(list(T)) -> T
// and string.isInn() -> bool.
public sealed class ResultTypeTests
{
    private const string Cases = """
        [1, 2][0] => int
        {'abc': 123}['abc'] => int
        m['a'] => int
        m.a => int
        {'a': 1}.a => int
        {1: 2}.f => int
        has(m.a) => bool
        dyn([5])[0] => dyn
        dx.f => dyn
        dyn({'a': 1}).a => dyn
        dyn(1) => dyn
        dyn(1) < 1.5 => bool
        'abc'.size() => int
        size('abc') => int
        '0123456789'.isInn() => bool
        [] => list(dyn)
        {} => map(dyn, dyn)
        [] + [1] => list(int)
        [1, 'a'] => list(dyn)
        [1, null] => list(dyn)
        [w, 1] => list(wrapper(int))
        [1, w] => list(wrapper(int))
        true ? 1 : 2 => int
        true ? w : 42 => wrapper(int)
        false ? w : null => wrapper(int)
        int => type(int)
        list => type(list(dyn))
        map => type(map(dyn, dyn))
        type(1) == type(1u) => bool
        true ? type(1) : type(1u) => type(dyn)
        [int, type([])] => list(type(dyn))
        [type([]), type([1])] => list(type(dyn))
        [[type(1)], [type(1u)]] => list(list(type(dyn)))
        ([] + [[type(1)]]) + [[type([])]] => list(list(type(dyn)))
        cast('hello', int) => int
        cast(123, string) => string
        first_elem_type('data', type([1])) => int
        unwrap_type(type(int)) => type(int)
        first([1, 2]) => int
        first(['a']) => string
        first([]) => dyn
        [1, 2].map(e, e * 2) => list(int)
        [1, 2].map(e, e > 1, e * 2) => list(int)
        [1, 2].filter(e, e > 1) => list(int)
        {'a': 1}.map(k, k) => list(string)
        [[1], [2]].map(l, l.map(e, e + 1)) => list(list(int))
        [1].exists_one(e, e == 1) => bool
        dyn([1]).all(e, e > 0) => bool
        [].map(x, x.map(y, y)) => list(list(dyn))
        [].map(e, e.f) => list(dyn)
        [].exists(e, type(e) == int && e == 'a') => bool
        [].exists(e, e == 'a' && type(e) == int) => bool
        """;

    private static readonly CelFunctionImplementation First = arguments => arguments[0];

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("m", CelType.MapOf(CelType.String, CelType.Int))
        .AddVariable("w", CelType.WrapperOf(CelType.Int))
        .AddVariable("dx", CelType.Dyn)
        .AddFunction(new CelFunctionDecl(
            "cast",
            CelOverloadDecl.Global("cast_val_to_type", [CelType.Dyn, CelType.TypeOf(T)], T)))
        .AddFunction(new CelFunctionDecl(
            "first_elem_type",
            CelOverloadDecl.Global("first_elem_type_list", [CelType.Dyn, CelType.TypeOf(CelType.ListOf(T))], T)))
        .AddFunction(new CelFunctionDecl(
            "unwrap_type",
            CelOverloadDecl.Global("unwrap_type_t", [CelType.TypeOf(CelType.TypeOf(T))], CelType.TypeOf(T))))
        .AddFunction(new CelFunctionDecl("first", CelOverloadDecl.Global("first_list", [CelType.ListOf(T)], T)))
        .AddFunction(new CelFunctionDecl("isInn", CelOverloadDecl.Receiver("string_isInn", CelType.String, [], CelType.Bool)))
        .AddFunctionImplementation("cast_val_to_type", First)
        .AddFunctionImplementation("first_elem_type_list", First)
        .AddFunctionImplementation("unwrap_type_t", First)
        .AddFunctionImplementation("first_list", First)
        .AddFunctionImplementation("string_isInn", First)
        .Build()
        .GetEnvironmentOrThrow();

    public static TheoryData<string, string> Types
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (string line in Cases.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0))
            {
                string[] parts = line.Split(" => ");
                data.Add(parts[0], parts[1]);
            }

            return data;
        }
    }

    private static CelType T => CelType.TypeParameter("T");

    [Theory]
    [Trait("Requirement", "+R-10")]
    [Trait("Requirement", "F-1")]
    [MemberData(nameof(Types))]
    public void Expression_HasItsStaticType(string expression, string type)
    {
        CelProgram<CelValue> program = environment.Compile<CelValue>(expression).GetProgramOrThrow();

        Assert.Equal(type, program.ResultType.ToString());
    }

    // No overload of the function takes the static types of the arguments. The ordering across int, uint and
    // double needs an argument of the static type dyn; an index of a list is int; a receiver overload is not
    // a global one. The place is the call: the operator, the "[" of an index, the "(" of a global call.
    [Theory]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "+R-6")]
    [InlineData("1 < 1.5", 2, "_<_", "int, double")]
    [InlineData("1 == 1u", 2, "_==_", "int, uint")]
    [InlineData("[[1]] == [[1.0]]", 6, "_==_", "list(list(int)), list(list(double))")]
    [InlineData("1 in [1u]", 2, "@in", "int, list(uint)")]
    [InlineData("[1][1u]", 3, "_[_]", "list(int), uint")]
    [InlineData("isInn('x')", 5, "isInn", "string")]
    [InlineData("first(1)", 5, "first", "int")]
    public void ArgumentsThatNoCandidateTakes_AreNoMatchingOverloadAtTheCall(string expression, int offset, string function, string argumentTypes)
    {
        CelError error = Assert.Single(environment.Compile<CelValue>(expression).Errors);

        Assert.Equal(CelErrorCode.NoMatchingOverload, error.Code);
        Assert.Equal(CelErrorStage.Check, error.Stage);
        Assert.Equal(offset, error.Location.Offset);
        Assert.Equal("found no matching overload for '" + function + "' applied to '(" + argumentTypes + ")'", error.Message);
    }

    // The candidates that type checking accepts are the ones evaluation chooses from: with dyn operands the
    // ordering across numeric kinds has candidates and gives the answer of the values.
    [Theory]
    [Trait("Requirement", "+R-6")]
    [InlineData("dyn(1.5) <= 1u", false)]
    public void OrderingAcrossNumericKindsWithDyn_IsEvaluated(string expression, bool expected)
    {
        CelProgram<bool> program = environment.Compile<bool>(expression).GetProgramOrThrow();

        Assert.Equal(expected, program.Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // The identifier type denotes the type of all type values, type(dyn), so its static type is type(type(dyn)).
    [Fact]
    [Trait("Requirement", "+R-10")]
    public void IdentifierType_HasTheTypeOfTheTypeOfTypeValues()
    {
        CelType type = environment.Compile<CelValue>("type").GetProgramOrThrow().ResultType;

        Assert.Equal(CelType.TypeOf(CelType.TypeOf(CelType.Dyn)), type);
    }
}
