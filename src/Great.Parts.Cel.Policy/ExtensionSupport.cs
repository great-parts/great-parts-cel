using System.Runtime.CompilerServices;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Policy;

// Which of the extension sets that the composed expression of a policy may need an environment has: optional, for the optional values
// of a rule that may give no output, and bindings, for cel.@block. The package tells it from the environment by checking a tree of two
// nodes that uses the set, once for an environment, which the cache keeps as long as the environment lives.
internal sealed class ExtensionSupport
{
    private static readonly ConditionalWeakTable<CelEnvironment, ExtensionSupport> Cache = new();

    private ExtensionSupport(bool optional, bool bindings)
    {
        Optional = optional;
        Bindings = bindings;
    }

    // Whether the environment declares optional.of, and with it the type optional_type.
    internal bool Optional { get; }

    // Whether the environment declares cel.@block.
    internal bool Bindings { get; }

    internal static ExtensionSupport Of(CelEnvironment environment) => Cache.GetValue(environment, Probe);

    private static ExtensionSupport Probe(CelEnvironment environment)
    {
        var optional = new CelExprFactory();
        CelParsedExpr optionalTree = optional.Build(optional.Call("optional.of", optional.Constant(CelValue.FromBoolean(true))));
        var block = new CelExprFactory();
        CelParsedExpr blockTree = block.Build(block.Call("cel.@block", block.List([]), block.Constant(CelValue.FromBoolean(true))));
        return new ExtensionSupport(environment.ValidateParsed(optionalTree).IsValid, environment.ValidateParsed(blockTree).IsValid);
    }
}
