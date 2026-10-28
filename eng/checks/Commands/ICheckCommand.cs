using System.Collections.Generic;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>One command of <c>eng/checks</c>: reads the repository and returns its findings.</summary>
internal interface ICheckCommand
{
    /// <summary>The name of the command on the command line, such as <c>suppressions</c>.</summary>
    string Name { get; }

    /// <summary>
    /// The name of the one argument that follows the command on the command line, such as <c>&lt;range&gt;</c>, or <see langword="null"/>
    /// when the command takes none. The argument is in <see cref="CheckContext.Arguments"/>.
    /// </summary>
    string? ArgumentName => null;

    /// <summary>
    /// Runs the command. The findings are sorted by the caller. An input that does not exist is reported by <see cref="CheckInputException"/>.
    /// </summary>
    /// <param name="context">The root, the options of the run and the scans that several commands share.</param>
    IEnumerable<Finding> Run(CheckContext context);
}
