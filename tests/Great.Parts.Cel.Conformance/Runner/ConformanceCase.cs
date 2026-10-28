using System.Collections.Generic;
using Cel.Expr;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>A case of a suite in the common model of both suites.</summary>
/// <param name="Name">The name, <c>&lt;file&gt;/&lt;section&gt;/&lt;test&gt;</c> from the names in the file, as the cel-go runner names it.</param>
/// <param name="Path">The path of the text file of the case relative to the repository root.</param>
/// <param name="Line">The line of the <c>test</c> token of the case, starting with 1.</param>
/// <param name="Column">The column of the <c>test</c> token of the case, starting with 1.</param>
/// <param name="Expression">The CEL expression.</param>
/// <param name="Environment">What the case adds to the common setup.</param>
/// <param name="Bindings">The values of the variables by name.</param>
/// <param name="Check">Whether the expression is checked, evaluated without a check, or only checked.</param>
/// <param name="Expected">The expected result.</param>
internal sealed record ConformanceCase(
    string Name,
    string Path,
    int Line,
    int Column,
    string Expression,
    CaseEnvironment Environment,
    IReadOnlyDictionary<string, ExprValue> Bindings,
    CheckMode Check,
    Expectation Expected);

/// <summary>What a case adds to the common setup of the cases.</summary>
/// <param name="Container">The container, or an empty string.</param>
/// <param name="Declarations">The declarations of <c>type_env</c>.</param>
/// <param name="DisableMacros">Whether the case is parsed without macros.</param>
internal sealed record CaseEnvironment(string Container, IReadOnlyList<Decl> Declarations, bool DisableMacros);

/// <summary>How a case treats type checking.</summary>
internal enum CheckMode
{
    /// <summary>The expression is checked, then evaluated.</summary>
    Check,

    /// <summary>The expression is evaluated without a check: <c>disable_check</c>.</summary>
    WithoutCheck,

    /// <summary>The expression is checked and not evaluated: <c>check_only</c>.</summary>
    CheckOnly,
}

/// <summary>The kind of the expected result of a case.</summary>
internal enum ExpectationKind
{
    /// <summary>A value: the matcher <c>value</c>, or no matcher, which expects <c>true</c>.</summary>
    Value,

    /// <summary>A value and the deduced type: the matcher <c>typed_result</c>.</summary>
    TypedResult,

    /// <summary>An evaluation error: the matcher <c>eval_error</c>.</summary>
    EvaluationError,

    /// <summary>A matcher the rule does not accept: <c>any_eval_errors</c>, <c>unknown</c> or <c>any_unknowns</c>.</summary>
    Other,
}

/// <summary>The expected result of a case.</summary>
/// <param name="Kind">The kind of the result.</param>
/// <param name="Matcher">The name of the matcher in the cel-spec format, or <c>none</c>.</param>
/// <param name="Value">The expected value of <see cref="ExpectationKind.Value"/> and <see cref="ExpectationKind.TypedResult"/>.</param>
/// <param name="DeducedType">The expected type of <see cref="ExpectationKind.TypedResult"/>.</param>
internal sealed record Expectation(ExpectationKind Kind, string Matcher, Value? Value, Type? DeducedType);
