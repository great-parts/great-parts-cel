using System;
using System.Text;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;

namespace Great.Parts.Cel.Filtering;

/// <summary>
/// Translates list filters written in the AIP-160 syntax (<see href="https://google.aip.dev/160">google.aip.dev/160</see>)
/// into CEL expressions of an environment and compiles them.
/// </summary>
/// <remarks>
/// <para>
/// The fields of a filter are the variables of the environment and the fields of their types: an environment with an input
/// (<see cref="CelEnvironmentBuilder.UseInput{TInput}"/>) makes the members of the input - the fields of the resource of a
/// list method - the fields of the filter. The compiler reads the description of the environment once, when it is created,
/// and translates each value of a filter by the type of its field: <c>rating &gt;= 4</c> becomes <c>rating &gt;= 4.0</c> for
/// a <c>double</c> field, <c>published &gt; "2026-01-01T00:00:00Z"</c> becomes
/// <c>published &gt; timestamp("2026-01-01T00:00:00Z")</c> for a <c>timestamp</c> field.
/// </para>
/// <para>
/// The translation is an ordinary CEL expression of the environment, which the library type-checks and evaluates: <c>AND</c>
/// and a sequence of restrictions become <c>&amp;&amp;</c>, <c>OR</c> becomes <c>||</c>, <c>NOT</c> and <c>-</c> become
/// <c>!</c>; comparisons become the CEL operators; <c>:</c> becomes <c>in</c> for a list or a map, <c>has</c> for a field of an
/// object, <c>exists</c> through a repeated field; a string value with a leading or trailing <c>*</c> becomes
/// <c>startsWith</c>, <c>endsWith</c> or <c>contains</c>. An item whose composite field on the path of a restriction is not set
/// does not match, also for <c>!=</c>, as AIP-160 prescribes: <c>author.name != "X"</c> becomes
/// <c>author != null &amp;&amp; author.name != "X"</c>. The full translation table is in the documentation of the package.
/// </para>
/// <para>
/// Errors are returned, not thrown, with their place in the text of the filter in UTF-16 code units: a syntax error
/// (<see cref="CelErrorCode.FilterSyntaxError"/>), a value that does not fit the type of its field
/// (<see cref="CelErrorCode.InvalidFilterValue"/>), a restriction that the type of the field does not allow
/// (<see cref="CelErrorCode.InvalidFilterRestriction"/>), and the errors of type checking, such as
/// <see cref="CelErrorCode.UndeclaredReference"/> for a field that the environment does not declare. A filter longer than the
/// length limit of an expression, or with parentheses nested deeper than the nesting limit, is
/// <see cref="CelErrorCode.ExpressionTooLong"/> or <see cref="CelErrorCode.NestingTooDeep"/>.
/// </para>
/// <para>The compiler is immutable and can be called from any number of threads at once.</para>
/// </remarks>
public sealed class CelFilterCompiler
{
    // The longest filter, as the longest expression: 65 536 UTF-16 code units and 65 536 bytes of UTF-8.
    private const int MaxLength = 65_536;

    private readonly FieldCatalog _catalog;

    /// <summary>
    /// Creates a compiler of filters for an environment.
    /// </summary>
    /// <param name="environment">The environment whose variables are the fields of the filters.</param>
    /// <param name="options">The options, or <see langword="null"/> for the default options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The options do not fit the environment: a key of <see cref="CelFilterOptions.EnumTypes"/> names neither a variable
    /// nor a field of an object type of the environment, or the field is not of type <c>int</c>; a value names no
    /// enumeration type of the environment; a field of <see cref="CelFilterOptions.SearchFields"/> is not a path to a field of
    /// type <c>string</c> or the wrapper of <c>string</c>.
    /// </exception>
    public CelFilterCompiler(CelEnvironment environment, CelFilterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(environment);
        Environment = environment;
        Options = options ?? new CelFilterOptions();
        _catalog = new FieldCatalog(environment, Options);
    }

    /// <summary>
    /// The environment whose variables are the fields of the filters.
    /// </summary>
    public CelEnvironment Environment { get; }

    /// <summary>
    /// The options of the compiler; the default options when none were given.
    /// </summary>
    public CelFilterOptions Options { get; }

    /// <summary>
    /// Translates a filter into a CEL expression of the environment without type checking it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For an application that translates the tree further with its own code, for example into SQL: the positions of the
    /// nodes are in the text of the filter, and
    /// <see cref="CelEnvironment.ValidateParsed{T}(CelParsedExpr, CelCompileOptions)"/> of the tree gives the CEL type of each
    /// node. <see cref="CelSyntax.Format(CelParsedExpr)"/> of the tree gives its CEL text, which parses in the environment.
    /// </para>
    /// <para>
    /// The errors are the syntax error, or all errors of the translation: values that do not fit their fields and restrictions
    /// that their fields do not allow. An empty filter, or a filter of white space only, is the expression <c>true</c>.
    /// </para>
    /// </remarks>
    /// <param name="filter">The text of the filter.</param>
    /// <returns>The CEL expression of the filter, or all errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> is <see langword="null"/>.</exception>
    public CelParseResult Parse(string filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.Length > MaxLength || Encoding.UTF8.GetByteCount(filter) > MaxLength)
        {
            // The place of the error is none: a length is not a place in the text.
            CelError tooLong = new CelExprFactory().CreateError(CelErrorCode.ExpressionTooLong, "The filter is longer than 65536 UTF-8 bytes.");
            return CelParseResult.FromErrors([tooLong]);
        }

        FilterParseResult parsed = FilterParser.Parse(filter);
        if (parsed.Error is { } error)
        {
            CelExprFactory factory = new(filter)
            {
                Position = error.Start..error.End,
            };
            return CelParseResult.FromErrors([factory.CreateError(error.Code, error.Message)]);
        }

        return FilterTranslator.Translate(filter, parsed.Root!, _catalog);
    }

    /// <summary>
    /// Translates a filter into a CEL expression of the environment and compiles it to a program with a <c>bool</c> result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The result is <see cref="Parse(string)"/> followed by
    /// <see cref="CelEnvironment.CompileParsed{T}(CelParsedExpr, CelCompileOptions)"/>: when the translation has errors, they
    /// are the errors of the result and the tree is not type-checked; otherwise the result is that of the compilation, with
    /// every error of type checking at its place in the filter. The program tells for each item whether it matches the
    /// filter, for example with <see cref="CelProgram{T}.EvaluateInput{TInput}(TInput, CelBindings, CelEvalOptions, System.Threading.CancellationToken)"/>.
    /// </para>
    /// <para>
    /// The compiler keeps no cache: an application that compiles the same filter often can keep the program by the text of
    /// the filter.
    /// </para>
    /// </remarks>
    /// <param name="filter">The text of the filter.</param>
    /// <returns>The program, or all errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> is <see langword="null"/>.</exception>
    public CelCompileResult<bool> Compile(string filter)
    {
        CelParseResult parsed = Parse(filter);
        return parsed.IsSuccess
            ? Environment.CompileParsed<bool>(parsed.ParsedExpr)
            : CelCompileResult<bool>.FromErrors(parsed.Errors);
    }
}
