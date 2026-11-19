using System;

namespace Great.Parts.Cel.Evaluation;

// The frames that the current thread keeps for its next evaluations, for each type of frame apart: a thread that evaluates again and
// again takes the same frame each time and allocates none. A frame is taken out while an evaluation uses it, so an evaluation inside
// an evaluation, which application code makes in the same thread, finds the outer frame gone and rents another; the thread keeps up to
// Depth of them, enough for the usual nesting, and a deeper one makes frames that it does not keep.
internal static class FrameSpares<TFrame>
    where TFrame : Frame
{
    private const int Depth = 4;

    [ThreadStatic]
    private static TFrame?[]? t_spares;

    [ThreadStatic]
    private static int t_count;

    // A frame the thread kept, or null when it keeps none.
    internal static TFrame? Take()
    {
        TFrame?[]? spares = t_spares;
        if (spares is null || t_count == 0)
        {
            return null;
        }

        TFrame? frame = spares[--t_count];
        spares[t_count] = null;
        return frame;
    }

    // Keeps a released frame for the thread, unless it keeps Depth already.
    internal static void Put(TFrame frame)
    {
        TFrame?[] spares = t_spares ??= new TFrame?[Depth];
        if (t_count < spares.Length)
        {
            spares[t_count++] = frame;
        }
    }
}
