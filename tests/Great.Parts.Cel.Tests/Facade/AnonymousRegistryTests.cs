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
        var seen = new AnonymousRegistry.Entry?[Threads, Types.Length];
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

                    AnonymousRegistry.Entry? before = seen[thread, index];
                    if (before is not null && !ReferenceEquals(before, entry))
                    {
                        failures[thread] = "the entry of " + Types[index] + " changed between two reads";
                        break;
                    }

                    seen[thread, index] = entry;
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
                if (seen[thread, index] is { } read)
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
}
