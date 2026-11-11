using System;

namespace Great.Parts.Cel.Library.Regex;

// The build of a program from a parsed pattern: a Thompson construction on the simplified tree, as compile.go of Go does it. The
// walk is a loop over explicit stacks (ProgramWalk), so the height of the tree and the counts of the repeats cost no stack.
internal static class ProgramBuilder
{
    // Builds the program of a parsed pattern. The tree is read, and not changed but for the places where the build notes where the
    // ranges of a class went; the caller disposes it. The program has the number of instructions that the parse counted, the one
    // that ends it included.
    internal static CompiledPattern Build(PatternTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        if (tree.IsBuilt)
        {
            throw new InvalidOperationException("The tree has been built into a program already.");
        }

        tree.IsBuilt = true;
        ProgramWalk walk = new(tree);
        try
        {
            return walk.Run();
        }
        finally
        {
            walk.Dispose();
        }
    }
}
