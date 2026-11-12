using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// The macros of the internal set block, version 1 (the page of the extension sets, "block"), which only the environment of the
// conformance runner has: cel.block(slots, result) expands to cel.@block(slots, result), cel.index(N) to the variable @indexN,
// and cel.iterVar(N, M) and cel.accuVar(N, M) to the names @it:N:M and @ac:N:M of the variables of a fold; the variables
// @index0 to @index29 have the type dyn. A slot is evaluated at its first read and once for each entry into the block, and a slot that is
// never read is not evaluated, so its error does not appear; a slot may read a later slot, and a slot that reads itself finds
// its cell in progress, so the read goes on to the cell of the same number in the next enclosing block that has it, as in
// cel-go: the slot of an inner block that names its own number reads the outer slot of that number, and the
// read ends Unbound (3009) at the macro that names it only when no block is left, as for a number that no enclosing block
// has slots for; an inner block that has the number shadows the outer one for it, and the numbers it has not go to the outer
// block. The errors of expansion are InvalidMacroCall (1006) at the argument that
// is wrong, with the text "invalid call of macro '<macro>': <text of cel-go>"; the texts are those of the conformance
// runner of cel-go v0.32.0 (cel.block requires the first arg to be a list literal; cel.index requires a single
// non-negative int constant arg; cel.iterVar and cel.accuVar require two non-negative int constant args), which the page of the
// set gives as the text of the macros. cel.index(N) with N of 30 or more is UndeclaredReference at the "(" of the macro,
// where the nodes of an expansion stand, "undeclared reference to '@indexN'". The variable @indexN outside a block is
// declared and not bound, so its read is Unbound (3009) at the same place. A call with another number of
// arguments is no macro. In an environment of the application the macros do not exist: cel is a name that nothing
// declares, so every call is UndeclaredReference at cel, one error for each cel that the text has, a call in an argument
// included (the function is not named, as in ExtensionMacroTests),
// and cel.bind, which is the macro of bindings, is there. The names @index0, @it:0:0 and the function cel.@block cannot be written in a
// text: the "@" is no token (SyntaxError at the "@", among the errors of the text: the list is ordered by offset, so an
// error of a macro at an earlier offset comes first). The values are those of cel-go v0.32.0 (a probe of the oracle with
// the cases of the runner and the macros of its test library, 2026-11-11). Rows are those of ExtensionRows.
[Trait("Requirement", "F-1")]
[Trait("Requirement", "R-1")]
[Trait("Requirement", "F-4")]
public sealed class BlockMacroTests
{
    private const string Prefix = "invalid call of macro ";

    private const string Values = """
        cel.block([1, 2], 3) => 3
        cel.block([], 1) => 1
        cel.block([1 + 1, cel.index(0) + 1], cel.index(1)) => 3
        cel.block([1], cel.index(0) + cel.index(0)) => 2
        cel.block([1, 2, 3], cel.index(1)) => 2
        cel.block(['a', 'b'], cel.index(0) + cel.index(1)) => 'ab'
        cel.block([1, [cel.index(0), cel.index(0)]], cel.index(1)) => [1, 1]
        cel.block([[1, 2, 3]], cel.index(0).map(x, x * 2)) => [2, 4, 6]
        cel.block([[1, 2, 3]], cel.index(0).exists(x, x > 2)) => true
        cel.block([2], [1, 3].map(x, cel.index(0) + x)) => [3, 5]
        cel.block([cel.bind(x, 1, x + 1)], cel.index(0)) => 2
        cel.bind(x, cel.block([1], cel.index(0)), x) => 1
        cel.block([2], cel.bind(x, 1, x + cel.index(0))) => 3
        cel.block([optional.of(1)], cel.index(0).value()) => 1
        cel.block([5], optional.of(cel.index(0)).optMap(v, v + cel.index(0))) => optional.of(10)
        cel.block([[1, 2]], cel.index(0).first()) => optional.of(1)
        cel.block([[1, 2]], cel.index(0)[?1]) => optional.of(2)
        cel.block([{'a': 2}, cel.index(0)['a'], cel.index(1) * cel.index(1), cel.index(1) + cel.index(2)], cel.index(3)) => 6
        cel.block([1], cel.block([cel.index(0) + 1], cel.index(0))) => 2
        cel.block([1, 2], cel.block([cel.index(1) + 10], cel.index(0) + cel.index(1))) => 14
        [1, 2].map(cel.iterVar(0, 0), cel.iterVar(0, 0)) => [1, 2]
        [1, 2].map(cel.iterVar(0, 0), cel.iterVar(0, 0) + 1) => [2, 3]
        [1, 2].exists(cel.iterVar(0, 0), cel.iterVar(0, 0) > 1) => true
        cel.bind(cel.accuVar(0, 0), 1, cel.accuVar(0, 0) + 1) => 2
        [1, 2].map(cel.iterVar(0, 0), [1, 2, 3].filter(cel.iterVar(1, 0), cel.iterVar(1, 0) == cel.iterVar(0, 0))) => [[1], [2]]
        """;

    // A slot that is not read is not evaluated; a slot is read through the operators that evaluate one of their parts only,
    // and the part that is not evaluated does not read it.
    private const string LazySlots = """
        cel.block([1 / 0, 2], cel.index(1)) => 2
        cel.block([cel.index(1), 2], cel.index(0)) => 2
        cel.block([1, 2], cel.block([3], cel.index(1))) => 2
        cel.block([2], true ? cel.index(0) : 1 / 0) => 2
        cel.block([1 / 0], false ? cel.index(0) : 5) => 5
        cel.block([1 / 0], false && cel.index(0)) => false
        cel.block([1 / 0], true || cel.index(0)) => true
        """;

    private const string EvaluationErrors = """
        Evaluate | cel.block([1 ^/ 0, 2], cel.index(0)) | DivisionByZero | division by zero
        Evaluate | cel.block([1 ^/ 0], cel.index(0) + 1) | DivisionByZero | division by zero
        Evaluate | cel.index^(0) | Unbound | no value is bound to variable '@index0'
        Evaluate | cel.index^(29) | Unbound | no value is bound to variable '@index29'
        Evaluate | cel.block([cel.index^(0)], cel.index(0)) | Unbound | no value is bound to variable '@index0'
        Evaluate | cel.block([1, 2], cel.index^(2)) | Unbound | no value is bound to variable '@index2'
        Evaluate | cel.block([cel.block([cel.index^(0)], cel.index(0))], cel.index(0)) | Unbound | no value is bound to variable '@index0'
        """;

    private const string ExpansionErrors = $$"""
        Parse | cel.block(^1, 2) | InvalidMacroCall | {{Prefix}}'block': cel.block requires the first arg to be a list literal
        Parse | cel.block(^'a', 2) | InvalidMacroCall | {{Prefix}}'block': cel.block requires the first arg to be a list literal
        Parse | cel.block(^x, 2) | InvalidMacroCall | {{Prefix}}'block': cel.block requires the first arg to be a list literal
        Parse | cel.block(^{1: 2}, 2) | InvalidMacroCall | {{Prefix}}'block': cel.block requires the first arg to be a list literal
        Parse | cel.index(^-1) | InvalidMacroCall | {{Prefix}}'index': cel.index requires a single non-negative int constant arg
        Parse | cel.index(^1.0) | InvalidMacroCall | {{Prefix}}'index': cel.index requires a single non-negative int constant arg
        Parse | cel.index(^1u) | InvalidMacroCall | {{Prefix}}'index': cel.index requires a single non-negative int constant arg
        Parse | cel.index(^'a') | InvalidMacroCall | {{Prefix}}'index': cel.index requires a single non-negative int constant arg
        Parse | cel.index(^x) | InvalidMacroCall | {{Prefix}}'index': cel.index requires a single non-negative int constant arg
        Parse | cel.index(1 ^+ 1) | InvalidMacroCall | {{Prefix}}'index': cel.index requires a single non-negative int constant arg
        Parse | cel.iterVar(^-1, 0) | InvalidMacroCall | {{Prefix}}'iterVar': cel.iterVar requires two non-negative int constant args
        Parse | cel.iterVar(0, ^-1) | InvalidMacroCall | {{Prefix}}'iterVar': cel.iterVar requires two non-negative int constant args
        Parse | cel.iterVar(^x, 0) | InvalidMacroCall | {{Prefix}}'iterVar': cel.iterVar requires two non-negative int constant args
        Parse | cel.accuVar(0, ^y) | InvalidMacroCall | {{Prefix}}'accuVar': cel.accuVar requires two non-negative int constant args
        Parse | cel.accuVar(^1u, 0) | InvalidMacroCall | {{Prefix}}'accuVar': cel.accuVar requires two non-negative int constant args
        Parse | cel.accuVar(^'a', 0) | InvalidMacroCall | {{Prefix}}'accuVar': cel.accuVar requires two non-negative int constant args
        Parse | cel.block(^1, 2) + cel.index(^-1) | InvalidMacroCall InvalidMacroCall | {{Prefix}}'block': cel.block requires the first arg to be a list literal ;; {{Prefix}}'index': cel.index requires a single non-negative int constant arg
        """;

    // The first argument that is wrong is the error: for the two numbers of iterVar and accuVar the first number, then the second.
    private const string FirstWrongArgument = $$"""
        Parse | cel.iterVar(^-1, -2) | InvalidMacroCall | {{Prefix}}'iterVar': cel.iterVar requires two non-negative int constant args
        Parse | cel.accuVar(^x, -2) | InvalidMacroCall | {{Prefix}}'accuVar': cel.accuVar requires two non-negative int constant args
        """;

    // A call with another number of arguments is no macro: the receiver cel is a name that nothing declares.
    private const string OtherArity = """
        Check | ^cel.block([1]) | UndeclaredReference | undeclared reference to 'cel' (in container '')
        Check | ^cel.block([1], 2, 3) | UndeclaredReference | undeclared reference to 'cel' (in container '')
        Check | ^cel.index(0, 1) | UndeclaredReference | undeclared reference to 'cel' (in container '')
        Check | ^cel.index() | UndeclaredReference | undeclared reference to 'cel' (in container '')
        Check | ^cel.iterVar(0) | UndeclaredReference | undeclared reference to 'cel' (in container '')
        Check | ^cel.accuVar(1, 2, 3) | UndeclaredReference | undeclared reference to 'cel' (in container '')
        """;

    // The variables that the set declares are @index0 to @index29; a larger index, a fold variable written alone and
    // a name of another fold depth are names that nothing declares.
    private const string UndeclaredNames = """
        Check | cel.index^(30) | UndeclaredReference | undeclared reference to '@index30' (in container '')
        Check | cel.index^(31) | UndeclaredReference | undeclared reference to '@index31' (in container '')
        Check | cel.index^(100) | UndeclaredReference | undeclared reference to '@index100' (in container '')
        Check | cel.block([1, 2], cel.index^(30)) | UndeclaredReference | undeclared reference to '@index30' (in container '')
        Check | cel.iterVar^(0, 0) | UndeclaredReference | undeclared reference to '@it:0:0' (in container '')
        Check | cel.accuVar^(0, 0) | UndeclaredReference | undeclared reference to '@ac:0:0' (in container '')
        Check | [1, 2].exists(cel.iterVar(0, 0), cel.iterVar^(1, 0) > 1) | UndeclaredReference | undeclared reference to '@it:1:0' (in container '')
        Check | [1, 2].map(cel.iterVar(0, 0), cel.accuVar^(0, 0)) | UndeclaredReference | undeclared reference to '@ac:0:0' (in container '')
        """;

    // In an environment of the application, where the set is not: every call is UndeclaredReference at cel, whatever its
    // arguments, even those that the macro would refuse, and one more for each cel in an argument (the arguments are checked as
// in cel.bind(x, 1, x) of ExtensionMacroTests); cel.bind is the macro of bindings and is there.
    private const string WithoutTheSet = """
        Check | ^cel.block([1], 2) | UndeclaredReference | undeclared reference to 'cel' (in container '')
        Check | ^cel.block([1], ^cel.index(0)) | UndeclaredReference UndeclaredReference | undeclared reference to 'cel' (in container '') ;; undeclared reference to 'cel' (in container '')
        Check | ^cel.block(1, 2) | UndeclaredReference | undeclared reference to 'cel' (in container '')
        Check | ^cel.index(0) | UndeclaredReference | undeclared reference to 'cel' (in container '')
        Check | ^cel.index(-1) | UndeclaredReference | undeclared reference to 'cel' (in container '')
        Check | ^cel.index(30) | UndeclaredReference | undeclared reference to 'cel' (in container '')
        Check | ^cel.iterVar(0, 0) | UndeclaredReference | undeclared reference to 'cel' (in container '')
        Check | ^cel.accuVar(0, 0) | UndeclaredReference | undeclared reference to 'cel' (in container '')
        Check | ^cel | UndeclaredReference | undeclared reference to 'cel' (in container '')
        Check | ^cel.block | UndeclaredReference | undeclared reference to 'cel' (in container '')
        """;

    private readonly CelEnvironment runner = BlockExample.Builder()
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelEnvironment application = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Bindings(1))
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void MacrosOfTheSet_GiveTheValuesOfTheRunner() => Assert.Empty(ExtensionRows.CheckValues(runner, Values));

    [Fact]
    public void SlotThatIsNotRead_IsNotEvaluated() => Assert.Empty(ExtensionRows.CheckValues(runner, LazySlots));

    [Fact]
    public void SlotThatIsRead_GivesItsError_AndIndexOutsideABlockIsUnbound() => Assert.Empty(ExtensionRows.CheckErrors(runner, EvaluationErrors));

    [Fact]
    public void ArgumentThatIsNotALiteral_IsInvalidMacroCallAtTheArgument() => Assert.Empty(ExtensionRows.CheckErrors(runner, ExpansionErrors));

    // The error of an expansion is the first wrong argument only: a macro call has one error whatever else is wrong in it.
    [Fact]
    public void MacroCall_HasTheFirstWrongArgumentOnly() => Assert.Empty(ExtensionRows.CheckErrors(runner, FirstWrongArgument));

    [Fact]
    public void CallWithAnotherNumberOfArguments_IsNoMacro() => Assert.Empty(ExtensionRows.CheckErrors(runner, OtherArity));

    [Fact]
    public void NameThatNothingDeclares_IsUndeclaredReferenceAtTheMacro() => Assert.Empty(ExtensionRows.CheckErrors(runner, UndeclaredNames));

    // Every @index0 to @index29 is declared: a block of N + 1 slots read at N gives the slot.
    [Fact]
    public void EveryIndexUpTo29_IsDeclared()
    {
        var failures = new List<string>();
        for (int index = 0; index < 30; index++)
        {
            string slots = string.Join(", ", Enumerable.Range(0, index + 1).Select(slot => slot.ToString(CultureInfo.InvariantCulture)));
            string expression = "cel.block([" + slots + "], cel.index(" + index.ToString(CultureInfo.InvariantCulture) + "))";
            failures.AddRange(ExtensionRows.CheckValues(runner, expression + " => " + index.ToString(CultureInfo.InvariantCulture)));
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void InEnvironmentOfTheApplication_TheMacrosOfTheSetDoNotExist()
    {
        Assert.Empty(ExtensionRows.CheckErrors(application, WithoutTheSet));
        Assert.Empty(ExtensionRows.CheckValues(application, "cel.bind(x, 1, x + 1) => 2"));
    }

    // The names of the set cannot be written: the "@" is no token of the syntax, so SyntaxError at it is among the errors. The
    // list is ordered by offset, and a macro that refuses the stub of its argument, as map does in the last case, has its
    // InvalidMacroCall at the call, before the "@" (MacroTests, "InvalidMacroCall_IsListedByItsOffset"); the first error is not tested.
    [Theory]
    [InlineData("@index0", 0)]
    [InlineData("cel.@index0", 4)]
    [InlineData("cel.@block([1], 2)", 4)]
    [InlineData("[1].map(@it:0:0, 1)", 8)]
    public void NameOfTheSet_CannotBeWrittenInTheText(string expression, int offset)
    {
        CelCompileResult<CelValue> compiled = runner.Compile<CelValue>(expression);

        Assert.False(compiled.IsSuccess);
        Assert.Contains((CelErrorCode.SyntaxError, CelErrorStage.Parse, offset), compiled.Errors.Select(error => (error.Code, error.Stage, error.Location.Offset)));
    }
}
