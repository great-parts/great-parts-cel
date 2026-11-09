namespace Great.Parts.Cel.Tests.Values;

// A deterministic sequence of numbers (SplitMix64) for the samples of the tests of time: the same values in every run, in
// JIT and under Native AOT.
internal sealed class TimeSamples(ulong seed)
{
    private ulong _state = seed;

    // The next 64 bits.
    internal ulong NextBits()
    {
        _state += 0x9E3779B97F4A7C15UL;
        ulong z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    // A number from the minimum up to and excluding the maximum.
    internal long Next(long minimum, long maximumExclusive) =>
        minimum + (long)(NextBits() % (ulong)(maximumExclusive - minimum));

    // A number from zero up to and excluding the maximum.
    internal int Next(int maximumExclusive) => (int)Next(0, maximumExclusive);
}
