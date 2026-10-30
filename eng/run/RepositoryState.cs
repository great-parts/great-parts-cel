namespace Great.Parts.Cel.Eng.Run;

/// <summary>
/// The commit that a run checked and whether the working tree is clean, for the last line of the command: a reviewer does not repeat
/// a run that names the same commit and a clean tree. Both are asked of <c>git</c> and are unknown when it cannot answer.
/// </summary>
/// <param name="Head">The full SHA of <c>HEAD</c>, or <see langword="null"/> when git cannot tell.</param>
/// <param name="Clean">Whether <c>git status</c> lists no change and no untracked file that git does not ignore; <see langword="null"/> when git cannot tell.</param>
internal sealed record RepositoryState(string? Head, bool? Clean)
{
    /// <summary>Asks git in the current directory.</summary>
    internal static RepositoryState Read()
    {
        ProcessResult head = Processes.Run("git", ["rev-parse", "HEAD"], capture: true);
        ProcessResult status = Processes.Run("git", ["status", "--porcelain"], capture: true);
        return new RepositoryState(
            head.Succeeded ? head.Output.Trim() : null,
            status.Succeeded ? status.Output.Trim().Length == 0 : null);
    }

    /// <summary>The state as the last line of a command writes it, from the state at the start and the state at the end.</summary>
    /// <param name="start">The state before the first step.</param>
    /// <param name="end">The state after the last step.</param>
    internal static string Describe(RepositoryState start, RepositoryState end)
    {
        string head = end.Head ?? "unknown";
        bool? clean = start.Clean is null || end.Clean is null ? null : start.Clean.Value && end.Clean.Value;
        string tree = clean is null ? "unknown" : clean.Value ? "clean" : "dirty";
        return $"head {head}; tree {tree}";
    }
}
