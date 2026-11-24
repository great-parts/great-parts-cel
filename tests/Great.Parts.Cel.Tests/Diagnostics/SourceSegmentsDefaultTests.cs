using System;
using Xunit;

namespace Great.Parts.Cel.Tests.Diagnostics;

// The value default of a segment names no document, which no constructor of the segment makes; the options refuse it when the
// segments are set, as an error of the call, and not as an exception from the first error that is mapped to the document.
[Trait("Requirement", "F-31")]
public sealed class SourceSegmentsDefaultTests
{
    [Fact]
    public void DefaultSegment_IsRefusedWhenTheSegmentsAreSet()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new CelCompileOptions { SourceSegments = [default] });

        Assert.Equal("segments", exception.ParamName);
    }
}
