using System.Collections.Generic;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Library;

// A meter that records each charge in the order it was made, and refuses one that does not fit the limit as the meter of a frame does:
// with CostLimitException, before the operation. Without a limit every charge fits.
internal sealed class RecordingMeter(ulong limit = ulong.MaxValue) : IEvaluationMeter
{
    private readonly List<ulong> charges = [];
    private ulong remaining = limit;

    // The charges that were accepted, one entry for each call of Charge.
    internal IReadOnlyList<ulong> Charges => charges;

    // The sum of the accepted charges.
    internal ulong Total { get; private set; }

    public void Charge(ulong units)
    {
        if (units > remaining)
        {
            throw new CostLimitException();
        }

        remaining -= units;
        Total += units;
        charges.Add(units);
    }

    public void Event()
    {
    }
}
