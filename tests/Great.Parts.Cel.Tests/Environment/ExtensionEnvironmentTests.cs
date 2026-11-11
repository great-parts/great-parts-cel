using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Declarations.ExtensionTestSets;

namespace Great.Parts.Cel.Tests.Environment;

// What an environment takes from its extension sets: the syntax of the environment with the macros of the sets, the overloads
// as candidates in the order of the sets, the variables, and the functions called through the whole path of compilation and
// evaluation. The sets are made by the tests.
public sealed class ExtensionEnvironmentTests
{
    private static readonly CelFunctionImplementation One = static arguments => CelValue.FromInt64(arguments.Length + 1);

    [Fact]
    public void EnvironmentWithoutSets_HasTheStandardSyntax()
    {
        CelEnvironment environment = BuildEnvironment(Builder());

        Assert.Same(SyntaxOptions.Standard, environment.Syntax);
    }

    [Fact]
    public void SetsThatBringNeitherMacrosNorSyntax_LeaveTheStandardSyntax()
    {
        CelEnvironment environment = BuildEnvironment(Builder().AddExtension(Of(Set("alpha", 1))));

        Assert.Same(SyntaxOptions.Standard, environment.Syntax);
    }

    [Fact]
    public void FlagOfASet_TurnsOnTheOptionalSyntaxAndKeepsTheStandardMacros()
    {
        CatalogExtension alpha = new("alpha", 1, [], optionalSyntax: true);

        CelEnvironment environment = BuildEnvironment(Builder().AddExtension(Of(alpha)));

        Assert.True(environment.Syntax.OptionalSyntax);
        Assert.Equal(MacroNames(MacroTable.Standard), MacroNames(environment.Syntax.Macros));
    }

    [Fact]
    public void MacroTable_HasTheStandardMacrosThenThoseOfTheSetsInTheOrderOfNamesAndVersions()
    {
        CelEnvironmentBuilder builder = Builder()
            .AddExtension(Of(Set("gamma", 1, macros: [Counting()])))
            .AddExtension(Of(Set("alpha", 1, macros: [Successor()])))
            .AddExtension(Of(Set("beta", 1, macros: [Failing()])));

        string[] names = MacroNames(BuildEnvironment(builder).Syntax.Macros);

        Assert.Equal([.. MacroNames(MacroTable.Standard), "succ", "fail", "count"], names);
    }

    [Fact]
    public void MacroOfASet_IsExpandedOnlyInAnEnvironmentThatHasTheSet()
    {
        CelEnvironment with = BuildEnvironment(Builder().AddExtension(Of(Set("alpha", 1, macros: [Successor()]))));
        CelEnvironment without = BuildEnvironment(Builder());

        Assert.Equal(3, Evaluate(with, "test.succ(2)"));
        CelCompileResult<CelValue> compiled = without.Compile<CelValue>("test.succ(2)");
        Assert.False(compiled.IsSuccess);
        Assert.Equal(CelErrorCode.UndeclaredReference, Assert.Single(compiled.Errors).Code);
    }

    [Fact]
    public void MacroOfASetWithANamespace_LeavesACallOnAnotherTargetAsItIs()
    {
        CelEnvironment environment = BuildEnvironment(Builder().AddExtension(Of(Set("alpha", 1, macros: [Successor()]))));

        ParseResult parsed = environment.Parse("other.succ(2)");

        Assert.True(parsed.IsSuccess);
        CallNode call = Assert.IsType<CallNode>(parsed.Expression!.Root);
        Assert.Equal("succ", call.Function);
        Assert.IsType<IdentNode>(call.Target);
    }

    [Fact]
    public void ErrorOfAMacroOfASet_IsInvalidMacroCallAtTheNodeTheMacroNames()
    {
        CelEnvironment environment = BuildEnvironment(Builder().AddExtension(Of(Set("alpha", 1, macros: [Failing()]))));
        const string Text = "test.fail(x)";

        ParseResult parsed = environment.Parse(Text);

        Assert.False(parsed.IsSuccess);
        CelError error = Assert.Single(parsed.Errors);
        Assert.Equal(CelErrorCode.InvalidMacroCall, error.Code);
        Assert.Equal("invalid call of macro 'fail': it always fails", error.Message);
        Assert.Equal(Text.IndexOf('x', StringComparison.Ordinal), error.Location.Offset);
    }

    [Fact]
    public void MacroOfAnyNumberOfArguments_IsFoundForEveryCount()
    {
        CelEnvironment environment = BuildEnvironment(Builder().AddExtension(Of(Set("alpha", 1, macros: [Counting()]))));

        Assert.Equal(0, Evaluate(environment, "test.count()"));
        Assert.Equal(1, Evaluate(environment, "test.count(1)"));
        Assert.Equal(3, Evaluate(environment, "test.count(1, 2, 3)"));
    }

    [Fact]
    public void MacroOfTheExactNumber_WinsOverTheMacroOfAnyNumber()
    {
        Macro exact = new("count", "test", 1, isReceiver: true, static (_, _) => MacroCheck.Expand, static (nodes, _, _, offset) => nodes.Constant(CelValue.Null, offset));
        MacroTable table = MacroTable.Create([Counting(), exact, Successor()]);

        Assert.Equal(1, table.Find("count", 1, isReceiver: true)!.ArgumentCount);
        Assert.Equal(Macro.AnyArgumentCount, table.Find("count", 4, isReceiver: true)!.ArgumentCount);
        Assert.Equal(Macro.AnyArgumentCount, table.Find("count", 0, isReceiver: true)!.ArgumentCount);
        Assert.Null(table.Find("succ", 2, isReceiver: true));
        Assert.Null(table.Find("succ", 1, isReceiver: false));
    }

    [Fact]
    public void MacroTable_RefusesTwoMacrosWithOneKey()
    {
        Assert.Throws<ArgumentException>(() => MacroTable.Create([Successor(), Successor()]));
        Assert.Throws<ArgumentException>(() => MacroTable.Create([Counting(), Counting()]));
        MacroTable.Create([.. MacroTable.Standard.Macros.ToArray(), Successor(), Failing(), Counting()]);
    }

    [Fact]
    public void FunctionOfASet_IsCompiledAndEvaluatedAsAnyFunction()
    {
        OverloadImplementation twice = static (ReadOnlySpan<CelValue> arguments, in CallSite _) =>
            arguments[0].TryGetInt64(out long value) ? CelValue.FromInt64(value * 2) : CelValue.Null;
        OverloadImplementation shout = static (ReadOnlySpan<CelValue> arguments, in CallSite _) =>
            arguments[0].TryGetString(out string? value) ? CelValue.FromString(value + "!") : CelValue.Null;
        CatalogExtension alpha = Set(
            "alpha",
            1,
            [
                Global(Source("alpha", 1), "twice", "alpha_twice", [CelType.Int], CelType.Int, twice),
                Receiver(Source("alpha", 1), "shout", "alpha_shout", CelType.String, [], CelType.String, shout),
            ]);

        CelEnvironment environment = BuildEnvironment(Builder().AddExtension(Of(alpha)));

        Assert.Equal(42, Evaluate(environment, "twice(21)"));
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>("'hey'.shout()");
        Assert.True(compiled.IsSuccess);
        Assert.True(Run(compiled, new CelBindings()).GetValueOrThrow().TryGetString(out string? text));
        Assert.Equal("hey!", text);
        Assert.False(BuildEnvironment(Builder()).Compile<CelValue>("twice(21)").IsSuccess);
    }

    [Fact]
    public void FunctionOfAnApplicationSet_RunsWithTheCheckOfItsTypes()
    {
        CelExtension application = CelExtension.Create(
            "app",
            1,
            [Function("triple", CelOverloadDecl.Global("triple_int", [CelType.Int], CelType.Int))],
            new Dictionary<string, CelFunctionImplementation>
            {
                ["triple_int"] = static arguments => arguments[0].TryGetInt64(out long value) ? CelValue.FromInt64(value * 3) : CelValue.Null,
            });

        CelEnvironment environment = BuildEnvironment(Builder().AddExtension(application));

        Assert.Equal(21, Evaluate(environment, "triple(7)"));
        Assert.True(Assert.Single(environment.Declarations.GetOverloads("triple", 1)).IsApplication);
    }

    // The candidates of a call are in the order of the sets, by name and version, then the application, whatever the order of
    // the calls of the builder.
    [Fact]
    public void Candidates_AreInTheOrderOfTheSetsThenTheApplication()
    {
        CatalogExtension alpha = Set("alpha", 1, [Global(Source("alpha", 1), "pick", "alpha_pick", [CelType.Int], CelType.Int)]);
        CatalogExtension beta = Set("beta", 1, [Global(Source("beta", 1), "pick", "beta_pick", [CelType.String], CelType.Int)]);
        CelFunctionDecl declaration = Function("pick", CelOverloadDecl.Global("app_pick", [CelType.Bytes], CelType.Int));
        string[] expected = ["alpha_pick", "beta_pick", "app_pick"];

        CelEnvironment first = BuildEnvironment(
            Builder().AddExtension(Of(beta)).AddFunction(declaration).AddFunctionImplementation("app_pick", One).AddExtension(Of(alpha)));
        CelEnvironment second = BuildEnvironment(
            Builder().AddFunction(declaration).AddFunctionImplementation("app_pick", One).AddExtension(Of(alpha)).AddExtension(Of(beta)));

        Assert.Equal(expected, Ids(first, "pick", 1));
        Assert.Equal(expected, Ids(second, "pick", 1));
    }

    [Fact]
    public void FunctionNamesOfTheSets_ArePartOfTheIndexIncludingQualifiedOnes()
    {
        CatalogExtension alpha = Set("alpha", 1, [Global(Source("alpha", 1), "math.@max", "alpha_max", [CelType.Int], CelType.Int)]);

        CelEnvironment environment = BuildEnvironment(Builder().AddExtension(Of(alpha)));

        Assert.True(environment.Declarations.IsFunction("math.@max"));
        Assert.False(BuildEnvironment(Builder()).Declarations.IsFunction("math.@max"));
    }

    [Fact]
    public void VariableOfASet_IsReadByAnExpression()
    {
        CatalogExtension alpha = Set("alpha", 1, variables: [new CelVariableDecl("alpha_seed", CelType.Int)]);

        CelEnvironment environment = BuildEnvironment(Builder().AddExtension(Of(alpha)));
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>("alpha_seed + 1");

        Assert.True(compiled.IsSuccess);
        Assert.True(Run(compiled, new CelBindings().Add("alpha_seed", 41L)).GetValueOrThrow().TryGetInt64(out long value));
        Assert.Equal(42, value);
    }

    // With ShallowArguments no argument is marked for the check at depth: a dyn list of mixed elements reaches the
    // implementation, which reads the elements itself; without it the same call has no matching overload.
    [Fact]
    public void ShallowArguments_MarkNoArgumentForTheCheckAtDepth()
    {
        OverloadImplementation size = static (ReadOnlySpan<CelValue> arguments, in CallSite _) =>
            arguments[0].TryGetList(out CelList? list) ? CelValue.FromInt64(list.Count) : CelValue.Null;
        CatalogExtension shallow = Set("alpha", 1, [Global(Source("alpha", 1), "len", "alpha_len", [CelType.ListOf(CelType.Int)], CelType.Int, size, shallow: true)]);
        CatalogExtension deep = Set("beta", 1, [Global(Source("beta", 1), "len", "beta_len", [CelType.ListOf(CelType.Int)], CelType.Int, size)]);
        CelBindings mixed = new CelBindings().Add("xs", CelValue.FromList(CelList.Create(CelValue.FromInt64(1), CelValue.FromString("x"))));

        CelEnvironment shallowEnvironment = BuildEnvironment(Builder().AddExtension(Of(shallow)).AddVariable("xs", CelType.Dyn));
        CelEnvironment deepEnvironment = BuildEnvironment(Builder().AddExtension(Of(deep)).AddVariable("xs", CelType.Dyn));

        CelEvalResult<CelValue> accepted = Run(shallowEnvironment.Compile<CelValue>("len(xs)"), mixed);
        CelEvalResult<CelValue> refused = Run(deepEnvironment.Compile<CelValue>("len(xs)"), mixed);
        CelEvalResult<CelValue> notAList = Run(shallowEnvironment.Compile<CelValue>("len(xs)"), new CelBindings().Add("xs", 5L));

        Assert.True(accepted.GetValueOrThrow().TryGetInt64(out long length));
        Assert.Equal(2, length);
        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, Assert.Single(refused.Errors).Code);

        // The top level is still checked: the implementation is not called with a value that is no list.
        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, Assert.Single(notAList.Errors).Code);
    }

    private static long Evaluate(CelEnvironment environment, string text)
    {
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(text);
        Assert.True(compiled.IsSuccess, string.Join("; ", compiled.Errors.Select(error => error.Message)));
        Assert.True(Run(compiled, new CelBindings()).GetValueOrThrow().TryGetInt64(out long value));
        return value;
    }

    private static CelEvalResult<CelValue> Run(CelCompileResult<CelValue> compiled, CelBindings bindings) =>
        compiled.GetProgramOrThrow().Evaluate(bindings, null, TestContext.Current.CancellationToken);

    private static string[] MacroNames(MacroTable table)
    {
        List<string> names = [];
        foreach (Macro macro in table.Macros)
        {
            names.Add(macro.Name);
        }

        return [.. names];
    }

    private static string[] Ids(CelEnvironment environment, string function, int arguments) =>
        [.. environment.Declarations.GetOverloads(function, arguments).Select(overload => overload.Declaration.OverloadId)];
}
