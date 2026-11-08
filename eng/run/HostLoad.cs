using System;
using System.Globalization;
using System.IO;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>
/// The processor time of the machine that the kernel counts, as <c>/proc/stat</c> shows it. A container sees the counters of the whole
/// host kernel, not only its own, so the change of them between two readings tells how busy the host is, containers of other builds
/// included.
/// </summary>
/// <param name="Busy">The time of all processors that was not idle, in kernel ticks.</param>
/// <param name="Total">The time of all processors, in the same ticks.</param>
/// <param name="Processors">The number of processors.</param>
internal readonly record struct CpuTimes(long Busy, long Total, int Processors)
{
    /// <summary>
    /// How many processors worked between two readings: the share of busy time times the number of processors. It is 0 when no time
    /// passed between the readings.
    /// </summary>
    /// <param name="before">The earlier reading.</param>
    /// <param name="after">The later reading.</param>
    internal static double Cores(CpuTimes before, CpuTimes after)
    {
        long total = after.Total - before.Total;
        return total <= 0 ? 0 : (double)(after.Busy - before.Busy) / total * after.Processors;
    }

    /// <summary>
    /// Reads <c>/proc/stat</c>, or returns <see langword="null"/> where the file is absent or has another form: the wait then cannot see
    /// the host.
    /// </summary>
    /// <param name="path">The file to read; <c>/proc/stat</c> by default.</param>
    internal static CpuTimes? Read(string path = "/proc/stat")
    {
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Parses the text of <c>/proc/stat</c>: the line <c>cpu</c> has the time of all processors in the order user, nice, system, idle,
    /// iowait, irq, softirq, steal; the guest columns are part of user and nice and are not added again. Time in idle and iowait is not busy.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    internal static CpuTimes? Parse(string text)
    {
        long? busy = null;
        long total = 0;
        int processors = 0;
        foreach (string line in text.Split('\n'))
        {
            if (line.StartsWith("cpu ", StringComparison.Ordinal))
            {
                busy = ParseTotal(line, out total);
            }
            else if (line.StartsWith("cpu", StringComparison.Ordinal) && line.Length > 3 && char.IsAsciiDigit(line[3]))
            {
                processors++;
            }
        }

        return busy is null || processors == 0 ? null : new CpuTimes(busy.Value, total, processors);
    }

    // The busy time of the line `cpu`, or null when the line is not a list of at least eight numbers.
    private static long? ParseTotal(string line, out long total)
    {
        total = 0;
        string[] fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        const int Columns = 8;
        if (fields.Length < Columns + 1)
        {
            return null;
        }

        long idle = 0;
        for (int i = 1; i <= Columns; i++)
        {
            if (!long.TryParse(fields[i], NumberStyles.None, CultureInfo.InvariantCulture, out long ticks))
            {
                return null;
            }

            total += ticks;
            if (i is 4 or 5)
            {
                idle += ticks;
            }
        }

        return total - idle;
    }
}
