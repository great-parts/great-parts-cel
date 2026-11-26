using System;
using System.Threading.Tasks;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The registry of anonymous types is read without a lock while modules register types: every reader sees, for each type, either no entry
// or the one entry that the first registration made, and every registration of a type that is there changes nothing. The eight types are
// closed generic classes of the framework, so that the test needs no anonymous types of its own.
[Trait("Requirement", "R-4")]
[Trait("Requirement", "U-7")]
public sealed class AnonymousRegistryTests
{
    private static readonly Type[] Types =
    [
        typeof(Tuple<int>), typeof(Tuple<long>), typeof(Tuple<string>), typeof(Tuple<double>),
        typeof(Tuple<byte>), typeof(Tuple<short>), typeof(Tuple<char>), typeof(Tuple<bool>),
    ];

    private static readonly Action[] Registrations =
    [
        RegisterOne<Tuple<int>>, RegisterOne<Tuple<long>>, RegisterOne<Tuple<string>>, RegisterOne<Tuple<double>>,
        RegisterOne<Tuple<byte>>, RegisterOne<Tuple<short>>, RegisterOne<Tuple<char>>, RegisterOne<Tuple<bool>>,
    ];

    private static void RegisterOne<TData>()
        where TData : class =>
        AnonymousRegistry.Register<TData>(static _ => { }, static (_, _) => { });

    [Fact]
    public void ReadsDuringRegistrations_SeeOneEntryForEachType()
    {
        const int Threads = 32;
        var seen = new AnonymousRegistry.Entry?[Threads][];
        for (int row = 0; row < Threads; row++)
        {
            seen[row] = new AnonymousRegistry.Entry?[Types.Length];
        }

        var failures = new string?[Threads];

        Parallel.For(0, Threads, new ParallelOptions { MaxDegreeOfParallelism = Threads }, thread =>
        {
            for (int round = 0; round < 2_000 && failures[thread] is null; round++)
            {
                if (thread % 2 == 0)
                {
                    Registrations[(thread + round) % Registrations.Length]();
                    continue;
                }

                for (int index = 0; index < Types.Length; index++)
                {
                    if (!AnonymousRegistry.TryGet(Types[index], out AnonymousRegistry.Entry? entry))
                    {
                        continue;
                    }

                    AnonymousRegistry.Entry? before = seen[thread][index];
                    if (before is not null && !ReferenceEquals(before, entry))
                    {
                        failures[thread] = "the entry of " + Types[index] + " changed between two reads";
                        break;
                    }

                    seen[thread][index] = entry;
                }
            }
        });

        Assert.All(failures, failure => Assert.Null(failure));
        foreach (Type type in Types)
        {
            Assert.True(AnonymousRegistry.TryGet(type, out AnonymousRegistry.Entry? final));
            for (int thread = 1; thread < Threads; thread += 2)
            {
                int index = Array.IndexOf(Types, type);
                if (seen[thread][index] is { } read)
                {
                    Assert.Same(final, read);
                }
            }
        }
    }

    [Fact]
    public void SecondRegistration_KeepsTheFirstEntry()
    {
        RegisterOne<Tuple<int>>();
        Assert.True(AnonymousRegistry.TryGet(typeof(Tuple<int>), out AnonymousRegistry.Entry? first));

        RegisterOne<Tuple<int>>();

        Assert.True(AnonymousRegistry.TryGet(typeof(Tuple<int>), out AnonymousRegistry.Entry? second));
        Assert.Same(first, second);
    }

    [Fact]
    public void UnregisteredType_IsNotFound()
    {
        Assert.False(AnonymousRegistry.TryGet(typeof(Tuple<int, int, int>), out AnonymousRegistry.Entry? entry));
        Assert.Null(entry);
    }

    // The entry of the type that was read last is kept in a field that is read before the dictionary: reads of two types, one after the
    // other, must still give each type its own entry, and an unregistered type must still be a miss while the field holds another one.
    [Fact]
    public void ReadsOfTwoTypesInTurn_GiveTheEntryOfEachType()
    {
        RegisterOne<Tuple<float>>();
        RegisterOne<Tuple<ushort>>();

        for (int round = 0; round < 3; round++)
        {
            Assert.True(AnonymousRegistry.TryGet(typeof(Tuple<float>), out AnonymousRegistry.Entry? first));
            Assert.Same(typeof(Tuple<float>), first.DataType);

            Assert.True(AnonymousRegistry.TryGet(typeof(Tuple<ushort>), out AnonymousRegistry.Entry? second));
            Assert.Same(typeof(Tuple<ushort>), second.DataType);
            Assert.NotSame(first, second);

            Assert.False(AnonymousRegistry.TryGet(typeof(Tuple<int, int, int>), out AnonymousRegistry.Entry? missing));
            Assert.Null(missing);
        }
    }

    // The binding of an entry reads the data as the type of its registration and hands the bindings to the delegate of the registration.
    [Fact]
    public void BindingOfAnEntry_CallsTheDelegateOfItsRegistrationWithTheData()
    {
        AnonymousRegistry.Entry entry = SimpleEntryFixtures.Register(
            new Tuple<decimal>(0m),
            static _ => { },
            static (data, bindings) => bindings.Add("registryBound", (double)data.Item1));
        var bindings = new CelBindings();

        entry.Bind(new Tuple<decimal>(2.5m), bindings);

        Assert.Equal((object)2.5, bindings["registryBound"]);
    }

    // The simple entry takes the entry of each call from the registry by the type of the data: two anonymous types that alternate in one
    // thread, which the field of the last entry follows, each find their own environment and bindings.
    [Fact]
    public void CallsWithTwoAnonymousTypesInTurn_EvaluateEachOnItsOwnData()
    {
        for (long round = 0; round < 4; round++)
        {
            Assert.Equal(round + 1, CelExpression.Evaluate<long>("turnFirst + 1", new { turnFirst = round }, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(round * 2, CelExpression.Evaluate<long>("turnSecond * 2", new { turnSecond = round }, cancellationToken: TestContext.Current.CancellationToken));
        }
    }
}
