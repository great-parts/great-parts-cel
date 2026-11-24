using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Policy.Yaml;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Policy;

// The compilation of a policy in an environment, as the compiler and the composer of cel-go (policy/compiler.go, composer.go) do it, over the
// public API of the core only. Every expression is parsed with the syntax of the environment; the copies of their trees make one probe,
// cel.@block([the variables], [the conditions and the outputs]), which type checking checks once; the checks of the policy read the types
// of the probe; the composition builds the program tree from the same parsed trees; and the core compiles that tree.
//
// The compilation is linear in the size of the document and takes no stack for the depth of its rules: the rules are walked with an
// explicit stack, and only a policy that passed the limit of nested rules, a hundred, is composed by recursion.
internal sealed partial class PolicyCompiler : ISlotLookup
{
    // The limit of variables and nested rules together.
    private const int MaxNested = 100;

    private readonly CelPolicy _policy;
    private readonly CelEnvironment _environment;
    private readonly LineTable _lines;
    private readonly CelExprFactory _factory;
    private readonly CelCompileOptions _documentPosition;
    private readonly int _unnestHeight;
    private readonly List<(int Offset, int Sequence, CelError Error)> _errors = [];

    // The environment of the policy: the one given, or a copy of it that has the imports as abbreviations.
    private CelEnvironment _policyEnvironment;

    // The environment that checks the probe: the one of the policy, with the set bindings added when it has not, so that a policy
    // whose environment lacks the set gets its one error about the set and not an error at every variable.
    private CelEnvironment _checkEnvironment;

    private PolicyCompiler(CelPolicy policy, CelEnvironment environment, int unnestHeight)
    {
        _policy = policy;
        _environment = environment;
        _unnestHeight = unnestHeight;
        _policyEnvironment = environment;
        _checkEnvironment = environment;
        _lines = new LineTable(policy.Text);
        _factory = new CelExprFactory(policy.Text);
        _documentPosition = new CelCompileOptions { SourceLocation = new CelSourceLocation(policy.SourceName, 0, 1, 1) };
    }

    // Compiles a policy: the program of the composed expression in the environment of the policy, or all errors.
    internal static CelPolicyCompileResult<T> Compile<T>(CelPolicy policy, CelEnvironment environment, CelType? expectedType, int unnestHeight)
    {
        var compiler = new PolicyCompiler(policy, environment, unnestHeight);
        CelParsedExpr? expression = compiler.Run();
        if (expression is null)
        {
            CheckArguments<T>(environment, expectedType);
            return new CelPolicyCompileResult<T>(compiler.SortedErrors());
        }

        CelCompileOptions options = new() { ExpectedType = expectedType, SourceLocation = compiler._documentPosition.SourceLocation };
        CelCompileResult<T> compiled = compiler._policyEnvironment.CompileParsed<T>(expression, options);
        return compiled.IsSuccess
            ? new CelPolicyCompileResult<T>(compiled.GetProgramOrThrow(), expression)
            : new CelPolicyCompileResult<T>(Sorted(compiled.Errors));
    }

    // A policy that does not compile still throws for a type argument or an expected type that no program could have, as every
    // compilation does, whatever the document says: the core checks them with any tree.
    private static void CheckArguments<T>(CelEnvironment environment, CelType? expectedType)
    {
        var factory = new CelExprFactory();
        CelParsedExpr tree = factory.Build(factory.Constant(CelValue.FromBoolean(true)));
        _ = environment.ValidateParsed<T>(tree, new CelCompileOptions { ExpectedType = expectedType });
    }

    // The composed expression, or null when there are errors.
    private CelParsedExpr? Run()
    {
        ExtensionSupport support = ExtensionSupport.Of(_environment);
        BuildEnvironments(support);
        Walk();
        CheckProbe();
        if (_errors.Count > 0 || _root is null)
        {
            return null;
        }

        var composer = new PolicyComposer(_factory, _unnestHeight);
        CelParsedExpr composed = composer.Compose(_root);
        ReportMissingSets(composer, support);
        return _errors.Count > 0 ? null : composed;
    }

    // The environment of the policy with its imports, and the one that checks.
    private void BuildEnvironments(ExtensionSupport support)
    {
        _policyEnvironment = WithImports(_environment);
        _checkEnvironment = _policyEnvironment;
        if (!support.Bindings)
        {
            CelBuildResult withBindings = _policyEnvironment.ToBuilder().AddExtension(CelExtensions.Bindings(1)).Build();
            if (withBindings.IsSuccess)
            {
                _checkEnvironment = withBindings.GetEnvironmentOrThrow();
            }
        }
    }

    // Each import is an abbreviation of the copy of the environment. An import that the environment rejects is an error at the import,
    // and the policy goes on without it, as cel-go goes on with the imports that are valid.
    private CelEnvironment WithImports(CelEnvironment environment)
    {
        List<PolicyImport> imports = _policy.Document.Imports;
        if (imports.Count == 0)
        {
            return environment;
        }

        CelBuildResult built = BuildWith(environment, imports, null);
        if (built.IsSuccess)
        {
            return built.GetEnvironmentOrThrow();
        }

        HashSet<string> rejected = new(StringComparer.Ordinal);
        foreach (CelError error in built.Errors)
        {
            PolicyImport import = ImportOf(imports, error.Subject);
            rejected.Add(NameOf(import));
            Report(error.Code, error.Message, import.Name?.Node ?? import.Node, error.Subject);
        }

        CelBuildResult remaining = BuildWith(environment, imports, rejected);
        return remaining.IsSuccess ? remaining.GetEnvironmentOrThrow() : environment;
    }

    private static CelBuildResult BuildWith(CelEnvironment environment, List<PolicyImport> imports, HashSet<string>? rejected)
    {
        CelEnvironmentBuilder builder = environment.ToBuilder();
        foreach (PolicyImport import in imports)
        {
            string name = NameOf(import);
            if (rejected is null || !rejected.Contains(name))
            {
                builder.AddAbbreviation(name);
            }
        }

        return builder.Build();
    }

    // The name of an import without its surrounding white space, the name that the environment checks and names in its errors: a name
    // in a block of YAML has the indentation of the block. The imports of the policy list the same names.
    internal static string NameOf(PolicyImport import) => (import.Name?.Text ?? string.Empty).Trim();

    // The import the subject of an error of the environment names; the first import when it names none.
    private static PolicyImport ImportOf(List<PolicyImport> imports, string? subject)
    {
        foreach (PolicyImport import in imports)
        {
            if (string.Equals(NameOf(import), subject, StringComparison.Ordinal))
            {
                return import;
            }
        }

        return imports[0];
    }

    // The composed expression needs the sets optional and bindings when it uses them; an environment without one is told so once.
    private void ReportMissingSets(PolicyComposer composer, ExtensionSupport support)
    {
        if (composer.UsesBlock && !support.Bindings)
        {
            ReportMissingSet("bindings", "the composed expression uses cel.@block, a function of the extension set 'bindings'");
        }

        if (composer.UsesOptional && !support.Optional)
        {
            ReportMissingSet("optional", "the composed expression uses optional values, a feature of the extension set 'optional'");
        }
    }

    private void ReportMissingSet(string set, string message) =>
        _errors.Add((-1, _errors.Count, CelError.Create(
            CelErrorCode.MissingExtensionDependency,
            CelErrorStage.Check,
            message + ", which the environment does not have",
            subject: set)));

    // ---- Slots of the probe ----

    // The slot of a variable of the policy at the place being copied: the visible declarations are those of the rules being walked.
    bool ISlotLookup.TryGetSlot(string name, out int slot) => _visible.TryGetValue(name, out slot);

    // ---- Errors ----

    private CelSourceLocation Locate(int offset)
    {
        (int line, int column) = _lines.Locate(offset);
        return new CelSourceLocation(_policy.SourceName, offset, line, column);
    }

    // An error of the policy at a node of the document: the place and the length of the node, as the reader reports them.
    private void Report(
        CelErrorCode code,
        string message,
        YamlNode node,
        string? subject = null,
        CelType? expectedType = null,
        CelType? actualType = null)
    {
        (int offset, int length) = PolicyReader.Place(node);
        _errors.Add((offset, _errors.Count, CelError.Create(code, CelErrorStage.Check, message, Locate(offset), length, subject, expectedType, actualType)));
    }

    // Errors that the core reports, each with its place in the document.
    private void AddAll(ImmutableArray<CelError> errors)
    {
        foreach (CelError error in errors)
        {
            _errors.Add((OffsetOf(error), _errors.Count, error));
        }
    }

    private static int OffsetOf(CelError error) => error.SourceLocation?.Offset ?? -1;

    // The errors in the order of their places in the document; an error without a place comes first.
    private ImmutableArray<CelError> SortedErrors() => Ordered(_errors);

    private static ImmutableArray<CelError> Sorted(ImmutableArray<CelError> errors)
    {
        List<(int Offset, int Sequence, CelError Error)> list = new(errors.Length);
        foreach (CelError error in errors)
        {
            list.Add((OffsetOf(error), list.Count, error));
        }

        return Ordered(list);
    }

    private static ImmutableArray<CelError> Ordered(List<(int Offset, int Sequence, CelError Error)> list)
    {
        list.Sort(static (a, b) => a.Offset != b.Offset ? a.Offset.CompareTo(b.Offset) : a.Sequence.CompareTo(b.Sequence));
        ImmutableArray<CelError>.Builder errors = ImmutableArray.CreateBuilder<CelError>(list.Count);
        foreach ((int _, int _, CelError error) in list)
        {
            errors.Add(error);
        }

        return errors.MoveToImmutable();
    }
}
