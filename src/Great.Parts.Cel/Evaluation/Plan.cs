using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A planned expression: the root node, the number of slots, the size of the argument stack, the line
// table of the text, the type index and the time zone database of the environment. It never changes, so it can be evaluated from any number of threads at once.
internal sealed class Plan
{
    internal Plan(PlanNode root, int variableCount, int localCount, int stackSize, LineTable lines, TypeIndex types, CelTimeZoneDatabase? timeZones = null)
    {
        Types = types;
        TimeZones = timeZones;
        Root = root;
        VariableCount = variableCount;
        LocalCount = localCount;
        StackSize = stackSize;
        Lines = lines;
    }

    internal PlanNode Root { get; }

    // One slot per distinct variable of the expression.
    internal int VariableCount { get; }

    // Two slots for each comprehension, its iteration variable and its accumulator.
    internal int LocalCount { get; }

    // The most argument values on the stack at one time.
    internal int StackSize { get; }

    internal LineTable Lines { get; }

    // The type index of the environment the plan was planned with: the descriptions of object values.
    internal TypeIndex Types { get; }

    // The time zone database of the environment the plan was planned with; null when it has none.
    internal CelTimeZoneDatabase? TimeZones { get; }

    // Evaluates the plan on the values of the bindings by name: the value of the root, or its error value.
    // Without a cost limit nothing is counted; with a token that cannot be canceled no event is counted.
    internal CelValue Evaluate(
        IReadOnlyDictionary<string, CelValue> bindings,
        ulong? costLimit,
        CancellationToken cancellationToken) =>
        Run(CreateFrame(bindings, costLimit, cancellationToken));

    // Allocates the frame of one evaluation with its slots, argument stack and meter.
    internal Frame CreateFrame(IReadOnlyDictionary<string, CelValue> bindings, ulong? costLimit, CancellationToken cancellationToken) =>
        new(this, bindings, costLimit, cancellationToken);

    // Evaluates the root in the frame inside the one handler of the evaluation: an exceeded cost limit is
    // CostLimitExceeded at the recorded node, and any other exception but OutOfMemoryException and the
    // OperationCanceledException of the canceled token is InternalError there, with the exception.
    internal CelValue Run(Frame frame)
    {
        try
        {
            return Root.Evaluate(frame);
        }
        catch (CostLimitException)
        {
            return EvaluationErrors.CostLimitExceeded(frame.Limit, frame.Lines, frame.Offset);
        }
        catch (Exception exception) when (IsInternal(exception, frame.Token))
        {
            return EvaluationErrors.InternalError(exception, frame.Lines, frame.Offset);
        }
    }

    // Whether an exception that reached the root is an error of the library itself.
    internal static bool IsInternal(Exception exception, CancellationToken token) => exception switch
    {
        OutOfMemoryException => false,
        OperationCanceledException canceled => !(token.IsCancellationRequested && canceled.CancellationToken == token),
        _ => true,
    };
}
