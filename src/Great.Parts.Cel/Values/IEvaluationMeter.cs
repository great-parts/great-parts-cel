namespace Great.Parts.Cel.Values;

/// <summary>
/// The cost and event counters of one evaluation, as library implementations and value rules reach them. An
/// evaluation without a cost limit passes no meter to the rules that charge, so they compute nothing.
/// </summary>
internal interface IEvaluationMeter
{
    /// <summary>
    /// Charges units before the operation they pay for.
    /// </summary>
    /// <param name="units">The units.</param>
    void Charge(ulong units);

    /// <summary>
    /// Counts an event before it begins: a step of a comprehension or a call of application code.
    /// </summary>
    void Event();
}
