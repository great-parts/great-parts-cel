namespace Great.Parts.Cel;

// The option of the ordering between numeric types.
public sealed partial class CelEnvironmentBuilder
{
    // Whether EnableCrossTypeNumericComparisons was called.
    private bool _crossTypeNumericComparisons;

    /// <summary>
    /// Lets type checking accept the ordering operators <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c> and <c>&gt;=</c> between
    /// values of different numeric types — <c>int</c>, <c>uint</c> and <c>double</c> — whose static types are known, as
    /// the option <c>CrossTypeNumericComparisons</c> of cel-go does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By the CEL language definition, type checking requires the same type on both sides of an ordering operator, and
    /// accepts mixed numeric types only for a <c>dyn</c> operand: <c>1 &lt; 1.5</c> is the type checking error
    /// <see cref="Diagnostics.CelErrorCode.NoMatchingOverload"/>. With this option it checks and evaluates to
    /// <see langword="true"/>, and <c>price &gt; 0</c> is accepted for a <c>double</c> field <c>price</c>. The comparison
    /// itself is the one the standard library already uses for <c>dyn</c> operands: <c>int</c> and <c>uint</c> compare exactly,
    /// and an integer compares with a <c>double</c> as the integer rounded to the nearest <c>double</c>.
    /// </para>
    /// <para>
    /// Without this option an expression converts one side explicitly, as in <c>double(n) &gt; 0.5</c>, or has a
    /// <c>dyn</c> operand. The option is for ordering only: equality between numeric types is not changed by it. An environment
    /// description in JSON does not set it; <see cref="CelEnvironment.ToBuilder"/> keeps it.
    /// </para>
    /// </remarks>
    /// <returns>This builder.</returns>
    public CelEnvironmentBuilder EnableCrossTypeNumericComparisons()
    {
        _crossTypeNumericComparisons = true;
        return this;
    }
}
