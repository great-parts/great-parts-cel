using System.Collections.Generic;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Values;

// A meter without a limit that keeps every charge in order.
internal sealed class RecordingMeter : IEvaluationMeter
{
    internal List<ulong> Charges { get; } = [];

    internal ulong Total
    {
        get
        {
            ulong total = 0;
            foreach (ulong units in Charges)
            {
                total += units;
            }

            return total;
        }
    }

    internal int Events { get; private set; }

    public void Charge(ulong units) => Charges.Add(units);

    public void Event() => Events++;
}
