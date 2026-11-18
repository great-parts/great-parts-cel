using Google.Protobuf.WellKnownTypes;

namespace SpeedBench.Adapters;

/// <summary>
/// Translation of the whole order graph into the protobuf messages of Proto/order.proto. Cel.NET and TELUS Cel
/// do not read the .NET classes of Model.cs, so their adapters call this on every evaluation, and the cost is
/// part of "evaluation" and the full cycle. Direct code, every field, independent of the expression.
/// </summary>
public static class ProtoData
{
    public static Proto.Order From(Order order)
    {
        var message = new Proto.Order
        {
            Id = order.Id,
            Status = order.Status,
            Total = order.Total,
            Currency = order.Currency,
            Customer = new Proto.Customer { Name = order.Customer.Name, Email = order.Customer.Email },
            Created = Timestamp.FromDateTimeOffset(order.Created),
        };
        if (order.Discount is { } discount)
        {
            message.Discount = discount;
        }

        foreach (var line in order.Lines)
        {
            message.Lines.Add(new Proto.Line { Sku = line.Sku, Qty = line.Qty, Price = line.Price });
        }

        return message;
    }
}
