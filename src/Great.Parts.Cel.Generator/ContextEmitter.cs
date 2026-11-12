namespace Great.Parts.Cel.Generator;

// The hand-over from the pipeline to the writer of the created code. The pipeline gives a ContextModel and nothing else; the
// writer, in Emit/, implements Emit and sets the text of the file, or leaves it null to write no file. A partial method that
// nothing implements is removed by the compiler together with its call, so no file is written.
internal static partial class ContextEmitter
{
    internal static string? Write(ContextModel model)
    {
        string? text = null;
        Emit(model, ref text);
        return text;
    }

    static partial void Emit(ContextModel model, ref string? text);
}
