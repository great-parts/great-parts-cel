using System.Collections.Generic;
using Great.Parts.Cel.Policy.Yaml;

namespace Great.Parts.Cel.Conformance.CelPolicy;

/// <summary>The kind of the expected output of a test of a category.</summary>
internal enum PolicyOutputKind
{
    /// <summary>A literal value: <c>output.value</c>.</summary>
    Value,

    /// <summary>The value of an expression: <c>output.expr</c>.</summary>
    Expression,

    /// <summary>A failed compilation: <c>output.error_set</c>.</summary>
    ErrorSet,

    /// <summary>An output the runner does not know, such as <c>unknown_set</c>.</summary>
    Other,
}

/// <summary>The expected output of a test.</summary>
/// <param name="Kind">The kind of the output.</param>
/// <param name="Text">The expression of <see cref="PolicyOutputKind.Expression"/>, or the name of the key of <see cref="PolicyOutputKind.Other"/>.</param>
/// <param name="Value">The value of <see cref="PolicyOutputKind.Value"/>.</param>
/// <param name="Errors">The strings of <see cref="PolicyOutputKind.ErrorSet"/>.</param>
internal sealed record PolicyOutput(PolicyOutputKind Kind, string Text, YamlNode? Value, IReadOnlyList<string> Errors);

/// <summary>An input of a test: the value of a variable, a literal or an expression, never both.</summary>
/// <param name="Expression">The expression, or <see langword="null"/> for a literal.</param>
/// <param name="Value">The literal, or <see langword="null"/> for an expression.</param>
internal sealed record PolicyInput(string? Expression, YamlNode? Value);

/// <summary>A test of a category.</summary>
/// <param name="Name">The name <c>&lt;category&gt;/&lt;section&gt;/&lt;test&gt;</c>, as the skip list names the case.</param>
/// <param name="Path">The path of <c>tests.yaml</c> relative to the repository root.</param>
/// <param name="Line">The line of the name of the test, starting with 1.</param>
/// <param name="Column">The column of the name of the test, starting with 1.</param>
/// <param name="Inputs">The inputs by the name of the variable, in the order of the file.</param>
/// <param name="ContextExpression">The expression of <c>context_expr</c>, or <see langword="null"/>.</param>
/// <param name="Output">The expected output.</param>
internal sealed record PolicyTest(
    string Name,
    string Path,
    int Line,
    int Column,
    IReadOnlyList<KeyValuePair<string, PolicyInput>> Inputs,
    string? ContextExpression,
    PolicyOutput Output);

/// <summary>
/// A category of the suite: a directory with <c>policy.yaml</c>, <c>tests.yaml</c> and, for most, <c>config.yaml</c>. The policy is compiled once
/// in the environment of the configuration, and every test of the category evaluates that program.
/// </summary>
/// <param name="Name">The name of the directory relative to <c>tests/cel-policy</c>.</param>
/// <param name="PolicyText">The text of <c>policy.yaml</c>.</param>
/// <param name="Config">The root of <c>config.yaml</c>, or <see langword="null"/> when the category has none: an environment without declarations.</param>
/// <param name="Tests">The tests of <c>tests.yaml</c> in the order of the file.</param>
internal sealed record PolicyCategory(string Name, string PolicyText, YamlMapping? Config, IReadOnlyList<PolicyTest> Tests);
