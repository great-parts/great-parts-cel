using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// An error value that an element or a value of a collection of the application is: the node that reads it gives it its
// place — the identifier of a variable, the call that checks an argument or a result, the read of an iteration variable, the
// root of the expression for the conversion of a result — and a mismatch of type is reported only when there is one.
public sealed class ErrorElementEvaluationTests
{
    private static readonly CelError Failure = new(CelErrorCode.InvalidArgument, CelErrorStage.Evaluate, "failed", default, null, null);

    private static readonly CelFunctionImplementation Total =
        static arguments => CelValue.FromInt64(arguments[0].TryGetList(out CelList? list) ? list.Count : 0);

    private static readonly CelFunctionImplementation Produce = static _ => CelValue.FromList(Elements(1L, Failure));

    private static readonly CelFunctionImplementation ProduceText = static _ => CelValue.FromList(Elements(1L, "x"));

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("xs", CelType.ListOf(CelType.Int))
        .AddVariable("m", CelType.MapOf(CelType.String, CelType.Int))
        .AddVariable("ys", CelType.ListOf(CelType.Dyn))
        .AddVariable("ds", CelType.Dyn)
        .AddFunction(new CelFunctionDecl("total", CelOverloadDecl.Global("total_list", [CelType.ListOf(CelType.Int)], CelType.Int)))
        .AddFunctionImplementation("total_list", Total)
        .AddFunction(new CelFunctionDecl("produce", CelOverloadDecl.Global("produce", [], CelType.ListOf(CelType.Int))))
        .AddFunctionImplementation("produce", Produce)
        .AddFunction(new CelFunctionDecl("produceText", CelOverloadDecl.Global("produceText", [], CelType.ListOf(CelType.Int))))
        .AddFunctionImplementation("produceText", ProduceText)
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void VariableOfAListWithAnErrorElement_GivesTheErrorTheIdentifierAsItsPlace()
    {
        CelError error = SingleError("xs", Bind("xs", Elements(1L, Failure, 3L)));

        Assert.Equal((CelErrorCode.InvalidArgument, 0), (error.Code, error.Location.Offset));
        Assert.Equal("failed", error.Message);
    }

    [Fact]
    public void VariableOfAMapWithAnErrorValue_GivesTheErrorTheIdentifierAsItsPlace()
    {
        CelError error = SingleError("1 + m['a']", BindMap("m", ("a", 1L), ("b", Failure)));

        Assert.Equal((CelErrorCode.InvalidArgument, 4), (error.Code, error.Location.Offset));
    }

    [Fact]
    public void ErrorElementOfAVariable_IsTheValueOfTheVariableForEveryLaterRead()
    {
        CelBindings bindings = Bind("xs", Elements(1L, Failure));

        Assert.Equal(CelErrorCode.InvalidArgument, SingleError("xs[0]", bindings).Code);
        Assert.Equal(CelErrorCode.InvalidArgument, SingleError("size(xs)", bindings).Code);
    }

    [Fact]
    public void ElementThatDoesNotFit_IsStillBindingTypeMismatch()
    {
        CelError error = SingleError("xs", Bind("xs", Elements(1L, "x", Failure)));

        Assert.Equal((CelErrorCode.BindingTypeMismatch, 0), (error.Code, error.Location.Offset));
    }

    [Fact]
    public void MarkedArgumentWithAnErrorElement_IsTheResultOfTheCallAtItsPlace()
    {
        CelError error = SingleError("total(ds)", Bind("ds", Elements(1L, Failure)));

        Assert.Equal((CelErrorCode.InvalidArgument, 5), (error.Code, error.Location.Offset));
        Assert.Equal("failed", error.Message);
    }

    [Fact]
    public void MarkedArgumentThatDoesNotFit_IsStillNoMatchingOverload()
    {
        CelError error = SingleError("total(ds)", Bind("ds", Elements(1L, "x", Failure)));

        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, error.Code);
    }

    [Fact]
    public void ResultWithAnErrorElement_IsTheResultOfTheCallAtItsPlace()
    {
        CelError error = SingleError("produce()", new CelBindings());

        Assert.Equal((CelErrorCode.InvalidArgument, 7), (error.Code, error.Location.Offset));
        Assert.Equal("failed", error.Message);
    }

    [Fact]
    public void ResultThatDoesNotFit_IsStillImplementationResultMismatch()
    {
        CelError error = SingleError("produceText()", new CelBindings());

        Assert.Equal((CelErrorCode.ImplementationResultMismatch, 11), (error.Code, error.Location.Offset));
    }

    [Fact]
    public void ErrorElementInTheIterationVariable_GetsThePlaceOfTheNodeThatReadsIt()
    {
        CelBindings bindings = Bind("ys", Elements(1L, Failure));

        CelError mapped = SingleError("ys.map(x, x)", bindings);
        CelError all = SingleError("ys.all(x, x > 0)", bindings);

        Assert.Equal((CelErrorCode.InvalidArgument, 10), (mapped.Code, mapped.Location.Offset));
        Assert.Equal((CelErrorCode.InvalidArgument, 10), (all.Code, all.Location.Offset));
    }

    [Fact]
    public void ErrorElementThatNoNodeReads_IsNotAnError()
    {
        CelBindings bindings = Bind("ys", Elements(1L, Failure));

        Assert.True(Evaluate<bool>("ys.exists(x, true)", bindings).GetValueOrThrow());
        Assert.Equal(2L, Evaluate<long>("size(ys)", bindings).GetValueOrThrow());
    }

    [Fact]
    public void ErrorElementAbsorbedByTheOperatorsOfCel_IsNotAnError()
    {
        CelBindings bindings = Bind("ys", Elements(Failure, 2L));

        Assert.True(Evaluate<bool>("ys.exists(x, x == 2)", bindings).GetValueOrThrow());
        Assert.False(Evaluate<bool>("ys.all(x, x == 1)", bindings).GetValueOrThrow());
    }

    [Fact]
    public void ResultConvertedToATypedList_ReturnsTheErrorElementAtTheRoot()
    {
        CelEvalResult<IReadOnlyList<long>> failed = Evaluate<IReadOnlyList<long>>("ds", Bind("ds", Elements(1L, Failure)));
        CelEvalResult<IReadOnlyList<long>> mixed = Evaluate<IReadOnlyList<long>>("ds", Bind("ds", Elements(1L, "x")));
        CelEvalResult<IReadOnlyList<long>> fits = Evaluate<IReadOnlyList<long>>("ds", Bind("ds", Elements(1L, 2L)));

        CelError error = Assert.Single(failed.Errors);
        Assert.Equal((CelErrorCode.InvalidArgument, 0), (error.Code, error.Location.Offset));
        Assert.Equal(CelErrorCode.ResultConversion, Assert.Single(mixed.Errors).Code);
        Assert.Equal<long>([1L, 2L], fits.GetValueOrThrow());
    }

    [Fact]
    public void ResultConvertedToATypedMap_ReturnsTheFirstErrorValueInTheOrderOfIteration()
    {
        CelEvalResult<IReadOnlyDictionary<string, long>> result = Evaluate<IReadOnlyDictionary<string, long>>(
            "ds",
            Bind("ds", CelValue.FromMap(Entries(("a", 1L), ("b", Failure)))));

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.InvalidArgument, 0), (error.Code, error.Location.Offset));
    }

    private static CelEvalResult<T> Evaluate<T>(string expression, CelBindings bindings) =>
        Environment.Compile<T>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);

    private static CelError SingleError(string expression, CelBindings bindings) =>
        Assert.Single(Evaluate<CelValue>(expression, bindings).Errors);

    private static CelBindings Bind(string name, CelList list) => new CelBindings().Add<CelValue>(name, CelValue.FromList(list));

    private static CelBindings Bind(string name, CelValue value) => new CelBindings().Add<CelValue>(name, value);

    private static CelBindings BindMap(string name, params (string Key, object Value)[] entries) => Bind(name, CelValue.FromMap(Entries(entries)));

    // A list of the application whose elements are longs, strings and error values.
    private static CelList Elements(params object[] items) => CelList.CreateView<object>(items, Convert);

    // A map of the application, in the order of the entries.
    private static CelMap Entries(params (string Key, object Value)[] entries)
    {
        var source = new Dictionary<string, object>();
        foreach ((string key, object value) in entries)
        {
            source.Add(key, value);
        }

        return CelMap.CreateView(source, Convert, keepSourceOrder: true);
    }

    private static CelValue Convert(object item) => item switch
    {
        CelError error => CelValue.FromError(error),
        string text => CelValue.FromString(text),
        _ => CelValue.FromInt64((long)item),
    };
}
