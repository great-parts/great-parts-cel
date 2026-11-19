using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Kinds;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// What the examples of the input of an environment leave out for an input that is a structure: the typed description
// CelObjectType<TInstance> reads the structure that the frame of the evaluation holds, by reference; a description that is not typed,
// or an input interface, gets one box for the evaluation; the cost and the exceptions of the typed read are those of any field read.
[Trait("Requirement", "F-12")]
public sealed class StructInputTests
{
    // The description reads the structure in the frame, not a copy of it: each read counts in the structure it was given, so two reads in
    // one expression give two counts, and the structure of the caller keeps its value.
    [Fact]
    public void StructureInput_IsReadByReferenceFromTheFrame()
    {
        CelEnvironment environment = CelEnvironment.Create(builder => builder.AddTypes(CelTypeProvider.Create("source", new CountingType())).UseInput<Cell>());
        CelProgram<bool> program = environment.Compile<bool>("count != count").GetProgramOrThrow();
        var cell = new Cell();

        Assert.True(program.EvaluateInput(cell, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(0L, cell.Count);

        // A new evaluation has a new frame: the count starts again.
        CelProgram<long> first = environment.Compile<long>("count").GetProgramOrThrow();
        Assert.Equal(1L, first.EvaluateInput(cell, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(1L, first.EvaluateInput(cell, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // A description over CelObjectType reads an object: the structure is boxed, once for the evaluation, whatever the number of reads.
    [Fact]
    public void StructureInputOfAnUntypedDescription_IsBoxedOnceForTheEvaluation()
    {
        var type = new PlainType();
        CelEnvironment environment = CelEnvironment.Create(builder => builder.AddTypes(CelTypeProvider.Create("source", type)).UseInput<Plain>());
        CelProgram<long> program = environment.Compile<long>("n + n").GetProgramOrThrow();

        Assert.Equal(6L, program.EvaluateInput(new Plain(3), cancellationToken: CancellationToken.None).GetValueOrThrow());

        Assert.Equal(2, type.Seen.Count);
        Assert.Same(type.Seen[0], type.Seen[1]);
        Assert.Equal(6L, program.EvaluateInput(new Plain(3), cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.NotSame(type.Seen[0], type.Seen[2]);
    }

    // The input type is an interface and the input a structure that implements it: the description is the one of the interface.
    [Fact]
    public void StructureThatImplementsTheInputInterface_IsReadThroughTheDescriptionOfTheInterface()
    {
        CelEnvironment environment = CelEnvironment.Create(builder => builder.AddTypes(KindsContext.Default).UseInput<IShape>());
        CelProgram<bool> program = environment.Compile<bool>("area > 12.0 && label == 'disc' && id == 7").GetProgramOrThrow();

        Assert.True(program.EvaluateInput(new Disc(), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // The structure of a type other than the input type is refused, as an instance of another class is.
    [Fact]
    public void StructureThatIsNotTheInputType_IsRefused()
    {
        CelEnvironment environment = CelEnvironment.Create(builder => builder.AddTypes(KindsContext.Default).UseInput<StructKind>());
        CelProgram<bool> program = environment.Compile<bool>("id > 0").GetProgramOrThrow();

        Assert.Throws<ArgumentException>("input", () => program.EvaluateInput(new PointKind(1, 2), cancellationToken: CancellationToken.None));
    }

    // The units that the typed description adds to the read are charged like those of any field: the unit of the identifier and the five.
    [Fact]
    public void FieldReadCost_OfTheTypedDescription_IsCharged()
    {
        CelEnvironment environment = CelEnvironment.Create(builder => builder.AddTypes(CelTypeProvider.Create("source", new CostlyType())).UseInput<Cell>());
        CelProgram<long> program = environment.Compile<long>("count").GetProgramOrThrow();

        CelEvalResult<long> enough = program.EvaluateInput(new Cell(), options: new CelEvalOptions { CostLimit = 100 }, cancellationToken: CancellationToken.None);
        CelEvalResult<long> short5 = program.EvaluateInput(new Cell(), options: new CelEvalOptions { CostLimit = 5 }, cancellationToken: CancellationToken.None);

        Assert.Equal(42L, enough.GetValueOrThrow());
        Assert.Equal(6UL, enough.Cost);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(short5.Errors).Code);
    }

    // An exception of the typed read is the error ApplicationException, as an exception of a getter is for a class.
    [Fact]
    public void ExceptionOfTheTypedRead_IsApplicationException()
    {
        CelEnvironment environment = CelEnvironment.Create(builder => builder.AddTypes(CelTypeProvider.Create("source", new FaultyType())).UseInput<Cell>());

        CelEvalResult<long> result = environment.Compile<long>("count").GetProgramOrThrow().EvaluateInput(new Cell(), cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        Assert.IsType<InvalidOperationException>(error.Exception);
    }

    private struct Cell
    {
        public long Count;
    }

    private readonly record struct Plain(long N);

    private readonly struct Disc : IShape
    {
        public long Id => 7;

        public string Label => "disc";

        public double Area => 12.5;
    }

    // Counts each read in the structure that the library passes, which a copy would not keep.
    private sealed class CountingType() : CelObjectType<Cell>("acme.Cell", [new(0, "count", CelType.Int)])
    {
        protected internal override CelValue GetField(in Cell instance, int number)
        {
            ref Cell cell = ref Unsafe.AsRef(in instance);
            cell.Count++;
            return CelValue.FromInt64(cell.Count);
        }

        protected internal override bool HasField(object instance, int number) => true;
    }

    private sealed class CostlyType() : CelObjectType<Cell>("acme.Cell", [new(0, "count", CelType.Int)])
    {
        protected internal override CelValue GetField(in Cell instance, int number) => CelValue.FromInt64(42);

        protected internal override long FieldReadCost(in Cell instance, int number) => 5;

        protected internal override bool HasField(object instance, int number) => true;
    }

    private sealed class FaultyType() : CelObjectType<Cell>("acme.Cell", [new(0, "count", CelType.Int)])
    {
        protected internal override CelValue GetField(in Cell instance, int number) => throw new InvalidOperationException("boom");

        protected internal override bool HasField(object instance, int number) => true;
    }

    // A description that is not typed: it keeps the objects it was asked to read.
    private sealed class PlainType() : CelObjectType("acme.Plain", typeof(Plain), [new(0, "n", CelType.Int)])
    {
        internal List<object> Seen { get; } = [];

        protected internal override CelValue GetField(object instance, int number)
        {
            Seen.Add(instance);
            return CelValue.FromInt64(((Plain)instance).N);
        }

        protected internal override bool HasField(object instance, int number) => true;
    }
}
