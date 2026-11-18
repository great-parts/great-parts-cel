namespace SpeedBench;

/// <summary>
/// A new expression text for every compilation and full cycle: the expression followed by a tail of
/// 16 CEL whitespace characters (space, \t, \n, \r; 2 bits each) that encodes a static counter. The counter
/// only grows and is never reset in the process, so every call in the process gets a text it has not
/// seen yet, with the same length and meaning. 2^32 different tails; one benchmark process makes far fewer calls.
/// </summary>
public static class ExpressionText
{
    public const int TailLength = 16;

    private const string Whitespace = " \t\n\r";

    private static uint s_counter;

    /// <summary>Creates the next text. The benchmarks and the measurement of text creation call only this method.</summary>
    public static string Next(string expression)
    {
        var n = s_counter++;
        return string.Create(expression.Length + TailLength, (expression, n), static (span, state) =>
        {
            state.expression.AsSpan().CopyTo(span);
            var value = state.n;
            for (var i = state.expression.Length; i < span.Length; i++)
            {
                span[i] = Whitespace[(int)(value & 3)];
                value >>= 2;
            }
        });
    }
}
