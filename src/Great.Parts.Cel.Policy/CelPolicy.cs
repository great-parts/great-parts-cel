using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Policy;

/// <summary>
/// A policy in the CEL Policy format, read from a YAML document: a named rule of variables and matches, each match with a
/// condition and an output or a nested rule, that compiles into one CEL program.
/// </summary>
/// <remarks>
/// <para>
/// The format is that of the policies of cel-go: the keys <c>name</c>, <c>description</c>, <c>imports</c> and <c>rule</c>;
/// a rule has <c>id</c>, <c>description</c>, <c>variables</c> and either <c>match</c> (first match wins) or
/// <c>aggregate</c> (the outputs of all matches, as a list); a match has <c>condition</c> (by default <c>true</c>) and
/// either <c>output</c> with an optional <c>explanation</c> or a nested <c>rule</c>. A variable is used in later expressions
/// of its rule and of the nested rules as <c>variables.name</c>, evaluated once and only when used.
/// </para>
/// <para>
/// As in cel-go, the text of an expression written as a YAML block scalar (<c>|</c> or <c>&gt;</c>) is the lines of the
/// document as they stand, with their indentation and line breaks; the text of any other string scalar is its YAML value.
/// Every error (of the YAML, of the structure of the policy, of parsing, type checking and evaluating its expressions)
/// reports its line and column in the document in <see cref="CelError.SourceLocation"/>, named by <see cref="SourceName"/>.
/// </para>
/// <para>
/// The library reads the document from the string it is given and opens no files. The policy is immutable and can be shared
/// between threads; one policy compiles in any number of environments.
/// </para>
/// </remarks>
public sealed class CelPolicy
{
    internal CelPolicy(string text, string sourceName, PolicyDocument document)
    {
        Text = text;
        SourceName = sourceName;
        Document = document;
        Imports = BuildImports(document);
    }

    /// <summary>
    /// The text of the document the policy was read from.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The name of the document, given to <see cref="Parse(string, string)"/>, that every error of the policy reports in
    /// <see cref="CelSourceLocation.SourceName"/>.
    /// </summary>
    public string SourceName { get; }

    /// <summary>
    /// The value of the key <c>name</c> of the policy, or <see langword="null"/> when the document has none.
    /// </summary>
    public string? Name => Document.Name?.Text;

    /// <summary>
    /// The value of the key <c>description</c> of the policy, or <see langword="null"/> when the document has none.
    /// </summary>
    public string? Description => Document.Description;

    // What the document says, with the nodes it comes from; the compilation of the policy reads it.
    internal PolicyDocument Document { get; }

    /// <summary>
    /// The qualified type names of the key <c>imports</c>, in the order of the document, each without its surrounding white
    /// space; empty when there are none.
    /// </summary>
    /// <remarks>
    /// A name written as a YAML block scalar has the indentation and the line break of the block in the document; the list has the
    /// names as the compilation reads them, trimmed of surrounding white space. At compilation each name becomes an abbreviation (<see cref="CelEnvironmentBuilder.AddAbbreviation(string)"/>): the
    /// expressions of the policy can name the type by the last part of its name.
    /// </remarks>
    public ImmutableArray<string> Imports { get; }

    /// <summary>
    /// Reads a policy from the text of a YAML document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Any text gives a result: a text that is not YAML is the error <see cref="CelErrorCode.InvalidYaml"/>, a YAML document
    /// that is not a policy is <see cref="CelErrorCode.InvalidPolicy"/>, each at its position in the document. The expressions
    /// are not parsed here; <see cref="Compile{T}(CelEnvironment, CelCompileOptions)"/> parses them with the syntax of its
    /// environment.
    /// </para>
    /// <para>
    /// The YAML is read as cel-go reads it: YAML 1.2 with the resolution of plain scalars of the YAML library of cel-go, the
    /// first document of the stream, anchors and aliases.
    /// </para>
    /// <para>
    /// An alias stands for its node at every place it is used, so the reader limits what a small document can stand for. A
    /// document in which the rules, matches and variables together would number more than 100000, or in which the strings
    /// that the reader builds (the names, descriptions, conditions, outputs and explanations, with a place in the document for
    /// each escape sequence or line break) and the keys and tags that its errors repeat would together be longer than four times
    /// the length of the document plus 65536 characters, is the error <see cref="CelErrorCode.InvalidPolicy"/> at the node
    /// where the limit is passed, and the reader stops there; the errors it found before are reported too. A document without
    /// aliases and without errors never passes the second limit.
    /// </para>
    /// </remarks>
    /// <param name="text">The text of the document.</param>
    /// <param name="sourceName">The name of the document, for example a file path; shown in positions, not opened.</param>
    /// <returns>The policy, or all errors of the document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="sourceName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sourceName"/> is empty.</exception>
    public static CelPolicyParseResult Parse(string text, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        return PolicyReader.Read(text, sourceName);
    }

    // The names of the imports in the order of the document, trimmed as the compilation trims them; an import without a name has the
    // empty name, which the compilation reports.
    private static ImmutableArray<string> BuildImports(PolicyDocument document)
    {
        ImmutableArray<string>.Builder names = ImmutableArray.CreateBuilder<string>(document.Imports.Count);
        foreach (PolicyImport import in document.Imports)
        {
            names.Add(PolicyCompiler.NameOf(import));
        }

        return names.MoveToImmutable();
    }

    /// <summary>
    /// Returns the same policy in which every match with an output outputs its <c>explanation</c> instead, and the matches
    /// with an output but without an explanation are left out, as the explanation policy of cel-go. The imports stay.
    /// </summary>
    /// <returns>The policy of explanations.</returns>
    public CelPolicy WithExplanationOutputs() => ExplanationPolicy.Of(this);

    /// <summary>
    /// Compiles the policy in an environment into one program.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every expression is parsed with the syntax of the environment and type-checked once; a variable has the type of its
    /// expression. Then the policy is checked as cel-go checks it: a condition is of type <c>bool</c>
    /// (<see cref="CelErrorCode.ExpectedTypeMismatch"/>), the outputs of a rule agree in type
    /// (<see cref="CelErrorCode.IncompatibleOutputTypes"/>), no match is unreachable and no condition is the literal
    /// <c>false</c> (<see cref="CelErrorCode.UnreachableMatch"/>), a variable does not redeclare a visible variable with another
    /// type (<see cref="CelErrorCode.OverlappingVariable"/>), and the policy has at most 100 variables and nested rules
    /// (<see cref="CelErrorCode.InvalidPolicy"/>). All errors are collected; the checks that read types run once every
    /// expression has parsed and type-checked, so the errors of the expressions are reported before them.
    /// </para>
    /// <para>
    /// The policy becomes one expression, as cel-go composes it: first-match rules chain conditionals, with
    /// <c>optional.of</c> and <c>optional.none()</c> when not every path gives an output; aggregate rules concatenate lists;
    /// variables are the slots of <c>cel.@block</c>, evaluated once and only when used. The result type of the policy is the
    /// type of its outputs, <c>optional_type</c> of it when not every path gives an output, and <c>list</c> of it for
    /// aggregate. The composed expression needs the extension sets <c>optional</c> and <c>bindings</c> when it uses them;
    /// an environment without them is the error <see cref="CelErrorCode.MissingExtensionDependency"/>.
    /// </para>
    /// <para>
    /// With <see cref="Imports"/> the policy compiles in a copy of the environment with an abbreviation for each import; the
    /// environment itself does not change. Errors of evaluation of the program report their position in the document.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The .NET type in which the program returns the result of the policy, as for
    /// <see cref="CelEnvironment.Compile{T}(string, CelCompileOptions)"/>.</typeparam>
    /// <param name="environment">The environment that declares the variables and functions the expressions use.</param>
    /// <param name="options">
    /// The options, or <see langword="null"/> for none; only <see cref="CelCompileOptions.ExpectedType"/> is read.
    /// </param>
    /// <returns>The program and the composed expression, or all errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="T"/> is not in the result column of the type mapping; the expected type of
    /// <paramref name="options"/> cannot be converted to <typeparamref name="T"/>; or <paramref name="options"/> sets
    /// <see cref="CelCompileOptions.SourceLocation"/> or <see cref="CelCompileOptions.SourceSegments"/>, which the policy sets
    /// itself.
    /// </exception>
    public CelPolicyCompileResult<T> Compile<T>(CelEnvironment environment, CelCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (options is not null && (options.SourceLocation is not null || !options.SourceSegments.IsDefaultOrEmpty))
        {
            throw new ArgumentException("The policy sets the position of its expressions itself, so the options set neither SourceLocation nor SourceSegments.", nameof(options));
        }

        return PolicyCompiler.Compile<T>(this, environment, options?.ExpectedType, ExpressionUnnester.DefaultHeight);
    }
}
