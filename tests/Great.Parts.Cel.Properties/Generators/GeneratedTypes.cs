using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CsCheck;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Properties.Generators;

/// <summary>
/// The carrier of <c>gen.Item</c>, a type described at run time over a class of the project. It holds the values its read
/// delegates return, created with it, so a read allocates nothing in application code.
/// </summary>
internal sealed class Item
{
    internal long N { get; init; }

    internal string S { get; init; } = "";

    // The field w is absent, present with null, or present with a value.
    internal bool WPresent { get; init; }

    internal long? W { get; init; }

    // The view of an IReadOnlyList<long>.
    internal CelValue L { get; init; }

    // The field c is absent, null, or the pair of another item.
    internal bool CPresent { get; init; }

    internal CelValue C { get; init; }

    internal string Description { get; init; } = "";

    public override string ToString() => Description;
}

/// <summary>
/// The types of application data in the fixed environment of the generator (docs/design/tests.md, "Property tests and the
/// seed"): the type source <c>generated</c> with <c>gen.Item</c> over a carrier class and <c>gen.Bag</c> over a dictionary,
/// the carriers and pairs that bind them, and the JSON document whose elements bind the variable of type <c>dyn</c>.
/// </summary>
internal static class GeneratedTypes
{
    internal static readonly CelType ItemName = CelType.ObjectNamed("gen.Item");
    internal static readonly CelType BagName = CelType.ObjectNamed("gen.Bag");

    internal static readonly CelObjectType ItemType = new CelObjectTypeBuilder<Item>("gen.Item")
        .AddField("n", CelType.Int, item => CelValue.FromInt64(item.N))
        .AddField("s", CelType.String, item => CelValue.FromString(item.S))
        .AddField("w", CelType.WrapperOf(CelType.Int), item => item.W is { } number ? CelValue.FromInt64(number) : CelValue.Null, item => item.WPresent)
        .AddField("l", CelType.ListOf(CelType.Int), item => item.L)
        .AddField("c", ItemName, item => item.C, item => item.CPresent)
        .Build();

    internal static readonly CelObjectType BagType = CelObjectTypeBuilder.ForDictionary("gen.Bag")
        .AddField("n", CelType.Int)
        .AddField("s", CelType.String)
        .AddField("c", BagName)
        .Build();

    internal static readonly CelTypeProvider Provider = CelTypeProvider.Create("generated", ItemType, BagType);

    private const string Document =
        """{"a": 1, "b": "text", "c": [1, 2.5, "x", null, {"d": true}], "e": {"f": {"g": 7}, "h": null}, "n": 3000000000, "z": 0.5}""";

    // The document is parsed once and lives to the end of the process, so that every element of it stays valid.
    private static readonly JsonDocument Parsed = JsonDocument.Parse(Document);

    private static readonly JsonElement[] Elements =
    [
        Parsed.RootElement,
        Parsed.RootElement.GetProperty("c"),
        Parsed.RootElement.GetProperty("c")[4],
        Parsed.RootElement.GetProperty("e"),
        Parsed.RootElement.GetProperty("e").GetProperty("f"),
        Parsed.RootElement.GetProperty("a"),
        Parsed.RootElement.GetProperty("b"),
        Parsed.RootElement.GetProperty("e").GetProperty("h"),
    ];

    /// <summary>Binds the variable <c>o</c>: a bare carrier or the pair of the description and the carrier, alternately.</summary>
    /// <param name="random">The source of randomness.</param>
    /// <param name="bindings">The bindings to add to.</param>
    /// <returns>The text of the value.</returns>
    internal static string BindItem(PCG random, CelBindings bindings)
    {
        Item item = NewItem(random, 2);
        if (random.Next(2) == 0)
        {
            bindings.Add("o", item);
            return item.Description;
        }

        bindings.Add("o", CelValue.FromObject(ItemType, item));
        return "pair " + item.Description;
    }

    /// <summary>Binds the variable <c>g</c>: a dictionary, sometimes without a key or with a value of another type.</summary>
    /// <param name="random">The source of randomness.</param>
    /// <param name="bindings">The bindings to add to.</param>
    /// <returns>The text of the value.</returns>
    internal static string BindBag(PCG random, CelBindings bindings)
    {
        var text = new StringBuilder();
        bindings.Add<IReadOnlyDictionary<string, object?>>("g", NewBag(random, 2, text));
        return text.ToString();
    }

    /// <summary>Binds the variable <c>j</c>: a <see cref="JsonElement"/> of the document of the property.</summary>
    /// <param name="random">The source of randomness.</param>
    /// <param name="bindings">The bindings to add to.</param>
    /// <returns>The text of the value.</returns>
    internal static string BindJson(PCG random, CelBindings bindings)
    {
        JsonElement element = Elements[random.Next((uint)Elements.Length)];
        bindings.Add("j", element);
        return "json " + element.GetRawText();
    }

    private static Item NewItem(PCG random, int depth)
    {
        long number = (long)random.Next(100) - 20;
        string text = random.Next(4) == 0 ? "" : "s" + number.ToString(CultureInfo.InvariantCulture);
        bool wPresent = random.Next(4) != 0;
        long? w = wPresent && random.Next(3) != 0 ? number * 2 : null;
        long[] list = new long[random.Next(4)];
        for (int index = 0; index < list.Length; index++)
        {
            list[index] = index + number;
        }

        bool cPresent = depth > 0 && random.Next(3) != 0;
        Item? next = cPresent && random.Next(3) != 0 ? NewItem(random, depth - 1) : null;
        string shown = string.Create(
            CultureInfo.InvariantCulture,
            $"Item{{n: {number}, s: \"{text}\", w: {(wPresent ? (w?.ToString(CultureInfo.InvariantCulture) ?? "null") : "-")}, l: [{string.Join(", ", list)}], c: {(cPresent ? (next?.Description ?? "null") : "-")}}}");
        return new Item
        {
            N = number,
            S = text,
            WPresent = wPresent,
            W = w,
            L = CelValue.FromList(CelList.CreateView<long>(list, static element => CelValue.FromInt64(element))),
            CPresent = cPresent,
            C = next is null ? CelValue.Null : CelValue.FromObject(ItemType, next),
            Description = shown,
        };
    }

    private static Dictionary<string, object?> NewBag(PCG random, int depth, StringBuilder text)
    {
        var bag = new Dictionary<string, object?>(StringComparer.Ordinal);
        long number = (long)random.Next(100) - 20;
        text.Append("Bag{");
        switch (random.Next(6))
        {
            case 0:
                break;
            case 1:
                bag["n"] = "wrong";
                text.Append("n: \"wrong\", ");
                break;
            default:
                bag["n"] = number;
                text.Append(CultureInfo.InvariantCulture, $"n: {number}, ");
                break;
        }

        switch (random.Next(6))
        {
            case 0:
                break;
            case 1:
                bag["s"] = number;
                text.Append(CultureInfo.InvariantCulture, $"s: {number}, ");
                break;
            default:
                bag["s"] = "t" + number.ToString(CultureInfo.InvariantCulture);
                text.Append(CultureInfo.InvariantCulture, $"s: \"t{number}\", ");
                break;
        }

        if (depth > 0 && random.Next(3) != 0)
        {
            text.Append("c: ");
            bag["c"] = NewBag(random, depth - 1, text);
        }

        text.Append('}');
        return bag;
    }
}
