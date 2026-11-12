using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Values;
using Xunit;
using Proto2 = global::Cel.Expr.Conformance.Proto2;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Protobuf;

// The environment and the helpers of the tests of the protobuf package: one environment with the messages of both packages and
// their extensions, the set protos, the container of the proto3 package, and the variables x (a proto3 TestAllTypes), n (a proto3
// NestedTestAllTypes) and p (a proto2 TestAllTypes). An expression is evaluated with the messages that a test gives; a message that
// a test does not give is an empty one.
internal static class Reading
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(ProtobufExample.TypesAll)
        .UseContainer(ProtobufExample.Container3)
        .AddExtension(CelExtensions.Protos(0))
        .AddVariable<Proto3.TestAllTypes>("x")
        .AddVariable<Proto3.NestedTestAllTypes>("n")
        .AddVariable<Proto2.TestAllTypes>("p")
        .Build()
        .GetEnvironmentOrThrow();

    // The value of an expression that evaluates without an error.
    internal static CelValue Read(string expression, Proto3.TestAllTypes? x = null, Proto2.TestAllTypes? p = null, Proto3.NestedTestAllTypes? n = null)
    {
        CelEvalResult<CelValue> result = Evaluate(expression, x, p, n);
        Assert.True(result.IsSuccess, expression + ": " + string.Join(" / ", result.Errors.Select(error => error.Code + " " + error.Message)));
        return result.GetValueOrThrow();
    }

    // The one error of an expression that evaluates with an error.
    internal static CelError ReadError(string expression, Proto3.TestAllTypes? x = null, Proto2.TestAllTypes? p = null)
    {
        CelEvalResult<CelValue> result = Evaluate(expression, x, p, null);
        return Assert.Single(result.Errors);
    }

    // The result of an expression that is a message, as an instance of its class.
    internal static T Message<T>(string expression, Proto3.TestAllTypes? x = null, Proto3.NestedTestAllTypes? n = null)
    {
        CelCompileResult<T> compiled = Environment.Compile<T>(expression);
        Assert.True(compiled.IsSuccess, expression + ": " + string.Join(" / ", compiled.Errors.Select(error => error.Message)));
        return compiled.GetProgramOrThrow().Evaluate(Bindings(x, null, n), cancellationToken: CancellationToken.None).GetValueOrThrow();
    }

    // An expression whose value is true.
    internal static bool IsTrue(string expression, Proto3.TestAllTypes? x = null, Proto2.TestAllTypes? p = null, Proto3.NestedTestAllTypes? n = null) =>
        Read(expression, x, p, n).TryGetBoolean(out bool value) && value;

    private static CelEvalResult<CelValue> Evaluate(string expression, Proto3.TestAllTypes? x, Proto2.TestAllTypes? p, Proto3.NestedTestAllTypes? n)
    {
        CelCompileResult<CelValue> compiled = Environment.Compile<CelValue>(expression);
        Assert.True(compiled.IsSuccess, expression + ": " + string.Join(" / ", compiled.Errors.Select(error => error.Code + " " + error.Message)));
        return compiled.GetProgramOrThrow().Evaluate(Bindings(x, p, n), cancellationToken: CancellationToken.None);
    }

    private static CelBindings Bindings(Proto3.TestAllTypes? x, Proto2.TestAllTypes? p, Proto3.NestedTestAllTypes? n) =>
        new CelBindings()
            .Add("x", x ?? new Proto3.TestAllTypes())
            .Add("p", p ?? new Proto2.TestAllTypes())
            .Add("n", n ?? new Proto3.NestedTestAllTypes());
}
