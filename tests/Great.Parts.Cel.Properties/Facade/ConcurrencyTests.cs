using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using CsCheck;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Properties.Evaluation;
using Great.Parts.Cel.Properties.Generators;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using PatternCache = Great.Parts.Cel.Library.Regex.PatternCache;

namespace Great.Parts.Cel.Properties.Facade;

// One environment and one program called from many threads at once give the results of sequential calls
// (docs/design/tests.md, "Properties of evaluation"). 64 threads, started together, each run every item of four scenarios in
// an order of their own: the evaluation of one program per text on its bindings, Compile of the texts, the one-step call, and
// GetOrCompile with Evaluate of one compilation cache whose capacities hold 16 entries. The texts read the application data of
// the generator — objects of the application with their descriptions, carriers of types described at run time, JsonElement — so
// the threads read the same instances, descriptions and documents at once. A quarter of the texts call matches — a literal
// pattern, a pattern from a variable in both call styles, a macro over a list of strings, and a literal that is refused — and each
// binding holds a pattern of a set three times as large as the cache of patterns of the environment, valid, refused and over the
// size limit, so that the cache evicts while the threads read and insert. The one environment is the one cache they share.
public sealed partial class ConcurrencyTests
{
    private const ulong Seed = 20261110;
    private const int Cases = 2;
    private const int Threads = 64;
    private const int Texts = 64;
    private const int BindingsPerText = 16;
    private const int CacheEntries = 16;
    private const ulong CostLimit = 20;
    private const int MatchesTextsEvery = 4;

    // The environment of the generator with two application functions: a typed delegate and a CelFunctionImplementation.
    private static readonly CelEnvironment Environment = TypedExpressions.Declare(new CelEnvironmentBuilder(CelStandardLibrary.V1))
        .AddVariable("p", CelType.String)
        .AddVariable("ss", CelType.ListOf(CelType.String))
        .AddFunction("half", (long value) => value / 2)
        .AddFunction(new CelFunctionDecl("pick", CelOverloadDecl.Global("pick_dyn_int", [CelType.Dyn, CelType.Int], CelType.Dyn)))
        .AddFunctionImplementation("pick_dyn_int", Pick)
        .Build()
        .GetEnvironmentOrThrow();

    // The patterns that the bindings draw from: three times the capacity of the cache, each distinct, in four kinds in turn — two valid,
    // one refused (an unclosed group, a nested repetition, a count over the limit) and one over the size limit of a program.
    private static readonly string[] MatchPatterns = BuildMatchPatterns();

    private static readonly CelValue Subjects = CelValue.FromList(CelList.Create(
        [CelValue.FromString(""), CelValue.FromString("ab"), CelValue.FromString("abc"), CelValue.FromString("İı"), CelValue.FromString("é😀"), CelValue.FromString("B")]));

    [GeneratedRegex(@"(?<![\w.'])[ogj](?![\w'])")]
    private static partial Regex ApplicationVariable();

    private enum Scenario
    {
        Program,
        Compile,
        OneStep,
        Cache,
    }

    [Fact]
    [Trait("Requirement", "R-4")]
    [Trait("Requirement", "+R-7")]
    public void ParallelEqualsSequential()
    {
        for (int i = 0; i < Cases; i++)
        {
            if (Violation(new Workload(i)) is { } reason)
            {
                Assert.Fail(string.Create(CultureInfo.InvariantCulture, $"{nameof(ParallelEqualsSequential)}: seed {Seed}, case {i}: {reason}"));
            }
        }
    }

    private static string[] BuildMatchPatterns()
    {
        string[] refused = ["(", "a**", "a{1001}"];
        var overSize = new StringBuilder();
        for (int repeat = 0; repeat < 101; repeat++)
        {
            overSize.Append("a{1000}");
        }

        string[] patterns = new string[PatternCache.DefaultMaxPatterns * 3];
        for (int i = 0; i < patterns.Length; i++)
        {
            patterns[i] = (i % 4) switch
            {
                0 => string.Create(CultureInfo.InvariantCulture, $"a|b{{{i % 6}}}c{i}"),
                1 => string.Create(CultureInfo.InvariantCulture, $"(?i)^\\pL*{i}$"),
                2 => refused[i / 4 % refused.Length] + i.ToString(CultureInfo.InvariantCulture),
                _ => overSize + i.ToString(CultureInfo.InvariantCulture),
            };
        }

        return patterns;
    }

    private static CelValue Pick(ReadOnlySpan<CelValue> arguments) =>
        arguments[1].TryGetInt64(out long index) && index % 2 == 0 ? arguments[0] : arguments[1];

    // Runs the items on 64 threads and compares each result with its reference; the first difference, with its thread and item.
    private static string? Violation(Workload workload)
    {
        if (!workload.ReadsApplicationData())
        {
            return "no text reads the objects or the JsonElement of the generator";
        }

        Item[] items = workload.Items();
        var references = new Result[items.Length];
        for (int index = 0; index < items.Length; index++)
        {
            references[index] = workload.Reference(items[index]);
        }

        var cache = new CelCompilationCache(Environment, CacheEntries, CacheEntries * 4096);
        return ThreadRun.OnThreads(Threads, (thread, barrier) => Run(workload, cache, items, references, thread, barrier));
    }

    private static string? Run(Workload workload, CelCompilationCache cache, Item[] items, Result[] references, int thread, Barrier barrier)
    {
        int[] order = ThreadRun.Shuffled(items.Length, new PCG((uint)thread, Seed));
        barrier.SignalAndWait();
        foreach (int index in order)
        {
            Item item = items[index];
            try
            {
                Result actual = workload.Parallel(item, cache);
                if (!actual.Matches(references[index]))
                {
                    return $"thread {thread}, {workload.Shown(item)}: {actual}, sequential {references[index]}";
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                return $"thread {thread}, {workload.Shown(item)}: {exception.GetType()}: {exception.Message}";
            }
        }

        return null;
    }

    private static Result Outcome(CelEvalResult<CelValue> result) =>
        result.IsSuccess ? new Result("value", result.Value) : new Result(Errors(result.Errors), null);

    // A compile result by its result type or by its errors.
    private static string Outcome(CelCompileResult<CelValue> result) =>
        result.IsSuccess ? "program of " + result.Program.ResultType : Errors(result.Errors);

    private static string Errors(IEnumerable<CelError> errors) => ThreadRun.Errors(errors);

    // The outcome of an item: errors by code, stage, location and message, a value by the structural comparison of CheckedEqualsUncheckedTests.
    private readonly record struct Result(string Text, CelValue? Value)
    {
        internal bool Matches(Result other) =>
            string.Equals(Text, other.Text, StringComparison.Ordinal)
            && (Value is { } value ? other.Value is { } otherValue && StructuralEquality.Equal(value, otherValue) : other.Value is null);

        public override string ToString()
        {
            if (Value is not { } value)
            {
                return Text;
            }

            var text = new System.Text.StringBuilder(Text).Append(' ');
            TypedExpressions.Print(text, value);
            return text.ToString();
        }
    }

    private readonly record struct Item(Scenario Scenario, int Text, int Binding)
    {
        // Half of the evaluations have a cost limit.
        internal CelEvalOptions? Options => (Text + Binding) % 2 == 1 ? new CelEvalOptions { CostLimit = CostLimit } : null;
    }

    // The texts, the bindings and the programs of one case.
    private sealed class Workload
    {
        private readonly string[] _texts = new string[Texts];
        private readonly CelBindings[][] _bindings = new CelBindings[Texts][];
        private readonly string[][] _bindingTexts = new string[Texts][];
        private readonly CelProgram<CelValue>?[] _programs = new CelProgram<CelValue>?[Texts];

        // Case i: text k comes from the stream k of the seed of the case; a third calls half, a third pick.
        internal Workload(int i)
        {
            ulong seed = Seed + (ulong)i;
            for (int k = 0; k < Texts; k++)
            {
                var random = new PCG((uint)k, seed);
                string integer = Generated(random, CelType.Int);
                _texts[k] = k % MatchesTextsEvery == MatchesTextsEvery - 1 ? MatchesText(k / MatchesTextsEvery) : (k % 3) switch
                {
                    0 => $"half({integer})",
                    1 => $"pick({Generated(random, TypedExpressions.Targets[random.Next((uint)TypedExpressions.Targets.Length)])}, {integer})",
                    _ => Generated(random, TypedExpressions.Targets[random.Next((uint)TypedExpressions.Targets.Length)]),
                };
                _bindings[k] = new CelBindings[BindingsPerText];
                _bindingTexts[k] = new string[BindingsPerText];
                for (int j = 0; j < BindingsPerText; j++)
                {
                    (_bindings[k][j], _bindingTexts[k][j]) = TypedExpressions.Bind(random);
                    string pattern = MatchPatterns[random.Next((uint)MatchPatterns.Length)];
                    _bindings[k][j].Add("p", CelValue.FromString(pattern)).Add("ss", Subjects);
                    _bindingTexts[k][j] += " with p " + (pattern.Length > 40 ? pattern[..40] + "..." : pattern);
                }

                // A text that does not compile has no program; its Compile scenario still runs.
                CelCompileResult<CelValue> compiled = Environment.Compile<CelValue>(_texts[k]);
                _programs[k] = compiled.IsSuccess ? compiled.Program : null;
            }
        }

        // Whether some text reads o, g or j, the variables of application data of the generator.
        internal bool ReadsApplicationData()
        {
            foreach (string text in _texts)
            {
                if (ApplicationVariable().IsMatch(text))
                {
                    return true;
                }
            }

            return false;
        }

        internal Item[] Items()
        {
            var items = new List<Item>();
            for (int k = 0; k < Texts; k++)
            {
                if (_programs[k] is not null)
                {
                    for (int j = 0; j < BindingsPerText; j++)
                    {
                        items.Add(new Item(Scenario.Program, k, j));
                    }
                }

                items.Add(new Item(Scenario.Compile, k, 0));
                items.Add(new Item(Scenario.OneStep, k, k % BindingsPerText));
                items.Add(new Item(Scenario.Cache, k, k * 7 % BindingsPerText));
            }

            return [.. items];
        }

        // The result of an item computed on one thread, before the parallel run.
        internal Result Reference(Item item) => item.Scenario switch
        {
            Scenario.Program => Outcome(_programs[item.Text]!.Evaluate(_bindings[item.Text][item.Binding], item.Options)),
            Scenario.Compile => Compiled(Environment.Compile<CelValue>(_texts[item.Text]), item),
            _ => Outcome(Environment.Evaluate<CelValue>(_texts[item.Text], _bindings[item.Text][item.Binding], null, item.Options)),
        };

        internal Result Parallel(Item item, CelCompilationCache cache) => item.Scenario switch
        {
            Scenario.Cache => Outcome(cache.Evaluate<CelValue>(_texts[item.Text], _bindings[item.Text][item.Binding], null, item.Options)),
            Scenario.OneStep => Outcome(Environment.Evaluate<CelValue>(_texts[item.Text], _bindings[item.Text][item.Binding], null, item.Options)),
            _ => Reference(item),
        };

        internal string Shown(Item item) =>
            $"{item.Scenario} of {_texts[item.Text]} on {_bindingTexts[item.Text][item.Binding]}";

        // A compile result and the evaluation of its program on the bindings of the item.
        private Result Compiled(CelCompileResult<CelValue> result, Item item)
        {
            if (!result.IsSuccess)
            {
                return new Result(Outcome(result), null);
            }

            Result evaluated = Outcome(result.Program.Evaluate(_bindings[item.Text][item.Binding], item.Options));
            return evaluated with { Text = Outcome(result) + "; " + evaluated.Text };
        }

        // The five forms of a call of matches, in turn: a literal pattern, the receiver style and the global style with the pattern of the
        // binding, a macro over a list of strings, and a literal that is refused, which is a compile error in every scenario.
        private static string MatchesText(int form) => (form % 5) switch
        {
            0 => "matches(s, 'a.c|é.*')",
            1 => "s.matches(p)",
            2 => "matches(s, p)",
            3 => "ss.filter(e, matches(e, p))",
            _ => "matches(s, '(')",
        };

        private static string Generated(PCG random, CelType target) =>
            TypedExpressions.Of(target).Generate(random, null!, out _).Text;
    }
}
