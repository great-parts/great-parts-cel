namespace Great.Parts.Cel.Values;

// A CelOptional<T> seen without its type argument, so that the input rows convert it to a CEL value where the static type is
// not known: boxed in a place of object, or as the static type T of a generic row. Only CelOptional<T> implements it, explicitly.
internal interface IOptionalSource
{
    // The CEL value of the optional: the empty optional, or the optional that holds the held value converted by the input rows;
    // an error value when the conversion of the held value gave one. False when the held value has no input row.
    bool TryToCelValue(out CelValue result);
}
