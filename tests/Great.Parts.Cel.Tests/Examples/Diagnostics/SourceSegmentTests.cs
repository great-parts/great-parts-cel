using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Examples.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Diagnostics;

// The segments of an expression in its document (CelCompileOptions.SourceSegments): from the last segment at or before a place of the
// expression, the offset is the offset of the segment plus the distance; with no line break between them the line is that of the
// segment and the column the column of the segment plus the distance, otherwise the line of the segment plus the line breaks and the
// column in the expression. One segment at offset 0 is the start of the expression, SourceLocation. The rules of the value are checked
// when it is set; a segment beyond the end of the expression is not used; the cache compares the segments one by one; the places are
// the same in Parse, Compile, Validate, CompileParsed and the errors of evaluation.
[Trait("Requirement", "F-31")]
public sealed class SourceSegmentTests
{
    private static readonly CelEnvironment Env = TreeEnvironments.Env;

    private static CelSourceSegment Segment(int offset, string name, int documentOffset, int line, int column) =>
        new(offset, new CelSourceLocation(name, documentOffset, line, column));

    private static CelCompileOptions With(params CelSourceSegment[] segments) => new() { SourceSegments = [.. segments] };

    private static CelError UndeclaredNope(string expression, CelCompileOptions options)
    {
        CelCompileResult<CelValue> result = Env.Compile<CelValue>(expression, options);
        return Assert.Single(result.Errors, error => error.Code == CelErrorCode.UndeclaredReference);
    }

    [Fact]
    public void LiteralBlockWithItsIndentationRemoved_ReportsTheLineAndColumnOfTheDocument()
    {
        CelError error = UndeclaredNope(
            "\"a\" +\nnope",
            With(Segment(0, "rules.yaml", 304, 12, 5), Segment(6, "rules.yaml", 314, 13, 5)));

        Assert.Equal((2, 1), (error.Location.Line, error.Location.Column));
        Assert.Equal(new CelSourceLocation("rules.yaml", 314, 13, 5), error.SourceLocation);
    }

    [Fact]
    public void DocumentWithCrLfLines_ReportsTheOffsetOfTheSegment()
    {
        CelError error = UndeclaredNope(
            "\"a\" +\nnope",
            With(Segment(0, "rules.yaml", 304, 12, 5), Segment(6, "rules.yaml", 315, 13, 5)));

        Assert.Equal((2, 1), (error.Location.Line, error.Location.Column));
        Assert.Equal(new CelSourceLocation("rules.yaml", 315, 13, 5), error.SourceLocation);
    }

    [Fact]
    public void JsonStringWithEscapeSequences_ReportsTheExactColumnAfterEachEscape()
    {
        CelError error = UndeclaredNope(
            "x + \"q\" + nope",
            With(Segment(0, "j.json", 7, 1, 8), Segment(5, "j.json", 13, 1, 14), Segment(7, "j.json", 16, 1, 17)));

        Assert.Equal((1, 11), (error.Location.Line, error.Location.Column));
        Assert.Equal(new CelSourceLocation("j.json", 19, 1, 20), error.SourceLocation);
    }

    [Fact]
    public void OneSegmentAtOffsetZero_FollowsTheRuleOfTheStartOfTheExpression()
    {
        CelError multiline = UndeclaredNope("x +\n  nope", With(Segment(0, "d.yaml", 0, 1, 1)));
        CelError firstLine = UndeclaredNope("x + nope", With(Segment(0, "d.yaml", 40, 3, 7)));
        CelError byStart = UndeclaredNope("x + nope", new CelCompileOptions { SourceLocation = new CelSourceLocation("d.yaml", 40, 3, 7) });

        Assert.Equal((2, 3), (multiline.SourceLocation!.Value.Line, multiline.SourceLocation.Value.Column));
        Assert.Equal((1, 5), (firstLine.Location.Line, firstLine.Location.Column));
        Assert.Equal(new CelSourceLocation("d.yaml", 44, 3, 11), firstLine.SourceLocation);
        Assert.Equal(byStart.SourceLocation, firstLine.SourceLocation);
    }

    [Fact]
    public void SegmentBeyondTheEndOfTheExpression_IsNotAnErrorAndIsNotUsed()
    {
        CelError error = UndeclaredNope("x + nope", With(Segment(0, "d.yaml", 40, 3, 7), Segment(100, "d.yaml", 400, 9, 9)));

        Assert.Equal(new CelSourceLocation("d.yaml", 44, 3, 11), error.SourceLocation);
    }

    [Fact]
    public void FirstSegmentFromAnotherOffset_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => With(Segment(2, "d.yaml", 2, 1, 3)));
    }

    [Fact]
    public void OffsetsThatDoNotGrow_ThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => With(Segment(0, "d.yaml", 0, 1, 1), Segment(5, "d.yaml", 5, 1, 6), Segment(5, "d.yaml", 6, 1, 7)));
        Assert.Throws<ArgumentException>(() => With(Segment(0, "d.yaml", 0, 1, 1), Segment(5, "d.yaml", 5, 1, 6), Segment(3, "d.yaml", 6, 1, 7)));
    }

    [Fact]
    public void SegmentsOfTwoDocuments_ThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => With(Segment(0, "a.yaml", 0, 1, 1), Segment(4, "b.yaml", 4, 1, 5)));
    }

    [Fact]
    public void StartAndSegmentsTogether_ThrowArgumentException_InEitherOrder()
    {
        var start = new CelSourceLocation("d.yaml", 0, 1, 1);
        ImmutableArray<CelSourceSegment> segments = [Segment(0, "d.yaml", 0, 1, 1)];

        Assert.Throws<ArgumentException>(() => new CelCompileOptions { SourceLocation = start, SourceSegments = segments });
        Assert.Throws<ArgumentException>(() => new CelCompileOptions { SourceSegments = segments, SourceLocation = start });
        Assert.Equal(segments, new CelCompileOptions { SourceSegments = segments }.SourceSegments);
        Assert.Equal(start, new CelCompileOptions { SourceLocation = start }.SourceLocation);
    }

    [Fact]
    public void NoSegments_AreTheDefaultArray()
    {
        Assert.True(new CelCompileOptions().SourceSegments.IsDefaultOrEmpty);
        Assert.True(new CelCompileOptions { SourceSegments = [] }.SourceSegments.IsDefaultOrEmpty);
    }

    [Fact]
    public void Segment_NeedsANonNegativeOffsetAndANamedDocument()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CelSourceSegment(-1, new CelSourceLocation("d.yaml", 0, 1, 1)));
        Assert.Throws<ArgumentException>(() => new CelSourceSegment(0, default));
    }

    [Fact]
    public void Segment_IsComparedByItsOffsetAndItsPlace()
    {
        CelSourceSegment segment = Segment(3, "d.yaml", 10, 2, 4);

        Assert.Equal(Segment(3, "d.yaml", 10, 2, 4), segment);
        Assert.True(segment == Segment(3, "d.yaml", 10, 2, 4));
        Assert.True(segment != Segment(4, "d.yaml", 10, 2, 4));
        Assert.True(segment != Segment(3, "d.yaml", 11, 2, 4));
        Assert.Equal(Segment(3, "d.yaml", 10, 2, 4).GetHashCode(), segment.GetHashCode());
        Assert.False(segment.Equals((object)"3"));
        Assert.Equal(3, segment.ExpressionOffset);
        Assert.Equal(new CelSourceLocation("d.yaml", 10, 2, 4), segment.DocumentLocation);
    }

    [Fact]
    public void ErrorOfEvaluation_ReportsTheDocumentPlaceOfTheOperator()
    {
        CelEnvironment environment = new CelEnvironmentBuilder().AddVariable<long>("x").Build().GetEnvironmentOrThrow();
        CelCompileResult<long> compiled = environment.Compile<long>(
            "x +\nx / 0",
            With(Segment(0, "d.yaml", 100, 5, 5), Segment(4, "d.yaml", 109, 6, 5)));

        CelEvalResult<long> result = compiled.GetProgramOrThrow().Evaluate(new CelBindings().Add("x", 1L), cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.DivisionByZero, error.Code);
        Assert.Equal((2, 3), (error.Location.Line, error.Location.Column));
        Assert.Equal(new CelSourceLocation("d.yaml", 111, 6, 7), error.SourceLocation);
    }

    [Fact]
    public void CompilationCache_KeepsOneEntryForEqualSegmentsAndTwoForDifferentOnes()
    {
        var cache = new CelCompilationCache(Env);
        string text = "x + nope";

        _ = cache.GetOrCompile<CelValue>(text, With(Segment(0, "d.yaml", 0, 1, 1)));
        _ = cache.GetOrCompile<CelValue>(text, With(Segment(0, "d.yaml", 0, 1, 1)));
        Assert.Equal(1, cache.Count);

        CelCompileResult<CelValue> other = cache.GetOrCompile<CelValue>(text, With(Segment(0, "d.yaml", 40, 3, 7)));
        Assert.Equal(2, cache.Count);
        Assert.Equal(new CelSourceLocation("d.yaml", 44, 3, 11), other.Errors.Single(error => error.Code == CelErrorCode.UndeclaredReference).SourceLocation);

        // Options without segments and options with an empty array are one key.
        _ = cache.GetOrCompile<CelValue>(text);
        _ = cache.GetOrCompile<CelValue>(text, new CelCompileOptions { SourceSegments = [] });
        Assert.Equal(3, cache.Count);
    }

    [Fact]
    public void Parse_ReportsTheSyntaxErrorInTheDocumentAndNamesTheDocument()
    {
        CelCompileOptions options = With(Segment(0, "d.yaml", 20, 4, 9), Segment(6, "d.yaml", 40, 5, 9));

        CelParseResult failed = Env.Parse("[1 2,\n3 4]", options);
        CelParseResult parsed = Env.Parse("x +\nx", options);

        CelSourceLocation[] expected = [new CelSourceLocation("d.yaml", 23, 4, 12), new CelSourceLocation("d.yaml", 42, 5, 11)];

        Assert.False(failed.IsSuccess);
        Assert.Equal(expected, failed.Errors.Select(error => error.SourceLocation!.Value).ToArray());
        Assert.True(parsed.IsSuccess);
        Assert.Equal("d.yaml", parsed.GetParsedExprOrThrow().SourceName);
    }

    [Fact]
    public void ValidateAndCompileParsed_ReportThePlaceInTheDocument()
    {
        CelCompileOptions options = With(Segment(0, "d.yaml", 0, 1, 1), Segment(4, "d.yaml", 12, 2, 5));
        string text = "x +\nnope";

        CelError validated = Assert.Single(Env.Validate(text, options).Errors);
        CelError compiled = Assert.Single(Env.CompileParsed<CelValue>(Env.Parse(text).GetParsedExprOrThrow(), options).Errors);
        CelError validatedTree = Assert.Single(Env.ValidateParsed(Env.Parse(text).GetParsedExprOrThrow(), options).Errors);

        Assert.All(
            new[] { validated, compiled, validatedTree },
            error => Assert.Equal(new CelSourceLocation("d.yaml", 12, 2, 5), error.SourceLocation));
    }
}
