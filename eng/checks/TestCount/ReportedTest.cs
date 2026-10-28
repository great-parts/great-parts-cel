namespace Great.Parts.Cel.Eng.Checks;

/// <summary>A test of an xUnit.net XML report that ran: the pair of the <c>type</c> and <c>name</c> attributes and the place the report gives.</summary>
/// <param name="Type">The <c>type</c> attribute: the full name of the class.</param>
/// <param name="Name">The <c>name</c> attribute: the full name of the test, with the arguments of a theory case.</param>
/// <param name="Method">The method name: the <c>method</c> attribute, or the name without the class and the arguments.</param>
/// <param name="SourceFile">The <c>source-file</c> attribute as written, or <see langword="null"/> when the report has no source for the test.</param>
/// <param name="SourceLine">The <c>source-line</c> attribute; 0 when the report has no source for the test.</param>
/// <param name="ElementLine">The line of the <c>&lt;test&gt;</c> element in the report, starting with 1.</param>
internal sealed record ReportedTest(string Type, string Name, string Method, string? SourceFile, int SourceLine, int ElementLine);
