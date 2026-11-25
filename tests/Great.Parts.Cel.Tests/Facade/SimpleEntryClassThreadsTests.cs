using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Generator.Samples.Orders;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// 64 threads call the simple entry through the cache of the process with shapes that have classes in their properties — a class, a list
// and a dictionary of classes, a structure — mixed with 80 shapes of dictionaries, so that environments are evicted all the time, and
// every result is the one that a sequential call gives. The classes are shared by the threads: they are read by reference, at the same
// time, through descriptions that the created code made once.
[Trait("Requirement", "R-4")]
[Trait("Requirement", "U-7")]
public sealed class SimpleEntryClassThreadsTests
{
    private const int Threads = 64;
    private const int CallsPerThread = 120;

    [Fact]
    public void SixtyFourThreads_OnShapesWithClasses_GiveTheSequentialResults()
    {
        var customer = new Customer { Email = "ann@example.com" };
        OrderLine[] lines = [new() { Sku = "a", Qty = 2, Price = 5.0 }, new() { Sku = "b", Qty = 3, Price = 1.5 }];
        var byName = new Dictionary<string, Customer> { ["ann"] = customer };
        var point = new CallPoint { X = 4, Y = 5 };
        long[] failures = new long[Threads];

        Parallel.For(0, Threads, new ParallelOptions { MaxDegreeOfParallelism = Threads }, thread =>
        {
            for (int call = 0; call < CallsPerThread; call++)
            {
                bool ok = ((thread + call) % 6) switch
                {
                    0 => CelExpression.Evaluate<bool>(Copy("customer.email.endsWith('.com')", call), new { customer }, cancellationToken: CancellationToken.None),
                    1 => CelExpression.Evaluate<long>(Copy("lines.map(l, l.qty).size() * 10 + lines[1].qty", call), new { lines }, cancellationToken: CancellationToken.None) == 23L,
                    2 => CelExpression.Evaluate<bool>("byName['ann'].email == 'ann@example.com'", new { byName }, cancellationToken: CancellationToken.None),
                    3 => CelExpression.Evaluate<long>(Copy("point.x * point.y", call), new { point }, cancellationToken: CancellationToken.None) == 20L,
                    4 => CelExpression.Evaluate<long>("list.size() + extra", new { list = new List<OrderLine>(lines), extra = (long)thread }, cancellationToken: CancellationToken.None) == 2L + thread,
                    _ => Evict(thread, call),
                };
                if (!ok)
                {
                    failures[thread]++;
                }
            }
        });

        Assert.All(failures, count => Assert.Equal(0L, count));
    }

    // A call with a dictionary of a shape of its own: it takes a place of level 1 from the shapes that have classes.
    private static bool Evict(int thread, int call)
    {
        string name = "e" + ((thread + call) % 80).ToString(CultureInfo.InvariantCulture);
        var variables = new Dictionary<string, object?> { [name] = 3L };
        return CelExpression.Evaluate<long>(name + " + 1", variables, cancellationToken: CancellationToken.None) == 4L;
    }

    // The same text in a string of its own on every other call, so that the program is found by the instance of the text and by its value.
    private static string Copy(string text, int call) => call % 2 == 0 ? text : new string(text.AsSpan());
}
