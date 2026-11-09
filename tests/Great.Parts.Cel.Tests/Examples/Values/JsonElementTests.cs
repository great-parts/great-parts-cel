using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// JsonElement by the JSON data conversion of the language definition: null, bool, a number as a double, a
// string, an array as a list and an object as a map with string keys, all read lazily. A repeated property
// name in an object is DuplicateMapKey in any operation that touches that key: a lookup counts the
// matches, and size, enumeration, in and equality check every name for a repeat before their result.
// j is dyn, p is map(string, dyn); the place of a call is the operator or the "(" after the name, of an
// index its "[", of a comprehension its range. cel-go has no JsonElement, so these examples do not rely on it.
public sealed class JsonElementTests
{
    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<JsonElement>("j")
        .AddVariable("p", CelType.MapOf(CelType.String, CelType.Dyn))
        .AddFunction<JsonElement>("payload", () => default)
        .Build()
        .GetEnvironmentOrThrow();

    [Theory]
    [Trait("Requirement", "F-2")]
    [InlineData("j.n == 1 && type(j.n) == double && int(j.n) == 1")]
    [InlineData("j.d == 2.5 && j.big == 12345678901234567890.0")]
    [InlineData("j.s == 'text' && j.t && j.z == null")]
    [InlineData("size(j.a) == 3 && j.a[1] == 'x' && j.a[2] == [true]")]
    [InlineData("j.o.k == 'v' && j.o == {'k': 'v'}")]
    [InlineData("has(j.n) && !has(j.missing) && 'n' in j && !(1 in j)")]
    [InlineData("j.map(k, k) == ['n', 'd', 'big', 's', 't', 'z', 'a', 'o']")]
    public void JsonValue_IsTheCelValueOfItsKind(string expression)
    {
        using JsonDocument document = JsonDocument.Parse(
            """{"n": 1, "d": 2.5, "big": 12345678901234567890, "s": "text", "t": true, "z": null, "a": [1, "x", [true]], "o": {"k": "v"}}""");

        Assert.True(Evaluate<bool>(expression, document));
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void NumberThatIsNotAFiniteDouble_IsConversionRangeAtTheRead()
    {
        using JsonDocument document = JsonDocument.Parse("""{"huge": 1e400}""");

        AssertError(SingleError("j.huge", document), CelErrorCode.ConversionRange, 1, "1e400 is out of the range of double");
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void MissingKey_IsEvalNoSuchField()
    {
        using JsonDocument document = JsonDocument.Parse("""{"a": 1}""");

        AssertError(SingleError("j.missing", document), CelErrorCode.EvalNoSuchField, 1, "no such field 'missing' in map");
    }

    // The key "a" is repeated; "b" is not. A lookup of "b" counts one match and gives its value; every
    // operation that touches "a", and size, enumeration, in and equality, which check all names, give
    // DuplicateMapKey with the repeated name written as a string literal.
    [Theory]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    [InlineData("j.a", 1)]
    [InlineData("j['a']", 1)]
    [InlineData("has(j.a)", 3)]
    [InlineData("size(j)", 4)]
    [InlineData("j.size()", 6)]
    [InlineData("'b' in j", 4)]
    [InlineData("j.all(k, true)", 0)]
    [InlineData("j.map(k, k)", 0)]
    [InlineData("j == j", 2)]
    [InlineData("j != {}", 2)]
    [InlineData("{'a': 1} == j", 9)]
    [InlineData("[j] == [j]", 4)]
    public void RepeatedName_IsDuplicateMapKeyInEveryOperationThatTouchesIt(string expression, int offset)
    {
        using JsonDocument document = JsonDocument.Parse("""{"a": 1, "b": 2, "a": 3}""");

        AssertError(SingleError(expression, document), CelErrorCode.DuplicateMapKey, offset, "duplicate map key: \"a\"");
        Assert.True(Evaluate<bool>("j.b == 2 && j['b'] == 2 && has(j.b)", document));
    }

    // Names compare ordinally after escape sequences are decoded: "a" repeats "a", "A" does not. Of
    // several repeats, size names the first name that repeats an earlier one in document order. Only the
    // object itself is checked, not the objects inside it.
    [Theory]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    [InlineData("""{"a": 1, "a": 2}""", "size(j)", 4, "\"a\"")]
    [InlineData("""{"b": 1, "a": 2, "b": 3, "a": 4}""", "size(j)", 4, "\"b\"")]
    [InlineData("""{"b": 1, "a": 2, "b": 3, "a": 4}""", "j.a", 1, "\"a\"")]
    [InlineData("""{"a\"b": 1, "a\"b": 2}""", "size(j)", 4, "\"a\\\"b\"")]
    [InlineData("""{"o": {"x": 1, "x": 2}}""", "j.o.x", 3, "\"x\"")]
    public void RepeatedName_IsFoundAfterEscapesAreDecoded(string json, string expression, int offset, string key)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        AssertError(SingleError(expression, document), CelErrorCode.DuplicateMapKey, offset, "duplicate map key: " + key);
    }

    [Theory]
    [Trait("Requirement", "F-2")]
    [InlineData("""{"a": 1, "A": 2}""", "size(j) == 2 && j.a == 1 && j.A == 2")]
    [InlineData("""{"o": {"x": 1, "x": 2}}""", "size(j) == 1 && has(j.o) && 'o' in j")]
    [InlineData("""{"a": 1, "b": 2}""", "j == {'b': 2, 'a': 1} && j != {'a': 1} && 'a' in j && j.map(k, k) == ['a', 'b']")]
    public void ObjectWithoutRepeats_IsAMap(string json, string expression)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.True(Evaluate<bool>(expression, document));
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void ArrayIndexOutOfRange_IsIndexOutOfRange()
    {
        using JsonDocument document = JsonDocument.Parse("""[1, 2, 3]""");

        Assert.True(Evaluate<bool>("j[2] == 3 && size(j) == 3", document));
        AssertError(SingleError("j[5]", document), CelErrorCode.IndexOutOfRange, 1, "index 5 out of range [0, 3)");
    }

    // A variable declared with a more exact type takes only a JsonElement of that kind.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-3")]
    public void JsonElementOfAnotherKindThanDeclared_IsBindingTypeMismatch()
    {
        using JsonDocument array = JsonDocument.Parse("""[1]""");
        using JsonDocument obj = JsonDocument.Parse("""{"k": 1}""");

        CelError error = Assert.Single(environment.Compile<CelValue>("p.k").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("p", array.RootElement), cancellationToken: CancellationToken.None).Errors);

        AssertError(error, CelErrorCode.BindingTypeMismatch, 0, "value of variable 'p' has type 'list', which does not match the declared type 'map(string, dyn)'");
        Assert.True(environment.Compile<bool>("p.k == 1").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("p", obj.RootElement), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // An undefined JsonElement is no value: CelBindings.Add refuses it, and as the result of an application
    // function it is InvalidArgument at the call.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void UndefinedJsonElement_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => new CelBindings().Add("j", default(JsonElement)));

        CelError error = Assert.Single(environment.Compile<CelValue>("payload()").GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).Errors);

        AssertError(error, CelErrorCode.InvalidArgument, 7, "invalid argument to 'payload': undefined JSON element");
    }

    // A JsonElement of a disposed document is ApplicationException with the original exception. Its place
    // is the node that reads the element, which depends on whether the binding read the kind of the root.
    [Fact]
    [Trait("Requirement", "+R-9")]
    public void ElementOfADisposedDocument_IsApplicationException()
    {
        JsonDocument document = JsonDocument.Parse("""{"a": 1}""");
        CelBindings bindings = new CelBindings().Add("j", document.RootElement);
        document.Dispose();

        CelError error = Assert.Single(environment.Compile<CelValue>("j.a").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).Errors);

        Assert.Equal((CelErrorCode.ApplicationException, CelErrorStage.Evaluate), (error.Code, error.Stage));
        Assert.Equal("application code threw System.ObjectDisposedException in a JSON element", error.Message);
        Assert.IsType<ObjectDisposedException>(error.Exception);
    }

    // A JsonElement is never a result: an array and an object convert by the list and map rows, copied at
    // the end of the evaluation, a number as a double.
    [Fact]
    [Trait("Requirement", "+R-10")]
    public void JsonValueAsAResult_ConvertsByTheListMapAndPrimitiveRows()
    {
        using JsonDocument document = JsonDocument.Parse("""{"a": [1, "x"], "n": 2}""");
        CelBindings bindings = new CelBindings().Add("j", document.RootElement);

        Assert.Equal(new object[] { 1.0, "x" }, environment.Compile<IReadOnlyList<object>>("j.a").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(2.0, environment.Compile<double>("j.n").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        IReadOnlyDictionary<string, object> map = environment.Compile<IReadOnlyDictionary<string, object>>("j").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
        Assert.Equal(2, map.Count);
        Assert.Equal(2.0, map["n"]);
    }

    private T Evaluate<T>(string expression, JsonDocument document) =>
        environment.Compile<T>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("j", document.RootElement), cancellationToken: CancellationToken.None).GetValueOrThrow();

    private CelError SingleError(string expression, JsonDocument document)
    {
        CelEvalResult<CelValue> result = environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("j", document.RootElement), cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        return error;
    }

    private static void AssertError(CelError error, CelErrorCode code, int offset, string message)
    {
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
        Assert.Equal((offset, 1, offset + 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }
}
