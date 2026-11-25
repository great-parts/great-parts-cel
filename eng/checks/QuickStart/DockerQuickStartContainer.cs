using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The container of the quick start in Docker (<c>docs/design/eng-release.md</c>, "eng/checks quick-start"): a new container of the image
/// with no network, the directory of the packages mounted as the only package source, and one <c>bash</c> session in it. The commands are
/// written to the standard input of the session one at a time, each in braces with its input closed, so that a command that reads its input
/// does not read the next command and <c>cd</c> stays in effect; after each command the session prints a marker line with its exit code to
/// both of its output streams.
/// </summary>
internal sealed class DockerQuickStartContainer : IQuickStartContainer
{
    private const string PackagesDirectory = "/packages";
    private const string WorkDirectory = "/work";

    // The one package source of the container: the directory of the packages, and nothing from nuget.org.
    private const string NuGetConfig = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<configuration>\n  <packageSources>\n    <clear />\n    <add key=\"packages\" value=\"" + PackagesDirectory + "\" />\n  </packageSources>\n</configuration>\n";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly TimeSpan DockerLimit = TimeSpan.FromMinutes(10);

    private readonly string _name = "gpc-quick-start-" + Guid.NewGuid().ToString("N");
    private readonly string _marker = "@@gpc-" + Guid.NewGuid().ToString("N") + "@@";
    private readonly Process _session;

    /// <summary>Starts the container and the session.</summary>
    /// <param name="image">The image of the clean container, such as <c>mcr.microsoft.com/dotnet/sdk:10.0.404</c>.</param>
    /// <param name="packages">The full path of the directory of the packages.</param>
    /// <exception cref="CheckInputException">Docker cannot be started or cannot start the container.</exception>
    internal DockerQuickStartContainer(string image, string packages)
    {
        Docker(["run", "--detach", "--name", _name, "--network", "none", "--mount", $"type=bind,source={packages},target={PackagesDirectory},readonly", "--workdir", WorkDirectory, image, "sleep", "infinity"], null);
        try
        {
            Docker(["exec", "--interactive", _name, "sh", "-c", "cat > \"$1\"", "sh", WorkDirectory + "/NuGet.config"], NuGetConfig);
            _session = Start(["exec", "--interactive", _name, "bash"]);
        }
        catch (CheckInputException)
        {
            Remove();
            throw;
        }
    }

    /// <inheritdoc/>
    public QuickStartStepResult Run(string command, TimeSpan limit)
    {
        string script = $"{{ {command}\n}} < /dev/null\n__gpc=$?\nprintf '%s %s\\n' '{_marker}' \"$__gpc\"\nprintf '%s\\n' '{_marker}' >&2\n";
        try
        {
            _session.StandardInput.Write(script);
            _session.StandardInput.Flush();
        }
        catch (IOException)
        {
            return new QuickStartStepResult(1, string.Empty, "the session of the container has ended");
        }

        Task<(string Text, int Code)> output = ReadUntilMarker(_session.StandardOutput);
        Task<(string Text, int Code)> error = ReadUntilMarker(_session.StandardError);
        if (!Task.WhenAll(output, error).Wait(limit < TimeSpan.Zero ? TimeSpan.Zero : limit))
        {
            _session.Kill(entireProcessTree: true);
            return new QuickStartStepResult(null, string.Empty, string.Empty);
        }

        return new QuickStartStepResult(ExitCodeOf(output.Result.Code), output.Result.Text, error.Result.Text);
    }

    /// <inheritdoc/>
    public QuickStartStepResult Write(string fileName, string text, TimeSpan limit) =>
        Run($"printf '%s' '{Convert.ToBase64String(Utf8.GetBytes(text))}' | base64 -d > '{fileName}'", limit);

    /// <summary>Stops the session and removes the container.</summary>
    public void Dispose()
    {
        if (!_session.HasExited)
        {
            _session.Kill(entireProcessTree: true);
        }

        _session.Dispose();
        Remove();
    }

    // The lines up to the marker line; the marker may follow the last line of the output that has no line break. An end of the stream
    // before the marker (-1) means that the session ended: bash did not get to the marker.
    private async Task<(string Text, int Code)> ReadUntilMarker(StreamReader reader)
    {
        StringBuilder text = new();
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            int at = line.IndexOf(_marker, StringComparison.Ordinal);
            if (at < 0)
            {
                text.Append(line).Append('\n');
                continue;
            }

            text.Append(line, 0, at);
            return (text.ToString(), int.TryParse(line[(at + _marker.Length)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int code) ? code : 0);
        }

        return (text.ToString(), -1);
    }

    // A session that ended without the marker did not end with 0: the commands after its end have not run.
    private int ExitCodeOf(int code)
    {
        if (code >= 0)
        {
            return code;
        }

        _session.WaitForExit();
        return _session.ExitCode == 0 ? 1 : _session.ExitCode;
    }

    private void Remove()
    {
        try
        {
            Docker(["rm", "--force", _name], null);
        }
        catch (CheckInputException)
        {
            // Docker does not know the container any more, or does not answer: there is nothing else to do for it.
        }
    }

    private static void Docker(IReadOnlyList<string> arguments, string? input)
    {
        using Process process = Start(arguments);
        Task<string> error = process.StandardError.ReadToEndAsync();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        process.StandardInput.Write(input ?? string.Empty);
        process.StandardInput.Close();
        if (!process.WaitForExit(DockerLimit))
        {
            process.Kill(entireProcessTree: true);
            throw new CheckInputException($"'docker {arguments[0]}' did not end within {DockerLimit.TotalMinutes.ToString(CultureInfo.InvariantCulture)} minutes");
        }

        if (process.ExitCode != 0)
        {
            string first = error.GetAwaiter().GetResult().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "no message on standard error";
            throw new CheckInputException($"'docker {arguments[0]}' ended with the exit code {process.ExitCode.ToString(CultureInfo.InvariantCulture)}: {first}");
        }

        output.GetAwaiter().GetResult();
    }

    private static Process Start(IReadOnlyList<string> arguments)
    {
        ProcessStartInfo start = new("docker")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Utf8,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            return Process.Start(start) ?? throw new CheckInputException("docker was not started");
        }
        catch (Win32Exception exception)
        {
            throw new CheckInputException($"docker cannot be started: {exception.Message}", exception);
        }
    }
}
