using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Values;

// Two type contexts written by hand, as the source generator writes them, that reach one .NET type: acme.Animal over the class
// Animal, which is not sealed. Each context has a description of its own. The context named Shelter has the smaller name, so
// its description stays in the type index and the description of Zoo is dropped as its alias, in either order of the two calls.
internal static class SharedTypeSamples
{
    // A class that is not sealed: an instance of Dog in a list(acme.Animal) is an Animal.
    internal class Animal
    {
        internal string? Name { get; init; }
    }

    internal sealed class Dog : Animal;

    // A description of Animal as the generator would write it, with the one field name; the arguments change its form.
    internal sealed class AnimalType(string name = "acme.Animal", string field = "name", CelType? type = null)
        : CelObjectType(name, typeof(Animal), [new CelObjectField(0, field, type ?? CelType.String)])
    {
        protected internal override CelValue GetField(object instance, int number) =>
            ((Animal)instance).Name is { } text ? CelValue.FromString(text) : FieldError(number, CelErrorCode.UnexpectedNull, null);

        protected internal override bool HasField(object instance, int number) => ((Animal)instance).Name is not null;
    }

    // A description of Dog, another .NET type than Animal, with a name that is given: with the name of the one of Animal it is
    // another type of the same name.
    internal sealed class DogType(string name) : CelObjectType(name, typeof(Dog), [])
    {
        protected internal override CelValue GetField(object instance, int number) => throw new System.InvalidOperationException();

        protected internal override bool HasField(object instance, int number) => throw new System.InvalidOperationException();
    }

    // A type context with the types that are given.
    internal sealed class Context(string sourceName, CelObjectType[] objectTypes, CelEnumType[] enumTypes)
        : CelTypeContext(sourceName, objectTypes, enumTypes);

    // An environment of the two contexts with the variables animals of list(acme.Animal) and a and b of acme.Animal.
    internal sealed class World
    {
        private World(CelEnvironment environment, AnimalType zoo, AnimalType shelter)
        {
            Environment = environment;
            Zoo = zoo;
            Shelter = shelter;
        }

        internal CelEnvironment Environment { get; }

        // The description of the context Zoo, which the index drops.
        internal AnimalType Zoo { get; }

        // The description of the context Shelter, which stays.
        internal AnimalType Shelter { get; }

        internal static World Create(bool zooFirst)
        {
            var zoo = new AnimalType();
            var shelter = new AnimalType();
            var zooContext = new Context("Zoo", [zoo], []);
            var shelterContext = new Context("Shelter", [shelter], []);
            CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);
            builder = zooFirst ? builder.AddTypes(zooContext).AddTypes(shelterContext) : builder.AddTypes(shelterContext).AddTypes(zooContext);
            CelEnvironment environment = builder
                .AddVariable("animals", CelType.ListOf(zoo.Type))
                .AddVariable("a", zoo.Type)
                .AddVariable("b", zoo.Type)
                .Build()
                .GetEnvironmentOrThrow();
            return new World(environment, zoo, shelter);
        }

        internal CelEvalResult<object> Evaluate(string expression, CelBindings bindings) =>
            Environment.Compile<object>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);

        // The units that one evaluation charges, with a limit that is never reached.
        internal ulong Cost(string expression, CelBindings bindings)
        {
            Plan plan = Environment.Compile<CelValue>(expression).GetProgramOrThrow().Plan;
            Frame frame = plan.CreateFrame(bindings.Values, ulong.MaxValue, CancellationToken.None);
            _ = plan.Run(frame);
            return frame.ChargedUnits;
        }
    }
}
