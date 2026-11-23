using System;
using System.Collections.Generic;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Generator.Samples.Schedules;

// DateOnly and TimeOnly as members, elements, values and sets: the types date and time_of_day of the calendar set. A member needs the set
// in the environment; a Nullable<> member that has no value is UnexpectedNull at the read. The member names are the camel-case names
// of the default policy.

public sealed class Shipment
{
    public DateOnly ShipDate { get; set; }

    public TimeOnly Cutoff { get; set; }

    public DateOnly? Delivered { get; set; }

    public TimeOnly? Window { get; set; }

    public IReadOnlyList<DateOnly> Holidays { get; set; } = [];

    public Dictionary<string, TimeOnly> Slots { get; set; } = [];

    public HashSet<DateOnly> Closed { get; set; } = [];
}

// A type of the input whose member has the name of the type date: with the calendar set it is DuplicateType.
public sealed class Entry
{
    public string Date { get; set; } = "";
}

[CelObject(typeof(Shipment), Name = "samples.Shipment")]
[CelObject(typeof(Entry), Name = "samples.Entry")]
public sealed partial class SchedulesContext : CelTypeContext
{
}
