using System.Collections.Generic;
using System.IO;
using Google.Protobuf.Collections;
using Great.Parts.Cel.Generator.Samples.Protos;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Generator.Samples.Special;

// Messages of protobuf as a member, an element and a value: the context names the message by its descriptor full name
// (great_parts.samples.Parcel) and reads the instance as it is; the description comes from a provider of
// Great.Parts.Cel.Protobuf, which an environment adds with AddTypes, in either order with the context. Without it Build is
// TypeNotDeclared (4008) for each field that names the message. The context lists no message and its closure holds none.

public sealed class Shipment
{
    public Parcel Parcel { get; set; } = new();

    public Parcel? NoParcel { get; set; }

    public IReadOnlyList<Parcel> Parcels { get; set; } = [];

    public Dictionary<string, Parcel> ByLabel { get; set; } = [];

    // The collections of protobuf are collections by the rows (IReadOnlyList<T>, IReadOnlyDictionary<K,V>); the order of a MapField read
    // by the created code is by ascending key, not the order of insertion.
    public RepeatedField<string> Tags { get; set; } = new();

    public MapField<string, long> Counts { get; set; } = new();
}

[CelObject(typeof(Shipment), Name = "samples.Shipment")]
public sealed partial class MessageContext : CelTypeContext
{
}

// The place rule inside a collection: an element has the declared element type of the member. An abstract class and an
// interface are paired with their description at every read, a class that is not sealed when the instance is of a derived
// type, a sealed class never (the exact type is the declared one).

public abstract class Animal
{
    public string Name { get; set; } = "";
}

public sealed class Dog : Animal
{
    public long Tricks { get; set; }
}

public class Cat : Animal
{
    public long Lives { get; set; }
}

public sealed class Lion : Cat
{
    public double Mane { get; set; }
}

public sealed class Zoo
{
    public List<Animal> Animals { get; set; } = [];

    public List<Dog> Dogs { get; set; } = [];

    public List<Cat> Cats { get; set; } = [];
}

[CelObject(typeof(Zoo), Name = "samples.Zoo")]
[CelObject(typeof(Animal), Name = "samples.Animal")]
[CelObject(typeof(Dog), Name = "samples.Dog")]
[CelObject(typeof(Cat), Name = "samples.Cat")]
public sealed partial class PlaceContext : CelTypeContext
{
}

// A hidden member has no warning, and a selection of it is NoSuchField.
public sealed class Hiding
{
    public long Visible { get; set; }

    [CelIgnore]
    public Stream? HiddenStream { get; set; }
}

[CelObject(typeof(Hiding), Name = "samples.Hiding")]
public sealed partial class HidingContext : CelTypeContext
{
}
