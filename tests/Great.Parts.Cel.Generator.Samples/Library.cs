using System;
using System.Collections.Generic;
using Great.Parts.Cel.Values;

namespace Library;

// The resource of the examples of the filtering package: a book, with a field of each kind that a filter reads. The names of the
// fields in CEL are those of the properties in snake case (LibraryContext).

public enum State
{
    Unspecified,
    Draft,
    Published,
}

public sealed class Address
{
    public string City { get; set; } = string.Empty;
}

public sealed class Author
{
    public string Name { get; set; } = string.Empty;

    public int Born { get; set; }

    public Address? Address { get; set; }
}

public sealed class Edition
{
    public int Year { get; set; }

    public string Isbn { get; set; } = string.Empty;

    public IReadOnlyList<string> Formats { get; set; } = [];
}

public sealed class Book
{
    public string Title { get; set; } = string.Empty;

    public string? Subtitle { get; set; }

    public int Pages { get; set; }

    public uint Copies { get; set; }

    public double Rating { get; set; }

    public bool Available { get; set; }

    public State State { get; set; }

    public DateTimeOffset Published { get; set; }

    public TimeSpan ReadTime { get; set; }

    public Author? Author { get; set; }

    public IReadOnlyList<string> Tags { get; set; } = [];

    public IReadOnlyList<Edition> Editions { get; set; } = [];

    public IReadOnlyDictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();

    public object? Meta { get; set; }
}

[CelObject(typeof(Book), Name = "Library.Book")]
[CelObject(typeof(Author), Name = "Library.Author")]
[CelObject(typeof(Address), Name = "Library.Address")]
[CelObject(typeof(Edition), Name = "Library.Edition")]
[CelTypeContextOptions(NamingPolicy = CelNamingPolicy.SnakeCaseLower)]
public sealed partial class LibraryContext : CelTypeContext
{
}
