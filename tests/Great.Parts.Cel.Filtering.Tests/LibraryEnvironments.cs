using System;
using System.Collections.Immutable;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Library;
using Xunit;

namespace Great.Parts.Cel.Filtering.Tests;

// What the examples of filters share: the environment Env, whose variables are the fields of a book, and the compilers of
// filters over it, with and without options, and the books that the examples evaluate on.
internal static class LibraryEnvironments
{
    // The environment of the books: the fields of Book as variables, and the functions lower and isRecent.
    internal static CelEnvironment Env { get; } = CelEnvironment.Create(builder => builder
        .AddTypes(LibraryContext.Default)
        .UseInput<Book>()
        .AddFunction<string, string>("lower", Lower)
        .AddFunction<DateTimeOffset, bool>("isRecent", static published => published.Year >= 2000));

    // The compiler of the examples: the field state is an enumeration, a value without a field is searched in the title and the
    // name of the author.
    internal static CelFilterCompiler F { get; } = new(Env, new CelFilterOptions
    {
        EnumTypes = ImmutableDictionary<string, string>.Empty.Add("state", "Library.State"),
        SearchFields = ["title", "author.name"],
    });

    // The compiler without options.
    internal static CelFilterCompiler F0 { get; } = new(Env);

    // The book of the examples, with an author whose address is not set.
    internal static Book Dune => Create(withAuthor: true);

    // The same book without an author.
    internal static Book Anon => Create(withAuthor: false);

    // The tree of a filter equals the tree of the CEL text, without ids and positions.
    internal static void AssertCel(string filter, string cel, CelFilterCompiler? compiler = null)
    {
        CelParseResult translated = (compiler ?? F).Parse(filter);
        Assert.True(translated.IsSuccess, filter + ": " + string.Join("; ", translated.Errors));
        CelParseResult expected = Env.Parse(cel);
        Assert.True(expected.IsSuccess, cel + ": " + string.Join("; ", expected.Errors));
        string? difference = SyntaxTreeComparer.Difference(translated.ParsedExpr.Root, expected.ParsedExpr.Root);
        Assert.True(difference is null, filter + " as " + cel + ": " + difference);
    }

    // The first error of a filter: its code and its place.
    internal static (CelErrorCode Code, int Start, int Length) FirstError(ImmutableArray<CelError> errors)
    {
        Assert.False(errors.IsEmpty);
        CelError error = errors[0];
        return (error.Code, error.Span.Start.Offset, error.Span.Length);
    }

    // Whether a book matches a filter: the filter compiles, and the program is true for the book.
    internal static bool Matches(string filter, Book book, CelFilterCompiler? compiler = null)
    {
        CelCompileResult<bool> compiled = (compiler ?? F).Compile(filter);
        Assert.True(compiled.IsSuccess, filter + ": " + string.Join("; ", compiled.Errors));
        return compiled.GetProgramOrThrow().EvaluateInput(book, cancellationToken: CancellationToken.None).GetValueOrThrow();
    }

    private static Book Create(bool withAuthor) => new()
    {
        Title = "Dune",
        Pages = 412,
        Rating = 4.3,
        State = State.Published,
        Published = new DateTimeOffset(1965, 8, 1, 0, 0, 0, TimeSpan.Zero),
        ReadTime = TimeSpan.FromMinutes(1),
        Author = withAuthor ? new Author { Name = "Frank Herbert", Born = 1920 } : null,
        Tags = ["sf", "classic"],
        Editions =
        [
            new Edition { Year = 1965, Isbn = "0-8023-0", Formats = ["hardcover"] },
            new Edition { Year = 2005, Isbn = "0-441-17271-7", Formats = ["paperback", "epub"] },
        ],
        Labels = new System.Collections.Generic.Dictionary<string, string> { ["genre"] = "sf" },
    };

    private static string Lower(string text) => string.Create(text.Length, text, static (lower, source) => source.AsSpan().ToLowerInvariant(lower));
}
