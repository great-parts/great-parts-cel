namespace Great.Parts.Cel.Generator;

// The part of the writer that the pipeline calls: the text of the file of a context, from its model and nothing else.
internal static partial class ContextEmitter
{
    static partial void Emit(ContextModel model, ref string? text)
    {
        text = ContextWriter.Write(model);
    }
}
