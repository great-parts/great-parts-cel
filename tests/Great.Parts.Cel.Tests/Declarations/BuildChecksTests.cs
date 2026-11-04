using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Declarations;

// The build errors of declarations: each code on its input with the text of the error code table, the order
// of the errors, and that a conflict leaves no environment, so no overload is chosen at evaluation.
public sealed class BuildChecksTests
{
    private static readonly CelFunctionImplementation First = arguments => arguments[0];

    private static readonly CelFunctionImplementation Second = arguments => arguments[0];

    private static readonly CelType A = CelType.TypeParameter("A");

    // A version 1 whose table has functions with plain names, which the application may extend:
    // count(string) and list(A).count().
    private static readonly CelStandardLibrary Library = new(new LibraryDeclarations(
        1,
        [
            Standard("count", CelOverloadDecl.Global("count_string", [CelType.String], CelType.Int)),
            Standard("count", CelOverloadDecl.Receiver("list_count", CelType.ListOf(A), [], CelType.Int)),
        ],
        new Dictionary<string, CelType>(StringComparer.Ordinal) { ["uint"] = CelType.UInt, ["map"] = CelType.MapOf(CelType.Dyn, CelType.Dyn) }
            .ToFrozenDictionary(StringComparer.Ordinal),
        ["google.protobuf.Duration"]));

    [Fact]
    public void TwoDifferentDeclarationsOfAVariable_AreDuplicateVariable()
    {
        AssertSingle(
            CelErrorCode.DuplicateVariable,
            "variable 'amount' is declared by both application and application",
            Builder().AddVariable("amount", CelType.Double).AddVariable("amount", CelType.ListOf(CelType.Double)));
    }

    [Fact]
    public void SameVariableDeclaredInEveryForm_IsOneDeclaration()
    {
        CelEnvironmentBuilder builder = Builder()
            .AddVariable("amount", CelType.Double)
            .AddVariable<double>("amount")
            .AddVariable(new CelVariableDecl("amount", CelType.Double));

        Assert.Single(builder.VariableDeclarations);
        Assert.True(builder.Build().IsSuccess);
    }

    [Theory]
    [InlineData("list(A)", "list(uint)")]
    [InlineData("uint", "wrapper(uint)")]
    [InlineData("bool", "dyn")]
    [InlineData("map(A, int)", "map(string, dyn)")]
    public static void OverloadsThatOverlapAfterErasure_AreOverlappingOverloads(string first, string second)
    {
        CelEnvironmentBuilder builder = Builder()
            .AddFunction(new CelFunctionDecl("score", Overload("score_2", second), Overload("score_1", first)))
            .AddFunctionImplementation("score_1", First)
            .AddFunctionImplementation("score_2", First);

        AssertSingle(CelErrorCode.OverlappingOverloads, "overload 'score_1' of application overlaps overload 'score_2' of application of function 'score'", builder);
    }

    [Fact]
    public void OverloadsWithoutParameters_Overlap()
    {
        CelEnvironmentBuilder builder = Builder()
            .AddFunction(new CelFunctionDecl("now_b", CelOverloadDecl.Global("b", [], CelType.Int)))
            .AddFunction(new CelFunctionDecl("now_b", CelOverloadDecl.Global("a", [], CelType.String)))
            .AddFunctionImplementation("a", First)
            .AddFunctionImplementation("b", First);

        AssertSingle(CelErrorCode.OverlappingOverloads, "overload 'a' of application overlaps overload 'b' of application of function 'now_b'", builder);
    }

    // Other element types, another call style or another parameter count do not overlap.
    [Fact]
    public void OverloadsThatDoNotOverlap_Build()
    {
        CelBuildResult result = Builder()
            .AddFunction(new CelFunctionDecl(
                "score",
                Overload("score_list_bool", "list(bool)"),
                Overload("score_list_bytes", "list(bytes)"),
                CelOverloadDecl.Receiver("bool_score", CelType.Bool, [], CelType.Int),
                CelOverloadDecl.Global("score_bool_bool", [CelType.Bool, CelType.Bool], CelType.Int),
                Overload("score_bool", "bool")))
            .AddFunctionImplementation("score_list_bool", First)
            .AddFunctionImplementation("score_list_bytes", First)
            .AddFunctionImplementation("bool_score", First)
            .AddFunctionImplementation("score_bool_bool", First)
            .AddFunctionImplementation("score_bool", First)
            .Build();

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("uint")]
    [InlineData("map")]
    [InlineData("google.protobuf.Duration")]
    public static void VariableWithTheNameOfATypeIdentifier_IsStandardLibraryConflict(string name)
    {
        AssertSingle(
            CelErrorCode.StandardLibraryConflict,
            "variable '" + name + "' of application conflicts with standard library v1: it is a type identifier",
            Builder().AddVariable(name, CelType.Int));
    }

    [Fact]
    public void TypeIdentifiersOfVersion1_AreReservedForVariables()
    {
        foreach (string name in new[] { "bool", "bytes", "double", "int", "uint", "string", "null_type", "type", "list", "map", "google.protobuf.Timestamp" })
        {
            CelError error = Assert.Single(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable(name, CelType.Dyn).Build().Errors);
            Assert.Equal(CelErrorCode.StandardLibraryConflict, error.Code);
        }
    }

    [Theory]
    [InlineData("filter", true, 2)]
    [InlineData("all", true, 2)]
    [InlineData("exists_one", true, 2)]
    [InlineData("map", true, 2)]
    [InlineData("map", true, 3)]
    [InlineData("has", false, 1)]
    public static void FunctionInTheFormOfAMacro_IsStandardLibraryConflict(string name, bool isReceiverStyle, int argumentCount)
    {
        CelType[] arguments = [.. Enumerable.Repeat(CelType.Dyn, argumentCount)];
        CelOverloadDecl overload = isReceiverStyle
            ? CelOverloadDecl.Receiver("my_" + name, CelType.ListOf(CelType.Dyn), arguments, CelType.Dyn)
            : CelOverloadDecl.Global("my_" + name, arguments, CelType.Dyn);

        AssertSingle(
            CelErrorCode.StandardLibraryConflict,
            "function '" + name + "' of application conflicts with standard library v1: it has the form of the macro '" + name + "'",
            Builder().AddFunction(new CelFunctionDecl(name, overload)));
    }

    [Fact]
    public void FunctionWithTheNameOfAMacroInAnotherForm_Builds()
    {
        CelBuildResult result = Builder()
            .AddFunction(new CelFunctionDecl(
                "map",
                CelOverloadDecl.Receiver("list_map", CelType.ListOf(CelType.Dyn), [CelType.Dyn], CelType.Dyn),
                CelOverloadDecl.Global("map_dyn_dyn", [CelType.Dyn, CelType.Dyn], CelType.Dyn)))
            .AddFunction(new CelFunctionDecl("has", CelOverloadDecl.Global("has_dyn_dyn", [CelType.Dyn, CelType.Dyn], CelType.Bool)))
            .AddFunctionImplementation("list_map", First)
            .AddFunctionImplementation("map_dyn_dyn", First)
            .AddFunctionImplementation("has_dyn_dyn", First)
            .Build();

        Assert.True(result.IsSuccess);
    }

    // An overload that overlaps one of the version of its function, the same one included, conflicts; one of
    // another call style or parameter types extends the function.
    [Fact]
    public void OverloadThatOverlapsTheStandardLibrary_IsStandardLibraryConflict()
    {
        AssertSingle(
            CelErrorCode.StandardLibraryConflict,
            "overload 'count_text' of application conflicts with standard library v1: it overlaps overload 'count_string'",
            Builder().AddFunction(new CelFunctionDecl("count", Overload("count_text", "string"))).AddFunctionImplementation("count_text", First));
        AssertSingle(
            CelErrorCode.StandardLibraryConflict,
            "overload 'ints_count' of application conflicts with standard library v1: it overlaps overload 'list_count'",
            Builder()
                .AddFunction(new CelFunctionDecl("count", CelOverloadDecl.Receiver("ints_count", CelType.ListOf(CelType.Int), [], CelType.Int)))
                .AddFunctionImplementation("ints_count", First));
        AssertSingle(
            CelErrorCode.StandardLibraryConflict,
            "overload 'count_string' of application conflicts with standard library v1: it overlaps overload 'count_string'",
            Builder().AddFunction(new CelFunctionDecl("count", Overload("count_string", "string"))));
        Assert.True(Builder()
            .AddFunction(new CelFunctionDecl(
                "count",
                Overload("count_bytes", "bytes"),
                CelOverloadDecl.Receiver("string_count", CelType.String, [], CelType.Int)))
            .AddFunctionImplementation("count_bytes", First)
            .AddFunctionImplementation("string_count", First)
            .Build()
            .IsSuccess);
    }

    [Fact]
    public void OverloadWithoutAnImplementation_IsMissingImplementation()
    {
        AssertSingle(
            CelErrorCode.MissingImplementation,
            "overload 'score_bool' of function 'acme.score' has no implementation",
            Builder().AddFunction(new CelFunctionDecl("acme.score", Overload("score_bool", "bool"))));
    }

    // An object type at any depth, and a .NET type outside the input rows of a variable or of a delegate. The
    // declaration is left out of the later checks: no MissingImplementation, no UnusedImplementation.
    [Fact]
    public void TypeThatNothingDeclares_IsTypeNotDeclared()
    {
        AssertSingle(
            CelErrorCode.TypeNotDeclared,
            "variable 'clients' has type 'acme.Client', which no type source or extension of the environment declares",
            Builder().AddVariable("clients", CelType.MapOf(CelType.String, CelType.ListOf(CelType.ObjectNamed("acme.Client")))));
        AssertSingle(
            CelErrorCode.TypeNotDeclared,
            "overload 'rate_int' has type 'acme.Rate', which no type source or extension of the environment declares",
            Builder()
                .AddFunction(new CelFunctionDecl("rate", CelOverloadDecl.Global("rate_int", [CelType.Int], CelType.ObjectNamed("acme.Rate"))))
                .AddFunctionImplementation("rate_int", First));
        AssertSingle(
            CelErrorCode.TypeNotDeclared,
            "variable 'when' has type 'System.DateTime', which no type source or extension of the environment declares",
            Builder().AddVariable<DateTime>("when"));
        AssertSingle(
            CelErrorCode.TypeNotDeclared,
            "overload 'early_System.DateTime' has type 'System.DateTime', which no type source or extension of the environment declares",
            Builder().AddFunction<DateTime, bool>("early", value => value.Hour < 9));
        AssertSingle(
            CelErrorCode.TypeNotDeclared,
            "overload 'stamp_int' has type 'System.DateTime', which no type source or extension of the environment declares",
            Builder().AddFunction<long, DateTime>("stamp", ticks => new DateTime(ticks, DateTimeKind.Utc)));
    }

    [Fact]
    public void ImplementationOfNoDeclaredOverload_IsUnusedImplementation()
    {
        AssertSingle(
            CelErrorCode.UnusedImplementation,
            "implementation 'score_bytes' has no matching overload declaration",
            Builder().AddFunctionImplementation<long, long>("score_bytes", value => value));
    }

    // A typed implementation has the parameter count of the overload, the receiver included, and the CEL types
    // of its .NET types are the declared ones with type parameters as dyn.
    [Theory]
    [InlineData("count", "pad(string, int) -> string")]
    [InlineData("parameter", "pad(string, int) -> string")]
    [InlineData("result", "pad(string, int) -> string")]
    [InlineData("receiver", "string.pad(int) -> string")]
    [InlineData("typeParameter", "pick(list(A)) -> A")]
    public static void TypedImplementationOfAnotherSignature_IsImplementationSignatureMismatch(string mismatch, string signature)
    {
        CelEnvironmentBuilder builder = Builder();
        string overloadId = mismatch switch
        {
            "receiver" => Declare(builder, CelOverloadDecl.Receiver("string_pad_int", CelType.String, [CelType.Int], CelType.String)),
            "typeParameter" => Declare(builder, CelOverloadDecl.Global("pick_list", [CelType.ListOf(A)], A), "pick"),
            _ => Declare(builder, CelOverloadDecl.Global("pad_string_int", [CelType.String, CelType.Int], CelType.String)),
        };
        _ = mismatch switch
        {
            "count" => builder.AddFunctionImplementation<string, string>(overloadId, text => text),
            "parameter" => builder.AddFunctionImplementation<string, ulong, string>(overloadId, (text, _) => text),
            "result" => builder.AddFunctionImplementation<string, long, long>(overloadId, (_, width) => width),
            "receiver" => builder.AddFunctionImplementation<string, string, long, string>(overloadId, (text, _, _) => text),
            _ => builder.AddFunctionImplementation<CelList, long>(overloadId, list => list.Count),
        };

        AssertSingle(
            CelErrorCode.ImplementationSignatureMismatch,
            "implementation '" + overloadId + "' does not match the signature " + signature + " of the overload",
            builder);
    }

    [Fact]
    public void TypedImplementationOfTheSignature_Builds()
    {
        CelEnvironmentBuilder builder = Builder();
        Declare(builder, CelOverloadDecl.Receiver("string_pad_int", CelType.String, [CelType.Int], CelType.String));
        Declare(builder, CelOverloadDecl.Global("pick_list", [CelType.ListOf(A)], A), "pick");
        Declare(builder, CelOverloadDecl.Global("maybe_int", [CelType.WrapperOf(CelType.Int)], CelType.Bytes), "maybe");
        builder
            .AddFunctionImplementation<string, int, string>("string_pad_int", (text, width) => text.PadLeft(width))
            .AddFunctionImplementation<CelList, CelValue>("pick_list", list => list[0])
            .AddFunctionImplementation<long?, byte[]>("maybe_int", value => value is null ? [] : [1]);

        Assert.True(builder.Build().IsSuccess);
    }

    [Fact]
    public void OneIdentifierForTwoDifferentOverloads_IsDuplicateOverloadId()
    {
        AssertSingle(
            CelErrorCode.DuplicateOverloadId,
            "overload id 'shared' is used by both application and application",
            Builder()
                .AddFunction(new CelFunctionDecl("score", Overload("shared", "int")))
                .AddFunction(new CelFunctionDecl("label", Overload("shared", "int")))
                .AddFunctionImplementation("shared", First));
        AssertSingle(
            CelErrorCode.DuplicateOverloadId,
            "overload id 'count_string' is used by both standard library v1 and application",
            Builder().AddFunction(new CelFunctionDecl("tally", Overload("count_string", "bytes"))));
    }

    // Two implementations of one identifier, the one that a delegate form of AddFunction registers included, or an
    // implementation of an overload of the version. The same delegate twice is one implementation.
    [Fact]
    public void SecondImplementationOfAnIdentifier_IsDuplicateImplementation()
    {
        AssertSingle(
            CelErrorCode.DuplicateImplementation,
            "overload id 'score_bool' has more than one implementation",
            Builder()
                .AddFunction(new CelFunctionDecl("score", Overload("score_bool", "bool")))
                .AddFunctionImplementation("score_bool", First)
                .AddFunctionImplementation("score_bool", Second));
        AssertSingle(
            CelErrorCode.DuplicateImplementation,
            "overload id 'score_bool' has more than one implementation",
            Builder().AddFunction<bool, long>("score", value => value ? 1 : 0).AddFunctionImplementation("score_bool", First));
        AssertSingle(
            CelErrorCode.DuplicateImplementation,
            "overload id 'add_int64' has more than one implementation",
            new CelEnvironmentBuilder(CelStandardLibrary.V1).AddFunctionImplementation("add_int64", First));
        Assert.True(Builder()
            .AddFunction(new CelFunctionDecl("score", Overload("score_bool", "bool")))
            .AddFunctionImplementation("score_bool", First)
            .AddFunctionImplementation("score_bool", First)
            .Build()
            .IsSuccess);
    }

    [Theory]
    [InlineData("_==_")]
    [InlineData("!_")]
    [InlineData("@in")]
    [InlineData("true")]
    [InlineData("while")]
    [InlineData("acme.null")]
    [InlineData("acme..score")]
    [InlineData(".score")]
    [InlineData("score.")]
    [InlineData("2score")]
    [InlineData("score!")]
    [InlineData("счёт")]
    public static void FunctionNameThatIsNotAQualifiedIdentifier_IsInvalidFunctionName(string name)
    {
        AssertSingle(
            CelErrorCode.InvalidFunctionName,
            "function name '" + name + "' of application is not an identifier or a qualified name; operators and internal functions cannot be declared",
            Builder().AddFunction(new CelFunctionDecl(name, Overload("bad_int", "int"))).AddFunctionImplementation("bad_int", First));
    }

    [Theory]
    [InlineData("in")]
    [InlineData("let")]
    [InlineData("acme.false")]
    [InlineData("acme-sales")]
    [InlineData("acme.")]
    [InlineData("9lives")]
    public static void VariableNameThatIsNotAQualifiedIdentifier_IsInvalidVariableName(string name)
    {
        AssertSingle(
            CelErrorCode.InvalidVariableName,
            "variable name '" + name + "' of application is not an identifier or a qualified name",
            Builder().AddVariable(name, CelType.Int));
        AssertSingle(
            CelErrorCode.InvalidVariableName,
            "variable name '" + name + "' of application is not an identifier or a qualified name",
            Builder().AddVariable<DateTime>(name));
    }

    // Reserved words other than keywords may follow the first segment.
    [Theory]
    [InlineData("_")]
    [InlineData("Amount_2")]
    [InlineData("acme.sales.amount")]
    [InlineData("acme.while")]
    public static void QualifiedIdentifier_IsAValidName(string name)
    {
        Assert.True(Builder()
            .AddVariable(name, CelType.Int)
            .AddFunction(new CelFunctionDecl(name, Overload("valid_bool", "bool")))
            .AddFunctionImplementation("valid_bool", First)
            .Build()
            .IsSuccess);
    }

    // The smallest container against each other one; the empty string declares none.
    [Fact]
    public void DifferentContainers_AreContainerConflict()
    {
        CelBuildResult result = Builder().UseContainer("acme.sales").UseContainer("").UseContainer("acme").UseContainer("beta").UseContainer("acme").Build();

        Assert.Equal(
            new[]
            {
                (CelErrorCode.ContainerConflict, "name resolution container 'acme' of application conflicts with container 'acme.sales' of application"),
                (CelErrorCode.ContainerConflict, "name resolution container 'acme' of application conflicts with container 'beta' of application"),
            },
            result.Errors.Select(error => (error.Code, error.Message)));
    }

    [Fact]
    public void UseContainer_ChecksItsArgument()
    {
        CelEnvironmentBuilder builder = Builder();

        Assert.Equal("container", Assert.Throws<ArgumentNullException>(() => builder.UseContainer(null!)).ParamName);
        Assert.Equal("container", Assert.Throws<ArgumentException>(() => builder.UseContainer("acme..sales")).ParamName);
        Assert.Equal("container", Assert.Throws<ArgumentException>(() => builder.UseContainer("in")).ParamName);
        Assert.Same(builder, builder.UseContainer(""));
        Assert.True(builder.Build().IsSuccess);
    }

    // Every error of the set, by code and then by message, whatever the order of the calls; an error found twice
    // once.
    [Fact]
    public void AllErrors_AreOrderedByCodeAndMessageWhateverTheOrderOfTheCalls()
    {
        Action<CelEnvironmentBuilder>[] calls =
        [
            builder => builder.AddVariable("zeta", CelType.Int),
            builder => builder.AddVariable("zeta", CelType.UInt),
            builder => builder.AddVariable("zeta", CelType.Bool),
            builder => builder.AddVariable("alpha", CelType.Int),
            builder => builder.AddVariable("alpha", CelType.Bool),
            builder => builder.UseContainer("x"),
            builder => builder.UseContainer("y"),
            builder => builder.AddFunctionImplementation("orphan", First),
            builder => builder.AddVariable("a-b", CelType.Int),
            builder => builder.AddFunction(new CelFunctionDecl("score", Overload("score_int", "int"))),
        ];
        (CelErrorCode, string)[] expected =
        [
            (CelErrorCode.DuplicateVariable, "variable 'alpha' is declared by both application and application"),
            (CelErrorCode.DuplicateVariable, "variable 'zeta' is declared by both application and application"),
            (CelErrorCode.MissingImplementation, "overload 'score_int' of function 'score' has no implementation"),
            (CelErrorCode.UnusedImplementation, "implementation 'orphan' has no matching overload declaration"),
            (CelErrorCode.InvalidVariableName, "variable name 'a-b' of application is not an identifier or a qualified name"),
            (CelErrorCode.ContainerConflict, "name resolution container 'x' of application conflicts with container 'y' of application"),
        ];

        foreach (IEnumerable<Action<CelEnvironmentBuilder>> order in new[] { calls, Enumerable.Reverse(calls) })
        {
            CelEnvironmentBuilder builder = Builder();
            foreach (Action<CelEnvironmentBuilder> call in order)
            {
                call(builder);
            }

            CelBuildResult result = builder.Build();

            Assert.False(result.IsSuccess);
            Assert.Equal(expected, result.Errors.Select(error => (error.Code, error.Message)));
            Assert.All(result.Errors, AssertBuildError);
        }
    }

    // A conflict is an error of the build, so evaluation never chooses between conflicting overloads.
    [Fact]
    public void Conflicts_LeaveNoEnvironment()
    {
        CelBuildResult overlapping = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction<long, long>("score", value => value)
            .AddFunction(new CelFunctionDecl("score", CelOverloadDecl.Global("score_dyn", [CelType.Dyn], CelType.Int)))
            .AddFunctionImplementation("score_dyn", _ => CelValue.FromInt64(0))
            .Build();

        Assert.False(overlapping.IsSuccess);
        Assert.Equal(CelErrorCode.OverlappingOverloads, Assert.Single(overlapping.Errors).Code);
        Assert.Throws<CelException>(overlapping.GetEnvironmentOrThrow);
    }

    private static CelEnvironmentBuilder Builder() => new(Library);

    private static OverloadBinding Standard(string function, CelOverloadDecl declaration) =>
        new(function, declaration, (ReadOnlySpan<CelValue> _, in CallSite _) => CelValue.Null, 1, null, "standard library v1");

    // A global overload of one parameter of the type in the text form, with the result int.
    private static CelOverloadDecl Overload(string id, string parameterType) =>
        CelOverloadDecl.Global(id, [Parse(parameterType)], CelType.Int);

    private static CelType Parse(string type) => type switch
    {
        "list(A)" => CelType.ListOf(A),
        "list(uint)" => CelType.ListOf(CelType.UInt),
        "list(bool)" => CelType.ListOf(CelType.Bool),
        "list(bytes)" => CelType.ListOf(CelType.Bytes),
        "map(A, int)" => CelType.MapOf(A, CelType.Int),
        "map(string, dyn)" => CelType.MapOf(CelType.String, CelType.Dyn),
        "wrapper(uint)" => CelType.WrapperOf(CelType.UInt),
        "uint" => CelType.UInt,
        "int" => CelType.Int,
        "bool" => CelType.Bool,
        "bytes" => CelType.Bytes,
        "string" => CelType.String,
        _ => CelType.Dyn,
    };

    private static string Declare(CelEnvironmentBuilder builder, CelOverloadDecl overload, string function = "pad")
    {
        builder.AddFunction(new CelFunctionDecl(function, overload));
        return overload.OverloadId;
    }

    private static void AssertSingle(CelErrorCode code, string message, CelEnvironmentBuilder builder)
    {
        CelBuildResult result = builder.Build();

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal((code, message), (error.Code, error.Message));
        AssertBuildError(error);
    }

    private static void AssertBuildError(CelError error)
    {
        Assert.Equal(CelErrorStage.Build, error.Stage);
        Assert.Equal(default, error.Location);
        Assert.Null(error.SourceLocation);
        Assert.Null(error.Exception);
    }
}
