using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

// The content of a value of the kind Abstract: a value of an abstract type of an extension set, such as net.IP. CelValue holds the
// object in its reference field and nothing else, so the rules of the kind need only the type and the equality of the content.
// A subclass is one object of at most 48 bytes, sealed, and immutable.
internal abstract class AbstractValue
{
    // The abstract type of the value; its name is the runtime name of the type.
    internal abstract CelType Type { get; }

    // Whether the other value has the same type and the same content.
    internal abstract bool ContentEquals(AbstractValue other);

    // A hash that two values with equal content share.
    internal abstract int ContentHash();
}
