using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using CsCheck;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Properties.Evaluation;
using Great.Parts.Cel.Properties.Generators;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Facade;

// One environment and one program called from many threads at once give the results of sequential calls
// (docs/design/tests.md, "Properties of evaluation"). 64 threads, started together, each run every item of four scenarios in
// an order of their own: the evaluation of one program per text on its bindings, Compile of the texts, the one-step call, and
// GetOrCompile with Evaluate of one compilation cache whose capacities hold 16 entries.
public sealed class ConcurrencyTests
{
    private const ulong Seed = 20261110;
    private const int Cases = 2;
    private const int Threads = 64;
    private const int Texts = 64;
    private const int BindingsPerText = 16;
    private const int CacheEntries = 16;
    private const ulong CostLimit = 20;

    // The environment of the generator with two application functions: a typed delegate and a CelFunctionImplementation.
    private static readonly CelEnvironment Environment = TypedExpressions.Declare(new CelEnvironmentBuilder(CelStandardLibrary.V1))
        .AddFunction("half", (long value) => value / 2)
        .AddFunction(new CelFunctionDecl("pick", CelOverloadDecl.Global("pick_dyn_int", [CelType.Dyn, CelType.Int], CelType.Dyn)))
        .AddFunctionImplementation("pick_dyn_int", Pick)
        .Build()
        .GetEnvironmentOrThrow();

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

    private static CelValue Pick(ReadOnlySpan<CelValue> arguments) =>
        arguments[1].TryGetInt64(out long index) && index % 2 == 0 ? arguments[0] : arguments[1];

    // Runs the items on 64 threads and compares each result with its reference; the first difference, with its thread and item.
    private static string? Violation(Workload workload)
    {
        Item[] items = workload.Items();
        var references = new Result[items.Length];
        for (int index = 0; index < items.Length; index++)
        {
            references[index] = workload.Reference(items[index]);
        }

        var cache = new CelCompilationCache(Environment, CacheEntries, CacheEntries * 4096);
        string?[] failures = new string?[Threads];
        using var barrier = new Barrier(Threads);
        var threads = new Thread[Threads];
        for (int thread = 0; thread < Threads; thread++)
        {
            int number = thread;
            threads[thread] = new Thread(() => failures[number] = Run(workload, cache, items, references, number, barrier));
            threads[thread].Start();
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        foreach (string? failure in failures)
        {
            if (failure is not null)
            {
                return failure;
            }
        }

        return null;
    }

    private static string? Run(Workload workload, CelCompilationCache cache, Item[] items, Result[] references, int thread, Barrier barrier)
    {
        int[] order = Shuffled(items.Length, new PCG((uint)thread, Seed));
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

    private static int[] Shuffled(int count, PCG random)
    {
        int[] order = new int[count];
        for (int index = 0; index < count; index++)
        {
            order[index] = index;
        }

        for (int index = count - 1; index > 0; index--)
        {
            int other = (int)random.Next((uint)index + 1);
            (order[index], order[other]) = (order[other], order[index]);
        }

        return order;
    }

    private static Result Outcome(CelEvalResult<CelValue> result) =>
        result.IsSuccess ? new Result("value", result.Value) : new Result(Errors(result.Errors), null);

    // A compile result by its result type or by its errors.
    private static string Outcome(CelCompileResult<CelValue> result) =>
        result.IsSuccess ? "program of " + result.Program.ResultType : Errors(result.Errors);

    private static string Errors(IEnumerable<CelError> errors)
    {
        var text = new System.Text.StringBuilder("errors");
        foreach (CelError error in errors)
        {
            text.Append(CultureInfo.InvariantCulture, $" [{error.Code} {error.Stage} {error.Location.Offset}:{error.Location.Line}:{error.Location.Column} {error.Message}]");
        }

        return text.ToString();
    }

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
                _texts[k] = (k % 3) switch
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
                }

                _programs[k] = Environment.Compile<CelValue>(_texts[k]).Program;
            }
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

        private static string Generated(PCG random, CelType target) =>
            TypedExpressions.Of(target).Generate(random, null!, out _).Text;
    }
}
