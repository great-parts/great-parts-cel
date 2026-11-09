namespace Great.Parts.Cel.Values;

/// <summary>
/// One time zone read from TZif data: the transitions, the local time types they lead to, and the POSIX TZ rule of the
/// times after the last transition. It holds arrays and the rule and has no logic; <see cref="TimeZoneRules"/> reads them.
/// </summary>
internal sealed class ZoneData
{
    /// <summary>
    /// Creates the data of a zone.
    /// </summary>
    /// <param name="transitions">The transition times in Unix seconds, strictly increasing.</param>
    /// <param name="transitionTypes">The index of the local time type after each transition.</param>
    /// <param name="typeOffsets">The offset east of UTC in seconds of each local time type; at least one.</param>
    /// <param name="rule">The rule of the footer, or <see langword="null"/> when the footer is empty.</param>
    internal ZoneData(long[] transitions, byte[] transitionTypes, int[] typeOffsets, TimeZonePosixRule? rule)
    {
        Transitions = transitions;
        TransitionTypes = transitionTypes;
        TypeOffsets = typeOffsets;
        Rule = rule;
    }

    /// <summary>The transition times in Unix seconds, strictly increasing.</summary>
    internal long[] Transitions { get; }

    /// <summary>The index of the local time type after each transition.</summary>
    internal byte[] TransitionTypes { get; }

    /// <summary>The offset east of UTC in seconds of each local time type.</summary>
    internal int[] TypeOffsets { get; }

    /// <summary>The rule of the times after the last transition, or <see langword="null"/>.</summary>
    internal TimeZonePosixRule? Rule { get; }
}
