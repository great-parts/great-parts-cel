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
    // The prepared object and the cache of patterns are those of the plan node of the call.
    internal static CelValue Call(
        Frame frame,
        OverloadBinding overload,
        ReadOnlySpan<CelValue> arguments,
        int offset,
        bool charges,
        object? prepared = null,
        object? cache = null)
    {
        // A call that the expansion of a standard macro adds charges nothing, also inside its implementation.
        var site = new CallSite(overload.Function, offset, frame.Lines, charges ? frame.Meter : null, frame.Types, frame, frame.TimeZones, prepared, cache);
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

        // Only the special forms have no implementation, and the planner plans them as nodes of their own. A library overload that
        // gives a decimal the environment has no set for — an index, in, a function over a dyn value — is TypeNotAccessible at the call.
        CelValue result = overload.Implementation!(arguments, in site);
        return ObjectRules.MayBeDecimal(result.Kind) && ObjectRules.TryRejectDecimal(result, frame.Types, out CelValue refused)
            ? EvaluationErrors.Placed(refused, frame.Lines, offset)
            : result;
    }

    private static CelValue CallApplication(Frame frame, OverloadBinding overload, ReadOnlySpan<CelValue> arguments, in CallSite site, int offset)
    {
        if (!TryChargeApplication(frame, overload, arguments, offset, out CelValue failure))
        {
            return failure;
        }

        CelValue result;
        try
        {
            result = overload.Implementation!(arguments, in site);
        }
        catch (Exception exception) when (exception is not CostLimitException && Plan.IsInternal(exception, frame.Token))
        {
            return EvaluationErrors.ApplicationException(exception, overload, false, frame.Lines, offset);
        }

        return CheckResult(frame, overload, result, offset);
    }

    // Counts an event and charges a call of an overload of the application: 1 plus the value of its cost function. A call of an overload
    // that waits is charged so in each pass that makes it. False, with the error value, when the cost function threw.
    internal static bool TryChargeApplication(Frame frame, OverloadBinding overload, ReadOnlySpan<CelValue> arguments, int offset, out CelValue error)
    {
        frame.Event();
        bool inCostFunction = false;
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
        }
        catch (Exception exception) when (exception is not CostLimitException && Plan.IsInternal(exception, frame.Token))
        {
            error = EvaluationErrors.ApplicationException(exception, overload, inCostFunction, frame.Lines, offset);
            return false;
        }

        error = default;
        return true;
    }

    // The value of a call of an overload that waits, completed between two passes: the error of its exception, or its result checked as the
    // result of a call of an application function is.
    internal static CelValue AsyncResult(Frame frame, AsyncCall call) =>
        call.Exception is { } exception
            ? EvaluationErrors.ApplicationException(exception, call.Overload, false, frame.Lines, call.Offset)
            : CheckResult(frame, call.Overload, call.Result, call.Offset);

    // The result of a call of an application function against the declared result type of its overload.
    private static CelValue CheckResult(Frame frame, OverloadBinding overload, CelValue result, int offset)
    {
        if (result.Kind == CelValueKind.Error)
        {
            return result;
        }

        // A .NET collection that no source describes, and a decimal that the environment has no set for, are TypeNotAccessible at the
        // call, before the check of the result.
        if (ObjectRules.TryRejectValue(result, null, frame.Types, out CelValue rejected))
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
