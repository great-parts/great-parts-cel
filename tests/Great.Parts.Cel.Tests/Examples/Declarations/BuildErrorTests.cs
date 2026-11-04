using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Declarations;

// The errors of building an environment. Build checks the whole set of declarations and returns every error
// without an environment, ordered by code and then by message, so the order of the builder calls changes
// nothing. A build error has the stage Build and no place in an expression. The sources are named
// "application" and "standard library v1"; in a pair the standard library comes first, and of two items of
// one source the one with the smaller identifier or container.
public sealed class BuildErrorTests
{
    private static readonly CelFunctionImplementation Echo = arguments => arguments[0];

    private static readonly CelFunctionImplementation Other = arguments => arguments[0];

    private readonly CelStandardLibrary library = CelStandardLibrary.V1;

    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-3")]
    public void TwoDeclarationsOfOneVariable_AreDuplicateVariable()
    {
        CelError error = SingleError(Builder().AddVariable("x", CelType.Int).AddVariable("x", CelType.String));

        AssertError(CelErrorCode.DuplicateVariable, "variable 'x' is declared by both application and application", error);
    }

    // Overloads of one function overlap when, after type parameters are erased to dyn, each parameter of one
    // takes the parameter of the other: list(A) and list(int), int and wrapper(int), dyn and anything.
    [Theory]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-3")]
    [InlineData("f_list", "f_list_int")]
    [InlineData("f_int", "f_wrapper")]
    [InlineData("f_dyn", "f_string")]
    public void OverloadsThatOverlapAfterErasure_AreOverlappingOverloads(string first, string second)
    {
        CelError error = SingleError(Builder()
            .AddFunction(new CelFunctionDecl("f", Overload(second), Overload(first)))
            .AddFunctionImplementation(first, Echo)
            .AddFunctionImplementation(second, Echo));

        AssertError(
            CelErrorCode.OverlappingOverloads,
            "overload '" + first + "' of application overlaps overload '" + second + "' of application of function 'f'",
            error);
    }

    // Different parameter types after erasure, or a different call style, do not overlap.
    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-3")]
    public void OverloadsThatDoNotOverlap_Build()
    {
        CelBuildResult result = Builder()
            .AddFunction(new CelFunctionDecl(
                "f",
                Overload("f_list_int"),
                CelOverloadDecl.Global("f_list_string", [CelType.ListOf(CelType.String)], CelType.Int),
                CelOverloadDecl.Receiver("string_f", CelType.String, [], CelType.Int),
                Overload("f_string")))
            .AddFunctionImplementation("f_list_int", Echo)
            .AddFunctionImplementation("f_list_string", Echo)
            .AddFunctionImplementation("string_f", Echo)
            .AddFunctionImplementation("f_string", Echo)
            .Build();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
    }

    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-3")]
    public void DeclarationThatTheStandardLibraryOwns_IsStandardLibraryConflict()
    {
        AssertError(
            CelErrorCode.StandardLibraryConflict,
            "variable 'int' of application conflicts with standard library v1: it is a type identifier",
            SingleError(Builder().AddVariable("int", CelType.Dyn)));
        AssertError(
            CelErrorCode.StandardLibraryConflict,
            "variable 'google.protobuf.Duration' of application conflicts with standard library v1: it is a type identifier",
            SingleError(Builder().AddVariable("google.protobuf.Duration", CelType.Dyn)));
        AssertError(
            CelErrorCode.StandardLibraryConflict,
            "function 'has' of application conflicts with standard library v1: it has the form of the macro 'has'",
            SingleError(Builder()
                .AddFunction(new CelFunctionDecl("has", CelOverloadDecl.Global("has_dyn", [CelType.Dyn], CelType.Bool)))
                .AddFunctionImplementation("has_dyn", Echo)));
        AssertError(
            CelErrorCode.StandardLibraryConflict,
            "function 'exists' of application conflicts with standard library v1: it has the form of the macro 'exists'",
            SingleError(Builder()
                .AddFunction(new CelFunctionDecl(
                    "exists",
                    CelOverloadDecl.Receiver("list_exists", CelType.ListOf(CelType.Dyn), [CelType.Dyn, CelType.Bool], CelType.Bool)))
                .AddFunctionImplementation("list_exists", Echo)));
        AssertError(
            CelErrorCode.StandardLibraryConflict,
            "overload 'size_text' of application conflicts with standard library v1: it overlaps overload 'size_string'",
            SingleError(Builder()
                .AddFunction(new CelFunctionDecl("size", CelOverloadDecl.Global("size_text", [CelType.String], CelType.Int)))
                .AddFunctionImplementation("size_text", Echo)));
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    public void OverloadWithoutAnImplementation_IsMissingImplementation()
    {
        CelError error = SingleError(Builder()
            .AddFunction(new CelFunctionDecl("isInn", CelOverloadDecl.Global("isInn_string", [CelType.String], CelType.Bool))));

        AssertError(CelErrorCode.MissingImplementation, "overload 'isInn_string' of function 'isInn' has no implementation", error);
    }

    // An object type that no type source describes; the declaration is left out of the later checks, so a
    // missing implementation of the overload is no second error.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void TypeThatNothingDeclares_IsTypeNotDeclared()
    {
        AssertError(
            CelErrorCode.TypeNotDeclared,
            "variable 'order' has type 'acme.sales.Order', which no type source or extension of the environment declares",
            SingleError(Builder().AddVariable("order", CelType.ObjectNamed("acme.sales.Order"))));
        AssertError(
            CelErrorCode.TypeNotDeclared,
            "overload 'total_order' has type 'acme.sales.Order', which no type source or extension of the environment declares",
            SingleError(Builder().AddFunction(new CelFunctionDecl(
                "total",
                CelOverloadDecl.Global("total_order", [CelType.ObjectNamed("acme.sales.Order")], CelType.Double)))));
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    public void ImplementationOfNoDeclaredOverload_IsUnusedImplementation()
    {
        CelError error = SingleError(Builder().AddFunctionImplementation("nothing_int", Echo));

        AssertError(CelErrorCode.UnusedImplementation, "implementation 'nothing_int' has no matching overload declaration", error);
    }

    // A typed implementation must have the parameter count of the overload, and its .NET types must map to the
    // declared CEL types; the signature is written in the notation of the language definition.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void TypedImplementationOfAnotherSignature_IsImplementationSignatureMismatch()
    {
        AssertError(
            CelErrorCode.ImplementationSignatureMismatch,
            "implementation 'isInn_string' does not match the signature isInn(string) -> bool of the overload",
            SingleError(Builder()
                .AddFunction(new CelFunctionDecl("isInn", CelOverloadDecl.Global("isInn_string", [CelType.String], CelType.Bool)))
                .AddFunctionImplementation<long, bool>("isInn_string", number => number > 0)));
        AssertError(
            CelErrorCode.ImplementationSignatureMismatch,
            "implementation 'string_isInn' does not match the signature string.isInn() -> bool of the overload",
            SingleError(Builder()
                .AddFunction(new CelFunctionDecl("isInn", CelOverloadDecl.Receiver("string_isInn", CelType.String, [], CelType.Bool)))
                .AddFunctionImplementation<string, string, bool>("string_isInn", (text, other) => text == other)));
        AssertError(
            CelErrorCode.ImplementationSignatureMismatch,
            "implementation 'len_string' does not match the signature len(string) -> int of the overload",
            SingleError(Builder()
                .AddFunction(new CelFunctionDecl("len", CelOverloadDecl.Global("len_string", [CelType.String], CelType.Int)))
                .AddFunctionImplementation<string, string>("len_string", text => text)));
    }

    // A parameter type that the input rows map to the declared type but the result rows never fill, so that
    // no argument could be passed: a Guid reads as a string, a short as an int, yet a string value is passed
    // only to string and an int only to long or int. Both delegate forms report it when the environment is
    // built; the overload of the delegate form is the one AddFunction derives.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void ParameterThatNoArgumentFills_IsImplementationSignatureMismatch()
    {
        AssertError(
            CelErrorCode.ImplementationSignatureMismatch,
            "implementation 'isInn_string' does not match the signature isInn(string) -> bool of the overload",
            SingleError(Builder()
                .AddFunction(new CelFunctionDecl("isInn", CelOverloadDecl.Global("isInn_string", [CelType.String], CelType.Bool)))
                .AddFunctionImplementation<Guid, bool>("isInn_string", id => id != Guid.Empty)));
        AssertError(
            CelErrorCode.ImplementationSignatureMismatch,
            "implementation 'small_int' does not match the signature small(int) -> bool of the overload",
            SingleError(Builder().AddFunction<short, bool>("small", number => number < 10)));
    }

    // The CEL type of a parameter is the one that the expected-type table derives from its .NET type, as for
    // Compile<T>: IReadOnlyList<long> and IReadOnlyList<int> take list(int), the first as a lazy view and the
    // second as a copy. A CelList is a list(dyn), so it takes list(dyn) but not list(int).
    [Fact]
    [Trait("Requirement", "F-3")]
    public void CollectionParameter_TakesTheTypeThatItsExpectedTypeNames()
    {
        Assert.True(Builder()
            .AddFunction(new CelFunctionDecl("sum", CelOverloadDecl.Global("sum_list_int", [CelType.ListOf(CelType.Int)], CelType.Int)))
            .AddFunction(new CelFunctionDecl("sum32", CelOverloadDecl.Global("sum32_list_int", [CelType.ListOf(CelType.Int)], CelType.Int)))
            .AddFunction(new CelFunctionDecl("count", CelOverloadDecl.Global("count_list_dyn", [CelType.ListOf(CelType.Dyn)], CelType.Int)))
            .AddFunctionImplementation<IReadOnlyList<long>, long>("sum_list_int", list => list.Sum())
            .AddFunctionImplementation<IReadOnlyList<int>, long>("sum32_list_int", list => list.Sum())
            .AddFunctionImplementation<CelList, long>("count_list_dyn", list => list.Count)
            .Build()
            .IsSuccess);
        AssertError(
            CelErrorCode.ImplementationSignatureMismatch,
            "implementation 'count_list_int' does not match the signature count(list(int)) -> int of the overload",
            SingleError(Builder()
                .AddFunction(new CelFunctionDecl("count", CelOverloadDecl.Global("count_list_int", [CelType.ListOf(CelType.Int)], CelType.Int)))
                .AddFunctionImplementation<CelList, long>("count_list_int", list => list.Count)));
    }

    // dyn and type parameters take CelValue and object; int takes long and int.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void TypedImplementationOfTheSignature_Builds()
    {
        CelBuildResult result = Builder()
            .AddFunction(new CelFunctionDecl("echo", CelOverloadDecl.Global("echo_dyn", [CelType.Dyn], CelType.Dyn)))
            .AddFunction(new CelFunctionDecl("small", CelOverloadDecl.Global("small_int", [CelType.Int], CelType.Bool)))
            .AddFunction(new CelFunctionDecl("first", CelOverloadDecl.Global("first_dyn", [CelType.TypeParameter("T")], CelType.TypeParameter("T"))))
            .AddFunctionImplementation<CelValue, object>("echo_dyn", value => value)
            .AddFunctionImplementation<int, bool>("small_int", number => number < 10)
            .AddFunctionImplementation<object, CelValue>("first_dyn", value => CelValue.FromString(value.ToString() ?? ""))
            .Build();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-3")]
    public void OneIdentifierForTwoDifferentOverloads_IsDuplicateOverloadId()
    {
        CelError error = SingleError(Builder()
            .AddFunction(new CelFunctionDecl("f", CelOverloadDecl.Global("same_id", [CelType.Int], CelType.Int)))
            .AddFunction(new CelFunctionDecl("g", CelOverloadDecl.Global("same_id", [CelType.String], CelType.Int)))
            .AddFunctionImplementation("same_id", Echo));

        AssertError(CelErrorCode.DuplicateOverloadId, "overload id 'same_id' is used by both application and application", error);
    }

    // A second implementation of one identifier, also of the identifier that AddFunction derives from the
    // delegate, or an implementation of an overload of the standard library.
    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-3")]
    public void SecondImplementationOfAnIdentifier_IsDuplicateImplementation()
    {
        AssertError(
            CelErrorCode.DuplicateImplementation,
            "overload id 'isInn_string' has more than one implementation",
            SingleError(Builder()
                .AddFunction(new CelFunctionDecl("isInn", CelOverloadDecl.Global("isInn_string", [CelType.String], CelType.Bool)))
                .AddFunctionImplementation("isInn_string", Echo)
                .AddFunctionImplementation("isInn_string", Other)));
        AssertError(
            CelErrorCode.DuplicateImplementation,
            "overload id 'isInn_string' has more than one implementation",
            SingleError(Builder()
                .AddFunction<string, bool>("isInn", text => text.Length == 10)
                .AddFunctionImplementation("isInn_string", Echo)));
        AssertError(
            CelErrorCode.DuplicateImplementation,
            "overload id 'size_string' has more than one implementation",
            SingleError(Builder().AddFunctionImplementation("size_string", Echo)));
    }

    // Operators and internal functions cannot be declared; a name is an identifier, or identifiers joined by
    // ".", whose first one is not a reserved word.
    [Theory]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-3")]
    [InlineData("_+_")]
    [InlineData("_[_]")]
    [InlineData("@in")]
    [InlineData("@not_strictly_false")]
    [InlineData("if")]
    [InlineData("in.f")]
    [InlineData("a..b")]
    [InlineData("a.")]
    [InlineData("1f")]
    [InlineData("is-inn")]
    public void FunctionNameThatIsNotAQualifiedIdentifier_IsInvalidFunctionName(string name)
    {
        CelError error = SingleError(Builder()
            .AddFunction(new CelFunctionDecl(name, CelOverloadDecl.Global("my_overload", [CelType.Int], CelType.Int))));

        AssertError(
            CelErrorCode.InvalidFunctionName,
            "function name '" + name + "' of application is not an identifier or a qualified name; operators and internal functions cannot be declared",
            error);
    }

    [Theory]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-3")]
    [InlineData("x-y")]
    [InlineData("in")]
    [InlineData("null.x")]
    [InlineData(".x")]
    [InlineData("x.")]
    [InlineData("1x")]
    public void VariableNameThatIsNotAQualifiedIdentifier_IsInvalidVariableName(string name)
    {
        CelError error = SingleError(Builder().AddVariable(name, CelType.Int));

        AssertError(CelErrorCode.InvalidVariableName, "variable name '" + name + "' of application is not an identifier or a qualified name", error);
    }

    [Theory]
    [Trait("Requirement", "F-3")]
    [InlineData("_x")]
    [InlineData("acme.sales.isInn")]
    [InlineData("a.if")]
    public void QualifiedIdentifier_IsAValidName(string name)
    {
        Assert.True(Builder()
            .AddVariable(name, CelType.Int)
            .AddFunction(new CelFunctionDecl(name, CelOverloadDecl.Global("my_overload", [CelType.String], CelType.Int)))
            .AddFunctionImplementation("my_overload", Echo)
            .Build()
            .IsSuccess);
    }

    // The same container twice is one declaration, and the empty string declares none.
    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-3")]
    public void TwoDifferentContainers_AreContainerConflict()
    {
        const string Message = "name resolution container 'a.b' of application conflicts with container 'c' of application";

        AssertError(CelErrorCode.ContainerConflict, Message, SingleError(Builder().UseContainer("a.b").UseContainer("c")));
        AssertError(CelErrorCode.ContainerConflict, Message, SingleError(Builder().UseContainer("c").UseContainer("a.b")));
        Assert.True(Builder().UseContainer("a.b").UseContainer("a.b").UseContainer("").Build().IsSuccess);
    }

    // Every error of the set is reported, by code and then by message; the reverse order of the calls gives
    // the same list.
    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-4")]
    public void AllErrors_AreReportedByCodeWhateverTheOrderOfTheCalls()
    {
        CelBuildResult forward = Builder()
            .AddVariable("x-y", CelType.Int)
            .AddVariable("z", CelType.Int)
            .AddVariable("z", CelType.Bool)
            .AddFunctionImplementation("nothing_int", Echo)
            .AddVariable("b", CelType.Int)
            .AddVariable("b", CelType.String)
            .Build();
        CelBuildResult reverse = Builder()
            .AddVariable("b", CelType.String)
            .AddVariable("b", CelType.Int)
            .AddFunctionImplementation("nothing_int", Echo)
            .AddVariable("z", CelType.Bool)
            .AddVariable("z", CelType.Int)
            .AddVariable("x-y", CelType.Int)
            .Build();

        Assert.Equal(
            new[]
            {
                (CelErrorCode.DuplicateVariable, "variable 'b' is declared by both application and application"),
                (CelErrorCode.DuplicateVariable, "variable 'z' is declared by both application and application"),
                (CelErrorCode.UnusedImplementation, "implementation 'nothing_int' has no matching overload declaration"),
                (CelErrorCode.InvalidVariableName, "variable name 'x-y' of application is not an identifier or a qualified name"),
            },
            forward.Errors.Select(error => (error.Code, error.Message)));
        Assert.Equal(forward.Errors.Select(error => (error.Code, error.Message)), reverse.Errors.Select(error => (error.Code, error.Message)));
    }

    // Repeating a declaration adds nothing.
    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-3")]
    public void SameDeclarationTwice_IsOneDeclaration()
    {
        var function = new CelFunctionDecl("isInn", CelOverloadDecl.Global("isInn_string", [CelType.String], CelType.Bool));

        CelBuildResult result = Builder()
            .AddVariable("x", CelType.Int)
            .AddVariable<long>("x")
            .AddFunction(function)
            .AddFunction(function)
            .AddFunctionImplementation("isInn_string", Echo)
            .Build();

        Assert.True(result.IsSuccess);
    }

    private CelEnvironmentBuilder Builder() => new(library);

    // The overloads of the overlap cases by identifier.
    private static CelOverloadDecl Overload(string id) => id switch
    {
        "f_list" => CelOverloadDecl.Global(id, [CelType.ListOf(CelType.TypeParameter("A"))], CelType.Int),
        "f_list_int" => CelOverloadDecl.Global(id, [CelType.ListOf(CelType.Int)], CelType.Int),
        "f_int" => CelOverloadDecl.Global(id, [CelType.Int], CelType.Int),
        "f_wrapper" => CelOverloadDecl.Global(id, [CelType.WrapperOf(CelType.Int)], CelType.Int),
        "f_dyn" => CelOverloadDecl.Global(id, [CelType.Dyn], CelType.Int),
        _ => CelOverloadDecl.Global(id, [CelType.String], CelType.Int),
    };

    private static CelError SingleError(CelEnvironmentBuilder builder)
    {
        CelBuildResult result = builder.Build();

        Assert.False(result.IsSuccess);
        Assert.Throws<InvalidOperationException>(() => result.Environment);
        return Assert.Single(result.Errors);
    }

    private static void AssertError(CelErrorCode code, string message, CelError error)
    {
        Assert.Equal((code, CelErrorStage.Build, message), (error.Code, error.Stage, error.Message));
        Assert.Equal(default(CelSourceLocation), error.Location);
        Assert.Null(error.SourceLocation);
    }
}
