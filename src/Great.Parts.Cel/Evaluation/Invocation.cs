using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// The call of an overload whose arguments passed their checks, shared by the call nodes. A library
// implementation is called after its cost known from the arguments is charged. An overload of the
// application counts an event, charges 1 plus the value of its cost function and calls its implementation
// inside the exception boundary, then checks the result against the declared result type.
internal static class Invocation
{
    // Calls the overload at the node offset; charges says whether the call pays its cost, which the calls
    // that the expansion of a standard macro adds do not.
    internal static CelValue Call(Frame frame, OverloadBinding overload, ReadOnlySpan<CelValue> arguments, int offset, bool charges)
    {
        // A call that the expansion of a standard macro adds charges nothing, also inside its implementation.
        var site = new CallSite(overload.Function, offset, frame.Lines, charges ? frame.Meter : null, frame.Types, frame);
        if (overload.IsApplication)
        {
            return CallApplication(frame, overload, arguments, in site, offset);
        }

        if (charges && frame.Metered)
        {
            frame.Charge(overload.FixedCost);
            if (overload.ArgumentCost is { } argumentCost)
            {
                frame.Charge(argumentCost(arguments));
            }
        }

        // Only the special forms have no implementation, and the planner plans them as nodes of their own.
        return overload.Implementation!(arguments, in site);
    }

    private static CelValue CallApplication(Frame frame, OverloadBinding overload, ReadOnlySpan<CelValue> arguments, in CallSite site, int offset)
    {
        frame.Event();
        bool inCostFunction = false;
        CelValue result;
        try
        {
            if (frame.Metered)
            {
                ulong units = 0;
                if (overload.ArgumentCost is { } costFunction)
                {
                    inCostFunction = true;
                    units = costFunction(arguments);
                    inCostFunction = false;
                }

                frame.Charge(overload.FixedCost);
                frame.Charge(units);
            }

            result = overload.Implementation!(arguments, in site);
        }
        catch (Exception exception) when (exception is not CostLimitException && Plan.IsInternal(exception, frame.Token))
        {
            return EvaluationErrors.ApplicationException(exception, overload, inCostFunction, frame.Lines, offset);
        }

        if (result.Kind == CelValueKind.Error)
        {
            return result;
        }

        // A .NET collection that no source describes is TypeNotAccessible at the call, before the check of the result.
        if (result.Kind == CelValueKind.Object && ObjectRules.TryRejectCollection(result, null, frame.Types, out CelValue rejected))
        {
            return EvaluationErrors.Placed(rejected, frame.Lines, offset);
        }

        CelType resultType = overload.Declaration.ResultType;
        TypeFit fit = ValueTypeRules.Fits(result, resultType, frame.Meter, false, frame.Types);
        if (fit.IsFit)
        {
            return result;
        }

        // An error value that an element of the result is, is the value read: the call gives it its place.
        return fit.ElementError is { } elementError
            ? EvaluationErrors.Placed(CelValue.FromError(elementError), frame.Lines, offset)
            : EvaluationErrors.ImplementationResultMismatch(overload, result, frame.Types, frame.Lines, offset);
    }
}
