using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Generator.Samples.Orders;
using Great.Parts.Cel.Tests.Examples.Generated;
using Xunit;

namespace Great.Parts.Cel.Tests.Allocation;

// A call of the simple entry in the steady state — the same text and the same shape of data as an earlier call — allocates nothing
// beyond the result when the properties are scalars, a string or a class of the application (read by reference). The data of the call is
// made before the measurement, since an anonymous object is an object of the heap. The bytes are those that
// GC.GetAllocatedBytesForCurrentThread counts around one call, the smallest of five calls after three, so that the few kilobytes that the
// runtime allocates on the test thread while it moves methods between tiers are not counted: a call allocates its bytes every time, the
// runtime only in some. A list or a dictionary in a property is a lazy view that a call makes; a list of classes makes a view, and a
// pair of the description and the instance only for an element read that is of a class derived from the declared one (an element of a
// sealed class is bare); a structure is boxed. Their bytes are written to the output of the test and have no threshold.
[Trait("Requirement", "U-7")]
[Trait("Requirement", "P-4")]
public sealed class SimpleEntryAllocationTests
{
    private const int Warm = 3;
    private const int Counted = 5;

    // A string of n code units: the header and the length, 2 bytes per code unit and the terminator, to a multiple of 8.
    private static long StringBytes(int length) => (22 + (2 * length) + 7) & ~7;

    private static long Smallest(Action call)
    {
        for (int i = 0; i < Warm; i++)
        {
            call();
        }

        long smallest = long.MaxValue;
        for (int i = 0; i < Counted; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            call();
            smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return smallest;
    }

    private static void Record(string form, long bytes) =>
        TestContext.Current.TestOutputHelper?.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{form}: {bytes} bytes"));

    [Fact]
    public void Scalars_AllocateNothing()
    {
        var data = new { a = 40L, b = 2L, c = 3.5, ok = true };

        Assert.Equal(42L, CelExpression.Evaluate<long>("a + b", data, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(7.0, CelExpression.Evaluate<double>("c * 2.0", data, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(CelExpression.Evaluate<bool>("ok && a > b", data, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(0, Smallest(() => CelExpression.Evaluate<long>("a + b", data, cancellationToken: CancellationToken.None)));
        Assert.Equal(0, Smallest(() => CelExpression.Evaluate<double>("c * 2.0", data, cancellationToken: CancellationToken.None)));
        Assert.Equal(0, Smallest(() => CelExpression.Evaluate<bool>("ok && a > b", data, cancellationToken: CancellationToken.None)));
    }

    [Fact]
    public void StringProperty_AllocatesNothing()
    {
        var data = new { name = "Ann", n = 3L };

        Assert.True(CelExpression.Evaluate<bool>("name == 'Ann' && n > 1", data, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(0, Smallest(() => CelExpression.Evaluate<bool>("name == 'Ann' && n > 1", data, cancellationToken: CancellationToken.None)));
    }

    // The three strings of the concatenations, one for each plus: 14, 16 and 24 code units, the last is the result.
    [Fact]
    public void StringResult_AllocatesTheStringsOfTheConcatenations()
    {
        var data = new { id = "ORD-0001", status = "approved" };

        Assert.Equal("Order ORD-0001: approved", CelExpression.Evaluate<string>("'Order ' + id + ': ' + status", data, cancellationToken: TestContext.Current.CancellationToken));

        long bytes = Smallest(() => CelExpression.Evaluate<string>("'Order ' + id + ': ' + status", data, cancellationToken: CancellationToken.None));
        Record("string result", bytes);
        Assert.True(bytes <= StringBytes(14) + StringBytes(16) + StringBytes(24), bytes.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ClassOfTheApplication_IsReadByReferenceAndAllocatesNothing()
    {
        var data = new { customer = new Customer { Email = "ann@example.com" } };

        Assert.True(CelExpression.Evaluate<bool>("customer.email.endsWith('.com')", data, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(0, Smallest(() => CelExpression.Evaluate<bool>("customer.email.endsWith('.com')", data, cancellationToken: CancellationToken.None)));
    }

    // The scenarios of the performance set that need only scalars, in the form of the simple entry: a conjunction of comparisons, a
    // literal pattern of matches, a sum of a moment and a duration compared with a moment.
    [Fact]
    public void ScenariosOfScalars_AllocateNothing()
    {
        var order = new { total = 1500.0, status = "approved" };
        var email = new { email = "alice@example.com" };
        var moments = new { created = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero), now = new DateTimeOffset(2026, 11, 1, 13, 0, 0, TimeSpan.Zero) };

        Assert.True(CelExpression.Evaluate<bool>("total > 1000.0 && status == 'approved'", order, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(CelExpression.Evaluate<bool>("email.matches('^[^@]+@[^@]+[.][a-z]+$')", email, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(CelExpression.Evaluate<bool>("created + duration('72h') > now", moments, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(0, Smallest(() => CelExpression.Evaluate<bool>("total > 1000.0 && status == 'approved'", order, cancellationToken: CancellationToken.None)));
        Assert.Equal(0, Smallest(() => CelExpression.Evaluate<bool>("email.matches('^[^@]+@[^@]+[.][a-z]+$')", email, cancellationToken: CancellationToken.None)));
        Assert.Equal(0, Smallest(() => CelExpression.Evaluate<bool>("created + duration('72h') > now", moments, cancellationToken: CancellationToken.None)));
    }

    // A list and a dictionary of scalars are lazy views that a call makes: the bytes are recorded.
    [Fact]
    public void ListAndDictionaryOfScalars_RecordTheirAllocation()
    {
        var list = new { items = new List<long> { 1, 2, 3 } };
        var dictionary = new { prices = new Dictionary<string, double> { ["a"] = 1.5, ["b"] = 2.5 } };

        Assert.Equal(4L, CelExpression.Evaluate<long>("items[0] + items[2]", list, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(4.0, CelExpression.Evaluate<double>("prices.a + prices.b", dictionary, cancellationToken: TestContext.Current.CancellationToken));

        Record("list of long", Smallest(() => CelExpression.Evaluate<long>("items[0] + items[2]", list, cancellationToken: CancellationToken.None)));
        Record("dictionary of double", Smallest(() => CelExpression.Evaluate<double>("prices.a + prices.b", dictionary, cancellationToken: CancellationToken.None)));
    }

    // A list of a sealed class and a structure of the application: the view of the list, whose elements are read bare, and the boxing of
    // the structure; a list of a class that is not sealed, with one element of a derived class, which is paired. The bytes are recorded
    // without a threshold.
    [Fact]
    public void ListOfClassesAndStructure_RecordTheirAllocation()
    {
        var lines = new { lines = new List<OrderLine> { new() { Sku = "a", Qty = 1, Price = 2.0 }, new() { Sku = "b", Qty = 2, Price = 3.0 } } };
        var point = new { point = new CallPoint { X = 1, Y = 2 } };
        var contacts = new { contacts = new List<Contact> { new VipContact { Name = "Vip", Level = 3 }, new() { Name = "Plain" } } };

        Assert.True(CelExpression.Evaluate<bool>("lines.all(l, l.qty > 0)", lines, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(3L, CelExpression.Evaluate<long>("point.x + point.y", point, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(CelExpression.Evaluate<bool>("contacts.all(c, c.name != '')", contacts, cancellationToken: TestContext.Current.CancellationToken));

        Record("list of classes", Smallest(() => CelExpression.Evaluate<bool>("lines.all(l, l.qty > 0)", lines, cancellationToken: CancellationToken.None)));
        Record("structure", Smallest(() => CelExpression.Evaluate<long>("point.x + point.y", point, cancellationToken: CancellationToken.None)));
        Record("list of a class with a derived element", Smallest(() => CelExpression.Evaluate<bool>("contacts.all(c, c.name != '')", contacts, cancellationToken: CancellationToken.None)));
    }
}
