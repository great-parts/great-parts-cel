using System;
using Great.Parts.Cel.Library.Regex;
using Xunit;

namespace Great.Parts.Cel.Tests.Regex;

// The build of the program from the tree: the instructions of small patterns, listed from the first one in the order a walk finds
// them; the number of instructions against the parse; the list of ranges that the copies of a repeat share.
public sealed class ProgramBuilderTests
{
    // A star is a fork that the body returns to, a plus is the body and then the fork, a quest is a fork that skips the body; a
    // counted repeat is expanded as Simplify expands it: x{1,3} is x(x(x)?)?.
    [Theory]
    [InlineData("", "0:match")]
    [InlineData("a", "0:char(a)>1 1:match")]
    [InlineData("abc", "0:char(a)>1 1:char(b)>2 2:char(c)>3 3:match")]
    [InlineData("a*", "0:alt(1,2) 1:char(a)>0 2:match")]
    [InlineData("a+", "0:char(a)>1 1:alt(0,2) 2:match")]
    [InlineData("a?", "0:alt(1,2) 1:char(a)>2 2:match")]
    [InlineData("ab|cd", "0:alt(1,4) 1:char(a)>2 2:char(b)>3 3:match 4:char(c)>5 5:char(d)>3")]
    [InlineData("a{2}", "0:char(a)>1 1:char(a)>2 2:match")]
    [InlineData("a{1,3}", "0:char(a)>1 1:alt(2,5) 2:char(a)>3 3:alt(4,5) 4:char(a)>5 5:match")]
    [InlineData("a{2,}", "0:char(a)>1 1:char(a)>2 2:alt(1,3) 3:match")]
    public static void Program_OfSmallPatterns_HasTheInstructionsOfTheConstruction(string pattern, string listing)
    {
        Assert.Equal(listing, ProgramHelper.Listing(ProgramHelper.Compile(pattern)));
    }

    // The conditions of zero width, a class, a class under (?i), a literal under (?i), and the dot are one instruction each.
    [Theory]
    [InlineData("^a$", "0:empty(begin-text)>1 1:char(a)>2 2:empty(end-text)>3 3:match")]
    [InlineData("(?m)^a$", "0:empty(begin-line)>1 1:char(a)>2 2:empty(end-line)>3 3:match")]
    [InlineData("\\b\\B", "0:empty(word-boundary)>1 1:empty(not-word-boundary)>2 2:match")]
    [InlineData("[a-cx]", "0:class[61-63,78]>1 1:match")]
    [InlineData("(?i)a", "0:fold(A)>1 1:match")]
    [InlineData("(?i)1", "0:char(1)>1 1:match")]
    [InlineData(".", "0:any-not-newline>1 1:match")]
    [InlineData("(?s).", "0:any>1 1:match")]
    public static void Program_OfOneInstructionNodes_NamesTheCondition(string pattern, string listing)
    {
        Assert.Equal(listing, ProgramHelper.Listing(ProgramHelper.Compile(pattern)));
    }

    // The simplification merges a repeat into the repeat inside it, and x{0} and a repeat of the empty match leave nothing: the
    // program is the one of the pattern they simplify to.
    [Theory]
    [InlineData("(?:a*)*", "a*")]
    [InlineData("(?:a+)+", "a+")]
    [InlineData("(?:a?)?", "a?")]
    [InlineData("a{0,}", "a*")]
    [InlineData("a{1,}", "a+")]
    [InlineData("a{1}", "a")]
    [InlineData("x{0}y", "y")]
    [InlineData("(?:)*", "")]
    [InlineData("(?:){3}a", "a")]
    public static void Program_OfASimplifiedRepeat_IsTheProgramOfTheSimplifiedPattern(string pattern, string simplified)
    {
        Assert.Equal(ProgramHelper.Listing(ProgramHelper.Compile(simplified)), ProgramHelper.Listing(ProgramHelper.Compile(pattern)));
    }

    // A repeat of what can be empty keeps its fork: the fork of the star is entered again from every path of the body.
    [Theory]
    [InlineData("(?:^)*", "0:alt(1,2) 1:empty(begin-text)>0 2:match")]
    [InlineData("(?:a?)*", "0:alt(1,3) 1:alt(2,0) 2:char(a)>0 3:match")]
    public static void Program_OfARepeatOfWhatCanBeEmpty_ReturnsToTheFork(string pattern, string listing)
    {
        Assert.Equal(listing, ProgramHelper.Listing(ProgramHelper.Compile(pattern)));
    }

    // The three copies of [a-c] in [a-c]{3} are three instructions that name the one run of ranges.
    [Fact]
    public static void CopiesOfARepeat_ShareTheListOfRanges()
    {
        CompiledPattern program = ProgramHelper.Compile("[a-c]{3}");

        Assert.Equal(4, program.Instructions.Length);
        Assert.Equal(2, program.Ranges.Length);
        int classes = 0;
        foreach (Instruction instruction in program.Instructions)
        {
            if (instruction.Kind == InstructionKind.Class)
            {
                classes++;
                Assert.Equal(0, instruction.A);
                Assert.Equal(1, instruction.B);
            }
        }

        Assert.Equal(3, classes);
    }

    // Two different classes each have a run of their own, one after the other.
    [Fact]
    public static void TwoClasses_HaveTheirOwnRuns()
    {
        CompiledPattern program = ProgramHelper.Compile("[a-c][x-z0-1]");

        Assert.Equal("0:class[61-63]>1 1:class[30-31,78-7A]>2 2:match", ProgramHelper.Listing(program));
        Assert.Equal(6, program.Ranges.Length);
    }

    // A class under x{0} is counted by the parse and has no instruction, so the program does not carry its ranges.
    [Fact]
    public static void ClassUnderZeroRepeat_IsNotInTheProgram()
    {
        CompiledPattern program = ProgramHelper.Compile("[a-c]{0}b");

        Assert.Equal("0:char(b)>1 1:match", ProgramHelper.Listing(program));
        Assert.Empty(program.Ranges.ToArray());
    }

    // A class of a table is one instruction on the table, with no ranges of its own.
    [Fact]
    public static void ClassOfATable_HasNoRanges()
    {
        CompiledPattern program = ProgramHelper.Compile("\\pL\\PN\\d");

        Assert.Equal(4, program.Instructions.Length);
        Assert.Empty(program.Ranges.ToArray());
        Assert.All(
            program.Instructions.ToArray()[..3],
            instruction => Assert.Equal(InstructionKind.StaticClass, instruction.Kind));
    }

    // The instructions that the parse counted, the Match included, are the instructions of the program, and the size of the program
    // is that of the parse, which is the instructions and the ranges of the classes.
    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("a|b|c|dd|ee")]
    [InlineData("^$")]
    [InlineData("\\b\\B")]
    [InlineData("a*")]
    [InlineData("(?:a*)*")]
    [InlineData("(?:a*)+")]
    [InlineData("(a*)*")]
    [InlineData("(?:a+)+")]
    [InlineData("(?:a?){0,3}")]
    [InlineData("(?:a?){2,3}")]
    [InlineData("(?:a+){2,}")]
    [InlineData("(?:a*){3,}")]
    [InlineData("a{0}")]
    [InlineData("a{0}b")]
    [InlineData("a{1}")]
    [InlineData("a{2}")]
    [InlineData("a{2,5}")]
    [InlineData("a{0,5}")]
    [InlineData("a{3,}")]
    [InlineData("a{1,}")]
    [InlineData("a{0,}")]
    [InlineData("a{5,5}")]
    [InlineData("(?:){3}")]
    [InlineData("(?:){2,}")]
    [InlineData("(?:){0,2}a")]
    [InlineData("(?:^){2,4}")]
    [InlineData("(?:^)*")]
    [InlineData("(?:^)+")]
    [InlineData("(?:\\b)?x")]
    [InlineData("(?:(?:a{2}){3}b){2,3}")]
    [InlineData("(?:a|b*)+c?")]
    [InlineData("[a-z]{3}")]
    [InlineData("[a-z]+[0-9]*")]
    [InlineData("[^a-z]")]
    [InlineData("(?i)abc")]
    [InlineData("(?i)[k-s]+")]
    [InlineData("(?i)\\d\\s\\w")]
    [InlineData("(?i)\\p{Lu}")]
    [InlineData("\\p{Greek}+\\PL")]
    [InlineData("\\p{^Nd}{2}")]
    [InlineData("[\\d\\pL]{4}")]
    [InlineData("[^\\x00-\\x{10FFFF}]")]
    [InlineData("[^\\x00-\\x{10FFFF}]|b")]
    [InlineData("(?s).*(?:^|x)$")]
    [InlineData("(?m)^.+?$")]
    [InlineData("😀+é?")]
    [InlineData("(?:ab|ac|ad)(?:xy|xz)")]
    [InlineData("a*b*c*d*e*f*")]
    public static void Build_AgreesWithTheParse_InTheInstructionsAndTheSize(string pattern)
    {
        ProgramHelper.AssertWellFormed(pattern);
    }

    // A tree is built into a program once: the build leaves in the nodes where the ranges went.
    [Fact]
    public static void Build_OfTheSameTreeTwice_IsRefused()
    {
        using PatternTree tree = ParseHelper.Parse("[a-c]+");
        ProgramBuilder.Build(tree);

        Assert.Throws<InvalidOperationException>(() => ProgramBuilder.Build(tree));
    }

    // A program is immutable data: the same program run twice, and on different subjects, gives the answers it gives alone.
    [Fact]
    public static void Program_RunMoreThanOnce_GivesTheSameAnswers()
    {
        CompiledPattern program = ProgramHelper.Compile("(?:ab)+c");

        Assert.True(ProgramHelper.IsMatch(program, "xababc", out long first));
        Assert.False(ProgramHelper.IsMatch(program, "xabab", out _));
        Assert.True(ProgramHelper.IsMatch(program, "xababc", out long second));
        Assert.Equal(first, second);
    }
}
