using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel;

/// <summary>
/// The result of <see cref="CelEnvironment.Validate{T}(string, CelCompileOptions)"/>: whether an expression compiles in an
/// environment, all its errors, the CEL type of its result and the names of the variables and functions it refers to,
/// without a program to evaluate.
/// </summary>
/// <remarks>
/// <para>
/// Validation runs the same compilation as <see cref="CelEnvironment.Compile{T}"/> — parsing, type checking and planning —
/// and gives the same errors; the program is not returned. An application validates an expression when a user saves it, and
/// shows each error with <see cref="CelError.ToDisplayString(string)"/>.
/// </para>
/// <para>
/// <see cref="ReferencedVariables"/> and <see cref="ReferencedFunctions"/> list names without repetitions, in the order of
/// <see cref="string.CompareOrdinal(string, string)"/>. When the expression has errors, they hold what was resolved before
/// the errors, which can be incomplete.
/// </para>
/// <para>The result is immutable and can be shared between threads.</para>
/// </remarks>
public sealed class CelValidationResult
{
    internal CelValidationResult(
        ImmutableArray<CelError> errors,
        CelType? resultType,
        ImmutableArray<string> referencedVariables,
        ImmutableArray<string> referencedFunctions,
        CelCheckedExpr? checkedExpr = null)
    {
        Errors = errors;
        ResultType = resultType;
        ReferencedVariables = referencedVariables;
        ReferencedFunctions = referencedFunctions;
        CheckedExpr = checkedExpr;
    }

    /// <summary>
    /// Whether the expression compiles: <see langword="true"/> when <see cref="Errors"/> is empty.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ResultType))]
    public bool IsValid => ResultType is not null;

    /// <summary>
    /// All errors of the compilation, each with its code, stage, message and place in the expression; empty when
    /// <see cref="IsValid"/> is <see langword="true"/>. The same errors as <see cref="CelCompileResult{T}.Errors"/> for the same
    /// text, type and options.
    /// </summary>
    public ImmutableArray<CelError> Errors { get; }

    /// <summary>
    /// The CEL type of the result that type checking found, for example <c>bool</c>, or <c>dyn</c> when the type is known
    /// only at evaluation; <see langword="null"/> when the expression has errors.
    /// </summary>
    public CelType? ResultType { get; }

    /// <summary>
    /// The names of the variables of the environment that the expression refers to, after name resolution in the container
    /// of the environment: <c>order</c> for <c>order.total &gt; 100</c>, <c>acme.sales.limit</c> for <c>limit</c> in the
    /// container <c>acme.sales</c> when that variable is declared. With an input type the names of its fields are included.
    /// </summary>
    /// <remarks>
    /// Local variables of macros, such as <c>x</c> in <c>items.all(x, x &gt; 0)</c> and the variables of <c>cel.bind</c>, are
    /// not included. A field path such as <c>order.total</c> is not listed: <c>order</c> is.
    /// </remarks>
    public ImmutableArray<string> ReferencedVariables { get; }

    /// <summary>
    /// The names of the functions and macros that the expression calls by name: functions of the standard library, of extension
    /// sets and of the application, and macros, such as <c>size</c>, <c>matches</c>, <c>math.greatest</c>, <c>all</c> or
    /// <c>isInn</c>.
    /// </summary>
    /// <remarks>
    /// A macro is listed by the name that the text writes, such as <c>has</c>, <c>map</c>, <c>cel.bind</c>, <c>sortBy</c> or
    /// <c>optMap</c>, although it expands to other calls; the names of those calls that the text does not write, such as
    /// <c>math.@max</c>, are not listed. Operators of the CEL language definition, such as <c>_+_</c>, <c>_[_]</c>, <c>!_</c> and <c>@in</c>, and internal
    /// names that start with <c>@</c> are not included. Overload identifiers are not listed: for an operand of the type
    /// <c>dyn</c> type checking keeps several overloads, so the name of the function is the only unambiguous answer.
    /// </remarks>
    public ImmutableArray<string> ReferencedFunctions { get; }

    /// <summary>
    /// The checked expression — the tree with the CEL type of every node and the references of its names and calls — when the
    /// expression is valid; <see langword="null"/> when it has errors.
    /// </summary>
    /// <remarks>
    /// A tool reads the types of the nodes, for example to show the type of a part of an expression; an application stores the
    /// checked expression with <see cref="CelCheckedExpr.ToProtobuf"/> or passes it to a service that reads <c>CheckedExpr</c>.
    /// A program does not keep its checked expression: validation is the call that gives it.
    /// </remarks>
    public CelCheckedExpr? CheckedExpr { get; }
}
