using System.Linq;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Checking;

// Name resolution in the container p.q: a name n is tried as p.q.n, p.n, then n, the first declared wins; a
// leading "." takes the name as written; comprehension variables shadow declarations; a chain of
// selections resolves its longest declared prefix. Each variable has a type of its own, so the type tells
// which declaration a name resolved to.
public sealed class ContainerResolutionTests
{
    private const string Container = "p.q";

    private static readonly DeclarationIndex InContainer = CheckingIndex.Create(
        [
            CheckingIndex.Global("p.q.g", "pqg_int", [CelType.Int], CelType.String),
            CheckingIndex.Global("g", "g_int", [CelType.Int], CelType.Bool),
            CheckingIndex.Global("p.h", "ph_int", [CelType.Int], CelType.Double),
            CheckingIndex.Receiver("p.q.twice", "pq_twice_int", CelType.Int, [], CelType.Int),
        ],
        [
            new CelVariableDecl("p.q.v", CelType.Int),
            new CelVariableDecl("p.v", CelType.UInt),
            new CelVariableDecl("v", CelType.String),
            new CelVariableDecl("p.only", CelType.Double),
            new CelVariableDecl("r", CelType.MapOf(CelType.String, CelType.MapOf(CelType.String, CelType.Bytes))),
            new CelVariableDecl("r.s", CelType.MapOf(CelType.String, CelType.Bool)),
            new CelVariableDecl("r.s.t", CelType.Null),
        ],
        Container);

    [Theory]
    [InlineData("v", "int", "p.q.v")]
    [InlineData(".v", "string", "v")]
    [InlineData("p.v", "uint", "p.v")]
    [InlineData(".p.v", "uint", "p.v")]
    [InlineData(".p.q.v", "int", "p.q.v")]
    [InlineData("only", "double", "p.only")]
    [InlineData("r.s.t", "null_type", "r.s.t")]
    [InlineData(".r.s.t", "null_type", "r.s.t")]
    public static void Name_ResolvesToTheFirstDeclaredCandidate(string expression, string type, string variable)
    {
        CheckResult result = Check(expression);

        Assert.True(result.IsSuccess);
        Assert.Equal(type, result.Expression.ResultType.ToString());
        Assert.Equal(variable, result.Expression.References[result.Expression.Parsed.Root.Id].Variable?.Name);
    }

    // The longest declared prefix of a chain wins; the selections after it select fields, and the nodes of
    // the prefix have no type or reference of their own.
    [Theory]
    [InlineData("r.s.u", "bool", "r.s")]
    [InlineData("r.x.y", "bytes", "r")]
    [InlineData("r['s'].t", "bytes", "r")]
    public static void Chain_ResolvesItsLongestDeclaredPrefix(string expression, string type, string variable)
    {
        CheckResult result = Check(expression);

        Assert.True(result.IsSuccess);
        Assert.Equal(type, result.Expression.ResultType.ToString());
        Assert.Equal([variable], result.Expression.References.Values.Select(reference => reference.Variable?.Name).Where(name => name is not null));
    }

    [Fact]
    public static void ResolvedPrefix_IsRecordedAtItsSelection()
    {
        CheckResult result = Check("r.s.u");

        Assert.True(result.IsSuccess);
        var outer = (SelectNode)result.Expression.Parsed.Root;
        var prefix = (SelectNode)outer.Operand;
        Assert.Equal("r.s", result.Expression.References[prefix.Id].Variable?.Name);
        Assert.Equal(CelType.MapOf(CelType.String, CelType.Bool), result.Expression.Types[prefix.Id]);
        Assert.False(result.Expression.Types.ContainsKey(prefix.Operand.Id));
        Assert.False(result.Expression.References.ContainsKey(outer.Id));
    }

    // A comprehension variable shadows every declaration; a leading "." skips it, and a chain that starts
    // with it is not a qualified name.
    [Theory]
    [InlineData("[true].map(v, v)", "list(bool)")]
    [InlineData("[true].map(v, .v)", "list(string)")]
    [InlineData("[{'s': 1u}].map(r, r.s)", "list(uint)")]
    [InlineData("[{'s': 1u}].map(r, .r.s.t)", "list(null_type)")]
    public static void ComprehensionVariable_ShadowsTheDeclarations(string expression, string type)
    {
        CheckResult result = Check(expression);

        Assert.True(result.IsSuccess);
        Assert.Equal(type, result.Expression.ResultType.ToString());
    }

    // A global call takes the candidates of the container; a target that, with the function, is a declared
    // qualified name makes a global call with the arguments only; receiver functions take the candidates too.
    [Theory]
    [InlineData("g(1)", "string", "pqg_int", false)]
    [InlineData(".g(1)", "bool", "g_int", false)]
    [InlineData("h(1)", "double", "ph_int", false)]
    [InlineData("p.h(1)", "double", "ph_int", false)]
    [InlineData("q.g(1)", "string", "pqg_int", false)]
    [InlineData(".p.q.g(1)", "string", "pqg_int", false)]
    [InlineData("1.twice()", "int", "pq_twice_int", true)]
    public static void Function_ResolvesByTheCandidatesOfTheContainer(string expression, string type, string overloadId, bool isReceiverStyle)
    {
        CheckResult result = Check(expression);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        Assert.Equal(type, result.Expression.ResultType.ToString());
        OverloadBinding candidate = Assert.Single(result.Expression.References[result.Expression.Parsed.Root.Id].Candidates);
        Assert.Equal((overloadId, isReceiverStyle), (candidate.Declaration.OverloadId, candidate.Declaration.IsReceiverStyle));
    }

    // The target of a qualified call is not checked: the undeclared names p and q make no error.
    [Fact]
    public static void TargetOfAQualifiedCall_IsPartOfTheName()
    {
        CheckResult result = Check("p.q.g(1)");

        Assert.True(result.IsSuccess);
        var call = (CallNode)result.Expression.Parsed.Root;
        Assert.False(result.Expression.Types.ContainsKey(call.Target!.Id));
    }

    [Fact]
    public static void TypeIdentifier_IsFoundAsTheLastCandidate()
    {
        CheckResult result = Check("int");

        Assert.True(result.IsSuccess);
        Assert.Equal(CelType.TypeOf(CelType.Int), result.Expression.ResultType);
    }

    [Theory]
    [InlineData("missing", 0, "missing")]
    [InlineData(".missing", 1, ".missing")]
    [InlineData("x.y.z", 0, "x")]
    [InlineData("missing(1)", 7, "missing")]
    [InlineData("1.missing()", 9, "missing")]
    public static void UndeclaredName_NamesTheContainer(string expression, int offset, string name)
    {
        CelError error = Assert.Single(Check(expression).Errors);

        Assert.Equal(
            (CelErrorCode.UndeclaredReference, offset, "undeclared reference to '" + name + "' (in container 'p.q')"),
            (error.Code, error.Location.Offset, error.Message));
    }

    private static CheckResult Check(string expression) => Checker.Check(CheckingIndex.Parse(expression), InContainer);
}
