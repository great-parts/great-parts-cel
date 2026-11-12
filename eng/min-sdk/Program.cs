using System;
using System.Collections.Generic;
using Great.Parts.Cel.Values;

// Default exists only in the file that the source generator of the package creates: a compiler that skipped the generator (CS9057)
// or a generator that created nothing fails the build (docs/design/eng-test.md, "min-sdk"). The project has no nullable context.
Console.WriteLine(SampleContext.Default.SourceName);

[CelObject(typeof(Customer))]
[CelObject(typeof(Order))]
internal sealed partial class SampleContext : CelTypeContext;

internal sealed class Customer
{
    public string Name { get; set; } = string.Empty;

    public long Age { get; set; }
}

internal sealed class Order
{
    public long Id { get; set; }

    public Customer Customer { get; set; } = new Customer();

    public List<string> Tags { get; set; } = new List<string>();
}
