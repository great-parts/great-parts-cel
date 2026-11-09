using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// JsonElement by the JSON data conversion of the language definition: the kind of an element decides its value, an
// array and an object are lazy adapters, and a repeated property name of an object is DuplicateMapKey wherever it is
// touched.
public sealed class JsonRulesTests
{
    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<JsonElement>("j")
        .AddFunction<JsonElement>("payload", () => default)
        .Build()
        .GetEnvironmentOrThrow();

    [Theory]
    [InlineData("j.i == 7 && type(j.i) == double && int(j.i) == 7")]
    [InlineData("j.f == 0.5 && j.t && !j.u && j.z == null")]
    [InlineData("j.s == 'hi' && size(j.a) == 2 && j.a[0] == 1 && j.a[1] == [2]")]
    [InlineData("j.o.k == 3 && j.o == {'k': 3} && size(j.o) == 1")]
    [InlineData("has(j.i) && !has(j.absent) && 'i' in j && !(7 in j) && size(j) == 8")]
    [InlineData("j.map(k, k) == ['i', 'f', 't', 'u', 'z', 's', 'a', 'o']")]
    public void ElementIsTheValueOfItsKind(string expression)
    {
        using JsonDocument document = JsonDocument.Parse("""{"i": 7, "f": 0.5, "t": true, "u": false, "z": null, "s": "hi", "a": [1, [2]], "o": {"k": 3}}""");

        Assert.True(Evaluate<bool>(expression, document));
    }

    [Fact]
    public void NumberThatIsNotAFiniteDouble_IsConversionRangeAtTheRead()
    {
        using JsonDocument document = JsonDocument.Parse("""{"big": 1e999}""");

        CelError error = SingleError("j.big", document);

        Assert.Equal((CelErrorCode.ConversionRange, 1, "1e999 is out of the range of double"), (error.Code, error.Location.Offset, error.Message));
    }

    [Fact]
    public void MissingKeyAndIndexOutOfRange_AreTheErrorsOfAMapAndAList()
    {
        using JsonDocument document = JsonDocument.Parse("""{"a": [1]}""");

        Assert.Equal(CelErrorCode.EvalNoSuchField, SingleError("j.b", document).Code);
        Assert.Equal(CelErrorCode.IndexOutOfRange, SingleError("j.a[3]", document).Code);
    }

    // The key x is repeated and y is not: a lookup of y gives its value, and every operation that touches x, and size,
    // enumeration, in and equality, which look at all names, give DuplicateMapKey at their own node.
    [Theory]
    [InlineData("j.x", 1)]
    [InlineData("j['x']", 1)]
    [InlineData("has(j.x)", 3)]
    [InlineData("size(j)", 4)]
    [InlineData("'y' in j", 4)]
    [InlineData("j.exists(k, k == 'y')", 0)]
    [InlineData("j.filter(k, true)", 0)]
    [InlineData("j == j", 2)]
    [InlineData("{} != j", 3)]
    [InlineData("[j] == [j]", 4)]
    public void RepeatedName_IsDuplicateMapKeyWhereverItIsTouched(string expression, int offset)
    {
        using JsonDocument document = JsonDocument.Parse("""{"x": 1, "y": 2, "x": 3}""");

        CelError error = SingleError(expression, document);

        Assert.Equal((CelErrorCode.DuplicateMapKey, offset, "duplicate map key: \"x\""), (error.Code, error.Location.Offset, error.Message));
        Assert.True(Evaluate<bool>("j.y == 2 && j['y'] == 2 && has(j.y)", document));
    }

    // The names are compared ordinally after the escape sequences are decoded; the first name that repeats an earlier
    // one, in document order, is the one that size names; only the object itself is checked.
    [Theory]
    [InlineData("""{"a": 1, "a": 2}""", "size(j)", 4, "\"a\"")]
    [InlineData("""{"q": 1, "p": 2, "q": 3, "p": 4}""", "size(j)", 4, "\"q\"")]
    [InlineData("""{"q": 1, "p": 2, "q": 3, "p": 4}""", "j.p", 1, "\"p\"")]
    [InlineData("""{"a\nb": 1, "a\nb": 2}""", "size(j)", 4, "\"a\\nb\"")]
    [InlineData("""{"o": {"x": 1, "x": 2}}""", "j.o.x", 3, "\"x\"")]
    public void RepeatedName_IsFoundAfterTheEscapesAreDecoded(string json, string expression, int offset, string key)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        CelError error = SingleError(expression, document);

        Assert.Equal((CelErrorCode.DuplicateMapKey, offset, "duplicate map key: " + key), (error.Code, error.Location.Offset, error.Message));
    }

    [Theory]
    [InlineData("""{"a": 1, "A": 2}""", "size(j) == 2 && j.a == 1 && j.A == 2")]
    [InlineData("""{"o": {"x": 1, "x": 2}}""", "size(j) == 1 && has(j.o) && 'o' in j")]
    [InlineData("""{"a": 1, "b": 2}""", "j == {'b': 2, 'a': 1} && j != {'a': 1} && j.map(k, k) == ['a', 'b']")]
    public void ObjectWithoutRepeats_IsAMap(string json, string expression)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.True(Evaluate<bool>(expression, document));
    }

    [Fact]
    public void JsonObjectWithRepeats_IsRefusedByTheResultConversionOfAMapCopy()
    {
        using JsonDocument document = JsonDocument.Parse("""{"x": 1, "x": 3}""");

        CelEvalResult<IReadOnlyDictionary<string, object>> result = environment.Compile<IReadOnlyDictionary<string, object>>("j").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("j", document.RootElement), cancellationToken: CancellationToken.None);

        Assert.Equal(CelErrorCode.ResultConversion, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void UndefinedElement_IsRefusedByAddAndIsInvalidArgumentInTheResultOfAFunction()
    {
        Assert.Throws<ArgumentException>(() => new CelBindings().Add("j", default(JsonElement)));

        CelError error = Assert.Single(environment.Compile<CelValue>("payload()").GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).Errors);

        Assert.Equal((CelErrorCode.InvalidArgument, 7, "invalid argument to 'payload': undefined JSON element"), (error.Code, error.Location.Offset, error.Message));
    }

    [Fact]
    public void ElementOfADisposedDocument_IsApplicationExceptionWithTheOriginalException()
    {
        JsonDocument document = JsonDocument.Parse("""{"a": 1}""");
        CelBindings bindings = new CelBindings().Add("j", document.RootElement);
        document.Dispose();

        CelError error = Assert.Single(environment.Compile<CelValue>("j.a").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).Errors);

        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        Assert.Equal("application code threw System.ObjectDisposedException in a JSON element", error.Message);
        Assert.IsType<ObjectDisposedException>(error.Exception);
    }

    private T Evaluate<T>(string expression, JsonDocument document) =>
        environment.Compile<T>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("j", document.RootElement), cancellationToken: CancellationToken.None).GetValueOrThrow();

    private CelError SingleError(string expression, JsonDocument document) =>
        Assert.Single(environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("j", document.RootElement), cancellationToken: CancellationToken.None).Errors);
}
