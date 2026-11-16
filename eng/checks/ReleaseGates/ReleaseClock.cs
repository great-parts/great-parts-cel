using System;
using System.Threading;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>The clock of the gates: the time and the wait between two tries. A test gives its own, so no test waits.</summary>
internal interface IReleaseClock
{
    /// <summary>The time now, UTC.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Waits.</summary>
    /// <param name="time">How long to wait.</param>
    void Delay(TimeSpan time);
}

/// <summary>The clock of the machine.</summary>
internal sealed class SystemReleaseClock : IReleaseClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc/>
    public void Delay(TimeSpan time) => Thread.Sleep(time);
}
