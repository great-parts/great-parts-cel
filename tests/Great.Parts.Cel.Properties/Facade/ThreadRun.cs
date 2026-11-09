using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using CsCheck;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Properties.Facade;

// The parts that the concurrency tests share: threads that start together and hand back the first failure, the order in which a
// thread runs the items, and the text of errors that a result is compared by.
internal static class ThreadRun
{
    // Runs the body on the given number of threads, started together, and returns the first failure in the order of the threads,
    // or null when every thread gave null. The body signals the barrier when it is ready to start.
    internal static string? OnThreads(int threads, Func<int, Barrier, string?> body)
    {
        string?[] failures = new string?[threads];
        using var barrier = new Barrier(threads);
        var running = new Thread[threads];
        for (int thread = 0; thread < threads; thread++)
        {
            int number = thread;
            running[thread] = new Thread(() => failures[number] = body(number, barrier));
            running[thread].Start();
        }

        foreach (Thread thread in running)
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

    // The numbers 0 to count - 1 in an order that the random source gives.
    internal static int[] Shuffled(int count, PCG random)
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

    // The errors of a result by code, stage, location and message.
    internal static string Errors(IEnumerable<CelError> errors)
    {
        var text = new StringBuilder("errors");
        foreach (CelError error in errors)
        {
            text.Append(CultureInfo.InvariantCulture, $" [{error.Code} {error.Stage} {error.Location.Offset}:{error.Location.Line}:{error.Location.Column} {error.Message}]");
        }

        return text.ToString();
    }
}
