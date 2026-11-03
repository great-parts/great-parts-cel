namespace Great.Parts.Cel.Eng.Checks;

/// <summary>One member of <c>Great.Parts.Cel.Diagnostics.CelErrorCode</c> as the page of error codes shows it.</summary>
/// <param name="Name">The name of the member.</param>
/// <param name="Number">The constant value of the member.</param>
/// <param name="Stage">The stage that follows from the number, see <see cref="ErrorCodeStages"/>.</param>
/// <param name="Summary">The text of the <c>&lt;summary&gt;</c> of the member in one line, with Markdown for the code elements.</param>
internal sealed record ErrorCodeMember(string Name, int Number, string Stage, string Summary);
