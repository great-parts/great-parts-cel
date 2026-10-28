using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>The commands of <c>eng/checks</c>. A command is added here together with its class.</summary>
internal static class CommandRegistry
{
    /// <summary>Every command of the application.</summary>
    internal static IReadOnlyList<ICheckCommand> All { get; } = [new SuppressionsCommand(), new DuplicatesCommand(), new RatchetCommand(), new SignoffCommand(), new PackagesCommand(), new TestCountCommand(), new MutationCommand()];
}
