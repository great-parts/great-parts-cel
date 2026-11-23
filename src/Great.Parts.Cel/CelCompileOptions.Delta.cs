namespace Great.Parts.Cel;

public sealed partial class CelCompileOptions
{
    /// <summary>
    /// Whether the program is compiled for partial evaluation, <see cref="CelProgram{T}.EvaluatePartial"/> and
    /// <see cref="CelProgram{T}.EvaluatePartialAsync"/>; <see langword="false"/> by default.
    /// </summary>
    /// <remarks>
    /// Such a program checks the patterns of unknown data at each attribute of the expression and keeps the parsed expression
    /// for the residual, so it takes more memory, also as an entry of a compilation cache. A program compiled without it is
    /// planned as before and evaluates as fast; its partial members throw <see cref="System.InvalidOperationException"/>. The
    /// value is part of the key of the compilation cache. <c>Evaluate</c>, <c>EvaluateAsync</c> and the other members give the
    /// same results for programs compiled with and without it.
    /// </remarks>
    public bool PartialEvaluation { get; init; }
}
