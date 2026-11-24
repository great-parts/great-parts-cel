using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Filtering;
using Great.Parts.Cel.Syntax;

namespace Great.Parts.Cel.Conformance.FilterCorpus;

/// <summary>
/// Runs a case of the corpus and judges it: the options of the case build the compiler, then the filter must give the expected error, or
/// the expected tree, and the expected results on the items. For a filter that translates, the text of its tree must compile in the same
/// environment and give the same results again.
/// </summary>
internal sealed class FilterJudge
{
    // The compilers by the options of the cases: a case with the same options shares one.
    private readonly Dictionary<string, CelFilterCompiler> _compilers = new(StringComparer.Ordinal);

    /// <summary>Runs a case.</summary>
    /// <param name="test">The case.</param>
    internal Judgement Judge(FilterTest test)
    {
        ArgumentNullException.ThrowIfNull(test);
        try
        {
            string filter = FilterText(test);
            CelFilterCompiler compiler = CompilerOf(test);
            return test.Error is { } expected ? JudgeError(compiler, filter, expected) : JudgeProgram(compiler, filter, test);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return Judgement.Fail($"the case threw {exception.GetType().Name}: {exception.Message}");
        }
    }

    // The text of the filter: the unit written repeat times and the tail, or the filter itself.
    private static string FilterText(FilterTest test)
    {
        if (test.Repeat <= 0)
        {
            return test.Filter;
        }

        StringBuilder text = new(test.Filter.Length * test.Repeat + test.Tail.Length);
        for (int index = 0; index < test.Repeat; index++)
        {
            text.Append(test.Filter);
        }

        return text.Append(test.Tail).ToString();
    }

    private CelFilterCompiler CompilerOf(FilterTest test)
    {
        string key = string.Join('|', test.EnumTypes.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value))
            + "#" + string.Join('|', test.SearchFields);
        if (_compilers.TryGetValue(key, out CelFilterCompiler? known))
        {
            return known;
        }

        ImmutableDictionary<string, string> enumTypes = ImmutableDictionary<string, string>.Empty.AddRange(test.EnumTypes);
        CelFilterCompiler compiler = new(FilterEnvironment.Value, new CelFilterOptions { EnumTypes = enumTypes, SearchFields = [.. test.SearchFields] });
        _compilers.Add(key, compiler);
        return compiler;
    }

    private static Judgement JudgeError(CelFilterCompiler compiler, string filter, ErrorExpectation expected)
    {
        CelCompileResult<bool> result = compiler.Compile(filter);
        if (result.IsSuccess)
        {
            return Judgement.Fail($"the filter compiles, the error {expected.Code} was expected");
        }

        ImmutableArray<CelError> errors = result.Errors;
        CelError first = errors[0];
        if (!string.Equals(first.Code.ToString(), expected.Code, StringComparison.Ordinal)
            || first.Span.Start.Offset != expected.Start
            || first.Span.Length != expected.Length)
        {
            return Judgement.Fail(string.Create(CultureInfo.InvariantCulture, $"the first error is {Describe(first)}; expected {expected.Code} at ({expected.Start}, {expected.Length})"));
        }

        if (expected.Subject.Length > 0 && !string.Equals(first.Subject, expected.Subject, StringComparison.Ordinal))
        {
            return Judgement.Fail($"the subject of the first error is '{first.Subject}', expected '{expected.Subject}'");
        }

        if (expected.ErrorCount > 0 && errors.Length != expected.ErrorCount)
        {
            return Judgement.Fail(string.Create(CultureInfo.InvariantCulture, $"the filter has {errors.Length} errors, expected {expected.ErrorCount}"));
        }

        return expected.LastCode.Length > 0 && !string.Equals(errors[^1].Code.ToString(), expected.LastCode, StringComparison.Ordinal)
            ? Judgement.Fail($"the last error is {errors[^1].Code}, expected {expected.LastCode}")
            : Judgement.Pass;
    }

    private static Judgement JudgeProgram(CelFilterCompiler compiler, string filter, FilterTest test)
    {
        CelParseResult parsed = compiler.Parse(filter);
        if (!parsed.IsSuccess)
        {
            return Judgement.Fail("the filter does not translate: " + Describe(parsed.Errors));
        }

        if (test.Cel.Length > 0)
        {
            CelParseResult expected = FilterEnvironment.Value.Parse(test.Cel);
            if (!expected.IsSuccess)
            {
                return Judgement.Fail($"the expected CEL '{test.Cel}' does not parse: " + Describe(expected.Errors));
            }

            if (SyntaxTreeComparer.Difference(parsed.ParsedExpr.Root, expected.ParsedExpr.Root) is { } difference)
            {
                return Judgement.Fail($"the tree differs from '{test.Cel}': {difference}; the tree is '{Shorten(CelSyntax.Format(parsed.ParsedExpr))}'");
            }
        }

        CelCompileResult<bool> compiled = compiler.Compile(filter);
        if (!compiled.IsSuccess)
        {
            return Judgement.Fail("the filter does not compile: " + Describe(compiled.Errors));
        }

        // The text of the tree: it compiles in the same environment and gives the same results as the program of the filter.
        string printed = CelSyntax.Format(parsed.ParsedExpr);
        CelCompileResult<bool> again = FilterEnvironment.Value.Compile<bool>(printed);
        if (!again.IsSuccess)
        {
            return Judgement.Fail($"the text of the tree '{Shorten(printed)}' does not compile: " + Describe(again.Errors));
        }

        for (int index = 0; index < test.Resource.Count; index++)
        {
            Item item = test.Resource[index].Value ?? new Item();
            bool expected = test.Resource[index].Match;
            string? problem = Evaluate(compiled.GetProgramOrThrow(), item, expected);
            if (problem is null && Evaluate(again.GetProgramOrThrow(), item, expected) is { } printedProblem)
            {
                problem = "the printed tree: " + printedProblem;
            }

            if (problem is not null)
            {
                return Judgement.Fail(string.Create(CultureInfo.InvariantCulture, $"resource {index + 1}: {problem}; the tree is '{Shorten(printed)}'"));
            }
        }

        return Judgement.Pass;
    }

    // Null when the program gives the expected result on the item; the problem otherwise.
    private static string? Evaluate(CelProgram<bool> program, Item item, bool expected)
    {
        CelEvalResult<bool> result = program.EvaluateInput(item, cancellationToken: CancellationToken.None);
        if (!result.IsSuccess)
        {
            return "the evaluation failed: " + string.Join("; ", result.Errors.Select(error => error.Message));
        }

        return result.Value == expected ? null : string.Create(CultureInfo.InvariantCulture, $"the result is {result.Value}, expected {expected}");
    }

    private static string Describe(ImmutableArray<CelError> errors) => string.Join("; ", errors.Take(3).Select(Describe));

    private static string Describe(CelError error) =>
        string.Create(CultureInfo.InvariantCulture, $"{error.Code} at ({error.Span.Start.Offset}, {error.Span.Length}): {error.Message}");

    private static string Shorten(string text) => text.Length <= 300 ? text : string.Concat(text.AsSpan(0, 300), "...");
}
