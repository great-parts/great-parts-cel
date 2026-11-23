using System.Threading;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel;

// The state of an asynchronous evaluation of a program on bindings, and on an input that is an object, after the first pass found a
// call to wait for. Nothing of it exists for an evaluation that completes in the first pass.
internal class AsyncEvaluation<T> : AsyncDriver<CelEvalResult<T>>
{
    private readonly object? _input;

    internal AsyncEvaluation(
        CelProgram<T> program,
        CelBindings bindings,
        object? input,
        ulong? limit,
        int window,
        AsyncCallTable table,
        ulong charged,
        uint events,
        CancellationToken cancellationToken)
        : base(program.Plan, limit, window, table, charged, events, cancellationToken)
    {
        Program = program;
        Bindings = bindings;
        _input = input;
    }

    private protected CelProgram<T> Program { get; }

    private protected CelBindings Bindings { get; }

    private protected override Frame Rent() => Program.Plan.RentFrame(Bindings.Values, Limit, Token, _input);

    private protected override CelEvalResult<T> Complete(CelValue root) => Program.ResultOf(root, Cost);
}

// The same for an input that is a structure: the state holds it and every pass reads it in place, so it is never boxed.
internal sealed class AsyncInputEvaluation<T, TInput> : AsyncEvaluation<T>
{
    private readonly TInput _structure;
    private readonly CelObjectType<TInput>? _description;

    internal AsyncInputEvaluation(
        CelProgram<T> program,
        CelBindings bindings,
        TInput structure,
        CelObjectType<TInput>? description,
        ulong? limit,
        int window,
        AsyncCallTable table,
        ulong charged,
        uint events,
        CancellationToken cancellationToken)
        : base(program, bindings, null, limit, window, table, charged, events, cancellationToken)
    {
        _structure = structure;
        _description = description;
    }

    private protected override Frame Rent() =>
        Program.Plan.RentInputFrame(Bindings.Values, Limit, _structure, _description, Token);
}

// The state of an asynchronous partial evaluation: the patterns, and the record of the values of the nodes in the last pass, from which
// the residual expression is made.
internal sealed class AsyncPartialEvaluation<T> : AsyncDriver<CelPartialEvalResult<T>>
{
    private readonly CelProgram<T> _program;
    private readonly CelBindings _bindings;
    private readonly UnknownPattern[] _patterns;
    private readonly CelParsedExpr _tree;
    private PartialState? _state;
    private ValueRecord _record = ValueRecord.Empty;

    internal AsyncPartialEvaluation(
        CelProgram<T> program,
        CelBindings bindings,
        UnknownPattern[] patterns,
        CelParsedExpr tree,
        ulong? limit,
        int window,
        AsyncCallTable table,
        ulong charged,
        uint events,
        CancellationToken cancellationToken)
        : base(program.Plan, limit, window, table, charged, events, cancellationToken)
    {
        _program = program;
        _bindings = bindings;
        _patterns = patterns;
        _tree = tree;
    }

    private protected override Frame Rent() => _program.Plan.RentFrame(_bindings.Values, Limit, Token);

    private protected override void Begin(Frame frame)
    {
        _state = new PartialState(_patterns, _tree.IdCount, _program.Plan.KeyCount);
        frame.Partial = _state;
    }

    // The record of the last pass is kept; the records of the others are given back unread.
    private protected override void End(Frame frame, bool last)
    {
        if (_state is null)
        {
            return;
        }

        if (last)
        {
            _record = _state.Release();
        }
        else
        {
            _state.Discard();
        }

        _state = null;
    }

    private protected override CelPartialEvalResult<T> Complete(CelValue root) =>
        _program.PartialResult(_tree, root, Cost, new ResidualSource(_tree, _record));
}
