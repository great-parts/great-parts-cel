using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Filtering;
using Great.Parts.Cel.Policy;
using Great.Parts.Cel.Protobuf;

namespace Great.Parts.Cel.Wasm.App;

/// <summary>
/// The expressions of the application: the quick start of the README on <c>Great.Parts.Cel</c>, the example of
/// <c>docs/protobuf.md</c> on <c>Great.Parts.Cel.Protobuf</c>, an asynchronous function with a partial evaluation, so that the
/// trimmer analyzes the asynchronous driver and the residual expression as well, a small policy on
/// <c>Great.Parts.Cel.Policy</c>, a small filter on <c>Great.Parts.Cel.Filtering</c>, and a call of the simple entry with a class
/// and a list of classes of the application, whose descriptions the generator of call sites writes into this assembly.
/// </summary>
internal static class CelSamples
{
    internal static long Arithmetic()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<long>("x")
            .Build()
            .GetEnvironmentOrThrow();
        CelProgram<long> program = environment.Compile<long>("(x + 2) / 3").GetProgramOrThrow();
        return program.Evaluate(new CelBindings().Add("x", 40L)).GetValueOrThrow();
    }

    // A function of the application that waits, called twice in a chain: the driver of asynchronous evaluation on the one thread.
    internal static async Task<long> AsynchronousCallAsync()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<long>("x")
            .AddAsyncFunction("inc", async (long value, CancellationToken token) =>
            {
                await Task.Yield();
                return value + 1;
            })
            .Build()
            .GetEnvironmentOrThrow();
        CelProgram<long> program = environment.Compile<long>("inc(inc(x))").GetProgramOrThrow();
        CelEvalResult<long> result = await program.EvaluateAsync(new CelBindings().Add("x", 1L), cancellationToken: CancellationToken.None).ConfigureAwait(false);
        return result.GetValueOrThrow();
    }

    // Partial evaluation with an unknown variable, and the residual expression: the known part is replaced by its value.
    internal static string Residual()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<long>("x")
            .AddVariable<long>("y")
            .Build()
            .GetEnvironmentOrThrow();
        CelProgram<bool> program = environment.Compile<bool>("x + 1 > y", new CelCompileOptions { PartialEvaluation = true }).GetProgramOrThrow();
        CelPartialEvalResult<bool> result = program.EvaluatePartial(new CelBindings().Add("x", 1L), [new CelAttributePattern("y")]);
        return CelSyntax.Format(result.GetResidual());
    }

    internal static string Message()
    {
        CelProtobufTypes types = new CelProtobufTypesBuilder("api")
            .AddFile(ApiReflection.Descriptor)
            .Build();
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(types)
            .UseContainer("google.protobuf")
            .AddVariable<Api>("api")
            .Build()
            .GetEnvironmentOrThrow();
        CelProgram<string> label = environment.Compile<string>("api.name + ' v' + api.version").GetProgramOrThrow();
        Api value = new() { Name = "orders", Version = "2" };
        return label.Evaluate(new CelBindings().Add("api", value)).GetValueOrThrow();
    }

    internal static string Greeting()
    {
        const string Yaml = """
            name: greeting
            rule:
              variables:
                - name: who
                  expression: user
              match:
                - condition: variables.who.startsWith('j')
                  output: "'Hi, J!'"
                - output: "'Hi, ' + variables.who + '!'"
            """;
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<string>("user")
            .AddExtension(CelExtensions.Optional(2))
            .AddExtension(CelExtensions.Bindings(1))
            .Build()
            .GetEnvironmentOrThrow();
        CelPolicy policy = CelPolicy.Parse(Yaml, "greeting.yaml").GetPolicyOrThrow();
        CelProgram<string> program = policy.Compile<string>(environment).GetProgramOrThrow();
        return program.Evaluate(new CelBindings().Add("user", "kim")).GetValueOrThrow();
    }

    // The imports of a policy, which are abbreviations of the environment, and the policy of its explanations.
    internal static string Explanation()
    {
        const string Yaml = """
            name: limits
            imports:
              - name: acme.limits.max
            rule:
              match:
                - condition: max > 10
                  output: "'over'"
                  explanation: "'the limit is ' + string(max)"
                - output: "'within'"
                  explanation: "'inside the limit'"
            """;
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<long>("acme.limits.max")
            .Build()
            .GetEnvironmentOrThrow();
        CelPolicy explanations = CelPolicy.Parse(Yaml, "limits.yaml").GetPolicyOrThrow().WithExplanationOutputs();
        CelProgram<string> program = explanations.Compile<string>(environment).GetProgramOrThrow();
        return program.Evaluate(new CelBindings().Add("acme.limits.max", 42L)).GetValueOrThrow();
    }

    // An abbreviation added by the application, an expression of a JSON string whose errors report the place in the document by
    // segments, and the error of a document that is not an expression in the exception that holds the errors of the expression.
    internal static string Located()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<long>("acme.limits.max")
            .AddAbbreviation("acme.limits.max")
            .Build()
            .GetEnvironmentOrThrow();
        CelCompileOptions options = new()
        {
            SourceSegments =
            [
                new CelSourceSegment(0, new CelSourceLocation("rules.json", 10, 1, 11)),
                new CelSourceSegment(7, new CelSourceLocation("rules.json", 20, 1, 21)),
            ],
        };
        CelCompileResult<long> compiled = environment.Compile<long>("max + \n nope", options);
        CelError document = CelError.Create(
            CelErrorCode.InvalidPolicy,
            CelErrorStage.Parse,
            "the document is not a policy",
            new CelSourceLocation("rules.json", 0, 1, 1));
        CelException exception = new([document, .. compiled.Errors]);
        return exception.Errors.Length + " errors, the first at " + exception.Errors[0].SourceLocation!.Value.Line;
    }

    internal static bool Filter()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<string>("title")
            .AddVariable<long>("pages")
            .Build()
            .GetEnvironmentOrThrow();
        CelFilterCompiler compiler = new(environment);
        CelProgram<bool> program = compiler.Compile("title = \"Dune\" AND pages > 100").GetProgramOrThrow();
        return program.Evaluate(new CelBindings().Add("title", "Dune").Add("pages", 412L)).GetValueOrThrow();
    }

    // The simple entry with a class and a list of classes in the properties of an anonymous object. The generator of call sites, which
    // runs in the compiler of this project as an analyzer, writes the registration of the object and the descriptions of the two classes
    // into this assembly, and the publish analyzes that code with the rest. The application is not run, so the call is only published.
    internal static bool Classes()
    {
        Customer customer = new() { Email = "ann@example.com" };
        List<OrderLine> lines = [new() { Sku = "a", Price = 2.5 }, new() { Sku = "b", Price = 4.0 }];
        return CelExpression.Evaluate<bool>(
            "customer.email.endsWith('@example.com') && lines.exists(l, l.price > 3.0)",
            new { customer, lines });
    }
}

// The classes of the application that the call of the simple entry reads: the descriptions of them are created, not written.
internal sealed class Customer
{
    public string Email { get; init; } = string.Empty;
}

internal sealed class OrderLine
{
    public string Sku { get; init; } = string.Empty;

    public double Price { get; init; }
}
