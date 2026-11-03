using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// `eng/checks links` (docs/design/eng-docs.md, "links"). Two kinds of reports are the input. One is a report that lychee 0.24.2 wrote
// in a container, kept as it is; it shows that the command reads the real form, the line and the column included. The other is built
// by the test in the same form, with the texts of the statuses that lychee 0.24.2 gives, one failure in each case. The resolver of
// the domains is a stub, so no test goes to the network. The command runs through Program, so the option and the exit codes are
// those of the command line.
public sealed class LinksTests
{
    private const string ReportPath = "artifacts/docs/lychee.json";
    private const string RootUrlInReport = "file:///work";

    // The input of the real report below, as lychee read it: README.md at the working directory /work, and a C# file.
    private const string RealReadme = """
        # Title

        See [the page](docs/missing.md) and [a heading](docs/cost.md#no-such-heading) and [ok](docs/cost.md#cost-limit).
        Also [a][b] link with an external address: https://httpbin.org/status/404 and
        [gone](https://httpbin.org/status/410), [unavailable](https://httpbin.org/status/503),
        [no domain](https://no-such-host-great-parts-cel.invalid/page).
        Rate: [limited](https://httpbin.org/status/429)

        [b]: https://httpbin.org/status/404
        """;

    private const string RealSource = """
        namespace A;

        /// <summary>
        /// See <see href="https://httpbin.org/status/404">the page</see> and docs/other.md.
        /// </summary>
        public static class A { }
        """;

    // The report of `lychee --include-fragments --format json README.md docs "src/**/*.cs"` for the two files above and docs/cost.md,
    // which has the heading "Cost limit". The report is not changed. The address of a file starts with the working directory of
    // the run, /work. The domain no-such-host-great-parts-cel.invalid is excluded by lychee itself.
    private const string RealReport = """
        {
          "total": 10,
          "unique": 8,
          "successful": 1,
          "unknown": 0,
          "unsupported": 0,
          "timeouts": 0,
          "redirects": 0,
          "remaps": 0,
          "excludes": 1,
          "errors": 8,
          "cached": 0,
          "success_map": {},
          "error_map": {
            "src/A.cs": [
              {
                "url": "https://httpbin.org/status/404",
                "status": {
                  "text": "Rejected status code: 404 Not Found",
                  "code": 404
                },
                "span": {
                  "line": 4,
                  "column": 20
                },
                "duration": {
                  "secs": 0,
                  "nanos": 689456764
                }
              }
            ],
            "README.md": [
              {
                "url": "https://httpbin.org/status/429",
                "status": {
                  "text": "Rejected status code: 429 Too Many Requests",
                  "code": 429
                },
                "span": {
                  "line": 7,
                  "column": 7
                },
                "duration": {
                  "secs": 0,
                  "nanos": 689433268
                }
              },
              {
                "url": "https://httpbin.org/status/503",
                "status": {
                  "text": "Rejected status code: 503 Service Unavailable",
                  "code": 503
                },
                "span": {
                  "line": 5,
                  "column": 41
                },
                "duration": {
                  "secs": 0,
                  "nanos": 689493295
                }
              },
              {
                "url": "https://httpbin.org/status/404",
                "status": {
                  "text": "Rejected status code: 404 Not Found",
                  "code": 404
                },
                "span": {
                  "line": 4,
                  "column": 44
                },
                "duration": {
                  "secs": 0,
                  "nanos": 689633348
                }
              },
              {
                "url": "file:///work/docs/missing.md",
                "status": {
                  "text": "File not found. Check if file exists and path is correct",
                  "details": "File not found. Check if file exists and path is correct"
                },
                "span": {
                  "line": 3,
                  "column": 5
                },
                "duration": {
                  "secs": 0,
                  "nanos": 1816881
                }
              },
              {
                "url": "file:///work/docs/cost.md#no-such-heading",
                "status": {
                  "text": "Cannot find fragment",
                  "details": "Cannot find fragment"
                },
                "span": {
                  "line": 3,
                  "column": 37
                },
                "duration": {
                  "secs": 0,
                  "nanos": 6454325
                }
              },
              {
                "url": "https://httpbin.org/status/404",
                "status": {
                  "text": "Rejected status code: 404 Not Found",
                  "code": 404
                },
                "span": {
                  "line": 4,
                  "column": 6
                },
                "duration": {
                  "secs": 0,
                  "nanos": 690896689
                }
              },
              {
                "url": "https://httpbin.org/status/410",
                "status": {
                  "text": "Rejected status code: 410 Gone",
                  "code": 410
                },
                "span": {
                  "line": 5,
                  "column": 1
                },
                "duration": {
                  "secs": 0,
                  "nanos": 689560656
                }
              }
            ]
          },
          "timeout_map": {},
          "suggestion_map": {},
          "redirect_map": {},
          "excluded_map": {
            "README.md": [
              {
                "url": "https://no-such-host-great-parts-cel.invalid/page",
                "status": {
                  "text": "Excluded",
                  "details": "This is due to your 'exclude' values"
                },
                "span": {
                  "line": 6,
                  "column": 1
                },
                "duration": {
                  "secs": 0,
                  "nanos": 2355
                }
              }
            ]
          },
          "duration": {
            "secs": 0,
            "nanos": 717997211
          },
          "detailed_stats": false
        }
        """;

    private static readonly string[] NoUnknownHosts = [];

    [Fact]
    public void LinksCommand_IsInTheRegistryAndTakesNoArgument()
    {
        ICheckCommand command = Assert.Single(CommandRegistry.All, candidate => candidate.Name == "links");

        Assert.Null(command.ArgumentName);
    }

    // ---- The real report ----

    [Fact]
    public void RealReport_GivesEveryFailureAtTheLineAndColumnOfTheReport()
    {
        using TempDirectory root = new();
        root.Write("README.md", RealReadme);
        root.Write("src/A.cs", RealSource);
        root.Write("docs/cost.md", "# Cost limit\n\nText.\n");

        Result result = Check(root, RealReport.Replace(RootUrlInReport, RootUrl(root), StringComparison.Ordinal));

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        AssertLines(
            result,
            new Finding("README.md", 3, 5, FindingSeverity.Error, EngCodes.Eng403, "file 'docs/missing.md' not found"),
            new Finding("README.md", 3, 37, FindingSeverity.Error, EngCodes.Eng404, "fragment '#no-such-heading' not found in docs/cost.md"),
            new Finding("README.md", 4, 6, FindingSeverity.Error, EngCodes.Eng401, "404 Not Found: https://httpbin.org/status/404"),
            new Finding("README.md", 4, 44, FindingSeverity.Error, EngCodes.Eng401, "404 Not Found: https://httpbin.org/status/404"),
            new Finding("README.md", 5, 1, FindingSeverity.Error, EngCodes.Eng401, "410 Gone: https://httpbin.org/status/410"),
            new Finding("README.md", 5, 41, FindingSeverity.Warning, EngCodes.Eng409, "503 Service Unavailable: https://httpbin.org/status/503"),
            new Finding("README.md", 7, 7, FindingSeverity.Warning, EngCodes.Eng409, "429 Too Many Requests: https://httpbin.org/status/429"),
            new Finding("src/A.cs", 4, 20, FindingSeverity.Error, EngCodes.Eng401, "404 Not Found: https://httpbin.org/status/404"));
    }

    // ---- Relative links: the file and the anchor ----

    [Fact]
    public void BrokenFileAndBrokenAnchor_AreEng403AndEng404AtTheirPlaces()
    {
        using TempDirectory root = new();
        root.Write("README.md", "# Title\n[a](docs/missing.md)\n[b](docs/cost.md#no-such-heading)\n");
        root.Write("docs/cost.md", "# Cost limit\n");
        string report = Report(
            Failed(
                "README.md",
                Link(FileUrl(root, "docs/missing.md"), "File not found. Check if file exists and path is correct", line: 2, column: 1),
                Link(FileUrl(root, "docs/cost.md#no-such-heading"), "Cannot find fragment", line: 3, column: 1)));

        Result result = Check(root, report);

        Assert.Equal(1, result.ExitCode);
        AssertLines(
            result,
            new Finding("README.md", 2, 1, FindingSeverity.Error, EngCodes.Eng403, "file 'docs/missing.md' not found"),
            new Finding("README.md", 3, 1, FindingSeverity.Error, EngCodes.Eng404, "fragment '#no-such-heading' not found in docs/cost.md"));
    }

    [Fact]
    public void AnAnchorOfTheSameFile_IsEng404WithTheNameOfTheFile()
    {
        using TempDirectory root = new();
        root.Write("docs/a.md", "# A\n[x](#nowhere)\n");
        string report = Report(Failed("docs/a.md", Link(FileUrl(root, "docs/a.md#nowhere"), "Cannot find fragment", line: 2, column: 1)));

        AssertLines(
            Check(root, report),
            new Finding("docs/a.md", 2, 1, FindingSeverity.Error, EngCodes.Eng404, "fragment '#nowhere' not found in docs/a.md"));
    }

    [Fact]
    public void ALinkThatCannotBeResolved_IsEng403()
    {
        using TempDirectory root = new();
        root.Write("README.md", "[a](/docs/b.md)\n");
        string text = "Error building URL for \"/docs/b.md\" (Attribute: Some(\"href\")): Cannot resolve root-relative link '/docs/b.md'";
        string details = "Cannot resolve root-relative link '/docs/b.md': To resolve root-relative links in local files, provide a root dir";
        string report = Report(Failed("README.md", Link("error:", text, details, line: 1, column: 1)));

        AssertLines(
            Check(root, report),
            new Finding(
                "README.md",
                1,
                1,
                FindingSeverity.Error,
                EngCodes.Eng403,
                "link cannot be resolved: Cannot resolve root-relative link '/docs/b.md': To resolve root-relative links in local files, provide a root dir"));
    }

    [Fact]
    public void AnAnchorOfAnExternalPage_IsEng404()
    {
        using TempDirectory root = new();
        root.Write("README.md", "[a](https://example.org/page#part)\n");
        string report = Report(Failed("README.md", Link("https://example.org/page#part", "Cannot find fragment", line: 1, column: 1)));

        AssertLines(
            Check(root, report),
            new Finding("README.md", 1, 1, FindingSeverity.Error, EngCodes.Eng404, "fragment '#part' not found in https://example.org/page"));
    }

    // ---- The place when the report gives none: the first place of the address in the file ----

    [Fact]
    public void ReportWithoutLineAndColumn_FindsTheFirstPlaceOfTheAddressInTheFile()
    {
        using TempDirectory root = new();
        root.Write(
            "README.md",
            "# Title\n[a](docs/missing.md)\n[b](docs/cost.md#no-such-heading)\nhttps://example.org/gone and https://example.org/gone\n[c](docs/missing.md)\n");
        string report = Report(
            Failed(
                "README.md",
                Link(FileUrl(root, "docs/missing.md"), "File not found. Check if file exists and path is correct"),
                Link(FileUrl(root, "docs/cost.md#no-such-heading"), "Cannot find fragment"),
                Link("https://example.org/gone", "Rejected status code: 404 Not Found", code: 404)));

        AssertLines(
            Check(root, report),
            new Finding("README.md", 2, 5, FindingSeverity.Error, EngCodes.Eng403, "file 'docs/missing.md' not found"),
            new Finding("README.md", 3, 5, FindingSeverity.Error, EngCodes.Eng404, "fragment '#no-such-heading' not found in docs/cost.md"),
            new Finding("README.md", 4, 1, FindingSeverity.Error, EngCodes.Eng401, "404 Not Found: https://example.org/gone"));
    }

    [Fact]
    public void ReportWithoutLineAndColumn_SearchesTheLinkAsTheFileThatHoldsItWritesIt()
    {
        using TempDirectory root = new();
        root.Write("docs/a.md", "# A\n\nSee [x](missing.md) and [y](other.md#part).\n");
        string report = Report(
            Failed(
                "docs/a.md",
                Link(FileUrl(root, "docs/missing.md"), "File not found. Check if file exists and path is correct"),
                Link(FileUrl(root, "docs/other.md#part"), "Cannot find fragment")));

        AssertLines(
            Check(root, report),
            new Finding("docs/a.md", 3, 9, FindingSeverity.Error, EngCodes.Eng403, "file 'docs/missing.md' not found"),
            new Finding("docs/a.md", 3, 29, FindingSeverity.Error, EngCodes.Eng404, "fragment '#part' not found in docs/other.md"));
    }

    [Fact]
    public void ReportWithoutLineAndColumn_AddressNotInTheFile_IsTheStartOfTheFile()
    {
        using TempDirectory root = new();
        root.Write("README.md", "# Title\n");
        string report = Report(Failed("README.md", Link("https://example.org/gone", "Rejected status code: 410 Gone", code: 410)));

        AssertLines(
            Check(root, report),
            new Finding("README.md", 1, 1, FindingSeverity.Error, EngCodes.Eng401, "410 Gone: https://example.org/gone"));
    }

    [Fact]
    public void ReportWithoutLineAndColumn_FileNotThere_IsTheStartOfTheFile()
    {
        using TempDirectory root = new();
        string report = Report(Failed("README.md", Link("https://example.org/gone", "Rejected status code: 404 Not Found", code: 404)));

        AssertLines(
            Check(root, report),
            new Finding("README.md", 1, 1, FindingSeverity.Error, EngCodes.Eng401, "404 Not Found: https://example.org/gone"));
    }

    [Fact]
    public void ReportWithoutLineAndColumn_AddressWrittenWithoutTheFinalSlash_IsFound()
    {
        using TempDirectory root = new();
        root.Write("README.md", "# Title\nSee https://example.org\n");
        string report = Report(Failed("README.md", Link("https://example.org/", "Rejected status code: 404 Not Found", code: 404)));

        AssertLines(
            Check(root, report),
            new Finding("README.md", 2, 5, FindingSeverity.Error, EngCodes.Eng401, "404 Not Found: https://example.org/"));
    }

    [Fact]
    public void AColumnOfCodePoints_IsConvertedToTheColumnOfCodeUnits()
    {
        using TempDirectory root = new();
        root.Write("README.md", "\U0001F600 [a](docs/missing.md)\n");
        string report = Report(
            Failed("README.md", Link(FileUrl(root, "docs/missing.md"), "File not found. Check if file exists and path is correct", line: 1, column: 3)));

        // The emoji is one code point for lychee and two UTF-16 code units for a finding.
        AssertLines(
            Check(root, report),
            new Finding("README.md", 1, 4, FindingSeverity.Error, EngCodes.Eng403, "file 'docs/missing.md' not found"));
    }

    [Fact]
    public void ASpanBeyondTheFile_IsTakenAsItIs()
    {
        using TempDirectory root = new();
        root.Write("README.md", "# Title\n");
        string report = Report(Failed("README.md", Link("https://example.org/gone", "Rejected status code: 404 Not Found", code: 404, line: 9, column: 4)));

        AssertLines(
            Check(root, report),
            new Finding("README.md", 9, 4, FindingSeverity.Error, EngCodes.Eng401, "404 Not Found: https://example.org/gone"));
    }

    // ---- External links: 404, 410, a domain that is not found, 503 ----

    [Theory]
    [InlineData(404, "404 Not Found")]
    [InlineData(410, "410 Gone")]
    public static void ExternalLink_With404Or410_IsEng401(int code, string reason)
    {
        using TempDirectory root = new();
        root.Write("docs/aot.md", "# AOT\n\nSee [page](https://example.org/page).\n");
        string report = Report(Failed("docs/aot.md", Link("https://example.org/page", $"Rejected status code: {reason}", code: code, line: 3, column: 5)));

        AssertLines(
            Check(root, report),
            new Finding("docs/aot.md", 3, 5, FindingSeverity.Error, EngCodes.Eng401, $"{reason}: https://example.org/page"));
    }

    [Theory]
    [InlineData(429, "429 Too Many Requests")]
    [InlineData(500, "500 Internal Server Error")]
    [InlineData(503, "503 Service Unavailable")]
    [InlineData(403, "403 Forbidden")]
    public static void ExternalLink_WithAnotherStatus_IsEng409WarningThatDoesNotFail(int code, string reason)
    {
        using TempDirectory root = new();
        string report = Report(Failed("README.md", Link("https://example.org/page", $"Rejected status code: {reason}", code: code, line: 2, column: 3)));

        Result result = Check(root, report);

        Assert.Equal(0, result.ExitCode);
        AssertLines(result, new Finding("README.md", 2, 3, FindingSeverity.Warning, EngCodes.Eng409, $"{reason}: https://example.org/page"));
    }

    [Fact]
    public void ExternalLink_WhoseDomainIsNotFound_IsEng402()
    {
        using TempDirectory root = new();
        string report = Report(Failed("README.md", ConnectionFailed("https://gone-domain.example.com/page", line: 4, column: 9)));

        Result result = Check(root, report, new StubResolver("gone-domain.example.com"));

        Assert.Equal(1, result.ExitCode);
        AssertLines(
            result,
            new Finding("README.md", 4, 9, FindingSeverity.Error, EngCodes.Eng402, "the domain gone-domain.example.com does not exist: https://gone-domain.example.com/page"));
    }

    [Fact]
    public void ExternalLink_ThatFailedToConnectToADomainThatExists_IsEng409()
    {
        using TempDirectory root = new();
        string report = Report(Failed("README.md", ConnectionFailed("https://example.org/page", line: 4, column: 9)));

        Result result = Check(root, report, new StubResolver(NoUnknownHosts));

        Assert.Equal(0, result.ExitCode);
        AssertLines(
            result,
            new Finding(
                "README.md",
                4,
                9,
                FindingSeverity.Warning,
                EngCodes.Eng409,
                "Connection failed. Check network connectivity and firewall settings: https://example.org/page"));
    }

    [Fact]
    public void TheDomainIsAskedOncePerHost()
    {
        using TempDirectory root = new();
        string report = Report(
            Failed(
                "README.md",
                ConnectionFailed("https://gone-domain.example.com/a", line: 1, column: 1),
                ConnectionFailed("https://GONE-DOMAIN.example.com/b", line: 2, column: 1)));
        StubResolver resolver = new("gone-domain.example.com");

        Result result = Check(root, report, resolver);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(2, result.Lines.Length);
        Assert.Equal(["gone-domain.example.com"], resolver.Asked);
    }

    [Fact]
    public void OtherFailuresOfAnExternalLink_AreEng409Warnings()
    {
        using TempDirectory root = new();
        string report = Report(
            Failed(
                "README.md",
                Link(
                    "http://127.0.0.1:9/x",
                    "Network error: Connection refused - server may be down or port blocked (error sending request for url (http://127.0.0.1:9/x))",
                    "Connection refused - server may be down or port blocked",
                    line: 1,
                    column: 1),
                Link(
                    "https://expired.example.com/",
                    "Network error: SSL certificate expired. Site needs to renew certificate (error sending request for url (https://expired.example.com/))",
                    "SSL certificate expired. Site needs to renew certificate",
                    line: 2,
                    column: 1)),
            Failed("docs/a.md", Link("https://10.255.255.1/x", "Timeout", "Request timed out", line: 3, column: 5)));
        root.Write("docs/a.md", "# A\n");

        Result result = Check(root, report, new StubResolver("expired.example.com"));

        Assert.Equal(0, result.ExitCode);
        AssertLines(
            result,
            new Finding("README.md", 1, 1, FindingSeverity.Warning, EngCodes.Eng409, "Connection refused - server may be down or port blocked: http://127.0.0.1:9/x"),
            new Finding("README.md", 2, 1, FindingSeverity.Warning, EngCodes.Eng409, "SSL certificate expired. Site needs to renew certificate: https://expired.example.com/"),
            new Finding("docs/a.md", 3, 5, FindingSeverity.Warning, EngCodes.Eng409, "Request timed out: https://10.255.255.1/x"));
    }

    [Fact]
    public void ATimeoutOfTheTimeoutMap_IsEng409AndTheDomainIsNotAsked()
    {
        using TempDirectory root = new();
        string report = TimeoutReport(Failed("README.md", Link("https://gone-domain.example.com/", "Timeout", "Request timed out", line: 1, column: 1)));
        StubResolver resolver = new("gone-domain.example.com");

        Result result = Check(root, report, resolver);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(resolver.Asked);
        AssertLines(
            result,
            new Finding("README.md", 1, 1, FindingSeverity.Warning, EngCodes.Eng409, "Request timed out: https://gone-domain.example.com/"));
    }

    // ---- The report: what is not a failure, the paths of the files ----

    [Fact]
    public void ReportWithoutFailures_HasNoFindings()
    {
        using TempDirectory root = new();
        string report = """
            {
              "total": 3, "unique": 3, "successful": 2, "unknown": 0, "unsupported": 0, "timeouts": 0, "redirects": 0, "remaps": 0, "excludes": 1, "errors": 0,
              "cached": 0, "success_map": {}, "error_map": {}, "timeout_map": {}, "suggestion_map": {}, "redirect_map": {},
              "excluded_map": { "README.md": [ { "url": "https://example.org/", "status": { "text": "Excluded", "details": "This is due to your 'exclude' values" }, "span": { "line": 1, "column": 1 } } ] },
              "duration": { "secs": 0, "nanos": 1000 }, "detailed_stats": false
            }
            """;

        Result result = Check(root, report);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void FilesOfTheReport_AreNamedRelativeToTheRootWithSlashes()
    {
        using TempDirectory root = new();
        root.Write("docs/a.md", "# A\n");
        string report = Report(
            Failed("./docs/a.md", Link("https://example.org/a", "Rejected status code: 404 Not Found", code: 404, line: 1, column: 1)),
            Failed(Path.Combine(root.Path, "README.md"), Link("https://example.org/b", "Rejected status code: 404 Not Found", code: 404, line: 1, column: 1)));

        AssertLines(
            Check(root, report),
            new Finding("docs/a.md", 1, 1, FindingSeverity.Error, EngCodes.Eng401, "404 Not Found: https://example.org/a"),
            new Finding("README.md", 1, 1, FindingSeverity.Error, EngCodes.Eng401, "404 Not Found: https://example.org/b"));
    }

    // ---- The command line ----

    [Fact]
    public void WithoutTheReportOption_IsAnInvalidInvocation()
    {
        using TempDirectory root = new();

        Result result = Run(root, ["links"], new StubResolver(NoUnknownHosts));

        AssertInputError(result, "the command links needs the option --report");
    }

    [Fact]
    public void AReportThatDoesNotExist_IsAnInvalidInvocation()
    {
        using TempDirectory root = new();

        Result result = Run(root, ["links", "--report", "artifacts/docs/none.json"], new StubResolver(NoUnknownHosts));

        AssertInputError(result, "does not exist", "none.json");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{ \"files\": {} }")]
    [InlineData("{ \"error_map\": [] }")]
    public static void AFileThatIsNotALycheeReport_IsAnInvalidInvocation(string text)
    {
        using TempDirectory root = new();

        Result result = Check(root, text);

        AssertInputError(result, "is not a lychee report");
    }

    [Fact]
    public void AReportRelativePath_IsTakenFromTheWorkingDirectory()
    {
        using TempDirectory root = new();
        root.Write(ReportPath, Report(Failed("README.md", Link("https://example.org/a", "Rejected status code: 404 Not Found", code: 404, line: 1, column: 1))));

        Result result = Run(root, ["links", "--report", ReportPath], new StubResolver(NoUnknownHosts));

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(["README.md(1,1): error ENG401: 404 Not Found: https://example.org/a"], result.Lines);
    }

    // ---- The resolver of the machine ----

    [Fact]
    public void TheResolverOfTheMachine_DoesNotTakeAKnownHostForUnknown()
    {
        Assert.False(new DnsHostResolver().IsUnknown("localhost"));
    }

    // ---- Helpers ----

    private static string RootUrl(TempDirectory root) => new Uri(root.Path + Path.DirectorySeparatorChar).AbsoluteUri.TrimEnd('/');

    private static string FileUrl(TempDirectory root, string relative) => RootUrl(root) + "/" + relative;

    // The report in the form of lychee: the maps of the failures and of the timeouts, with the other fields that lychee writes.
    private static string Report(params string[] failedFiles) => Build(string.Join(", ", failedFiles), string.Empty);

    private static string TimeoutReport(string failedFile) => Build(string.Empty, failedFile);

    private static string Build(string errors, string timeouts) =>
        $$"""
        { "total": 1, "unique": 1, "successful": 0, "unknown": 0, "unsupported": 0, "timeouts": 0, "redirects": 0, "remaps": 0, "excludes": 0, "errors": 1,
          "cached": 0, "success_map": {}, "error_map": { {{errors}} }, "timeout_map": { {{timeouts}} }, "suggestion_map": {}, "redirect_map": {}, "excluded_map": {},
          "duration": { "secs": 0, "nanos": 1000 }, "detailed_stats": false }
        """;

    private static string Failed(string file, params string[] links) =>
        $"{JsonSerializer.Serialize(file)}: [{string.Join(", ", links)}]";

    private static string Link(string url, string text, string? details = null, int? code = null, int? line = null, int? column = null)
    {
        string status = $"\"text\": {JsonSerializer.Serialize(text)}, \"details\": {JsonSerializer.Serialize(details ?? text)}";
        if (code is not null)
        {
            status = $"\"text\": {JsonSerializer.Serialize(text)}, \"code\": {code}";
        }

        string span = line is null || column is null ? string.Empty : $", \"span\": {{ \"line\": {line}, \"column\": {column} }}";
        return $"{{ \"url\": {JsonSerializer.Serialize(url)}, \"status\": {{ {status} }}{span}, \"duration\": {{ \"secs\": 0, \"nanos\": 1000 }} }}";
    }

    // The status that lychee 0.24.2 gives to a domain that does not resolve; it is the same as for any other failure to connect.
    private static string ConnectionFailed(string url, int line, int column) => Link(
        url,
        $"Network error: Connection failed. Check network connectivity and firewall settings (error sending request for url ({url}))",
        "Connection failed. Check network connectivity and firewall settings",
        line: line,
        column: column);

    private static Result Check(TempDirectory root, string report, IHostResolver? resolver = null)
    {
        root.Write(ReportPath, report);
        return Run(root, ["links", "--report", Path.Combine(root.Path, ReportPath)], resolver ?? new StubResolver(NoUnknownHosts));
    }

    private static Result Run(TempDirectory root, IReadOnlyList<string> args, IHostResolver resolver)
    {
        using StringWriter output = new() { NewLine = "\n" };
        using StringWriter error = new() { NewLine = "\n" };
        int exitCode = Program.Run([.. args, "--root", root.Path], [new LinksCommand(resolver)], root.Path, output, error);
        return new Result(exitCode, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries), error.ToString());
    }

    private static void AssertLines(Result result, params Finding[] expected)
    {
        Assert.Equal(string.Empty, result.Error);
        foreach (string line in result.Lines)
        {
            Assert.Matches(Finding.Pattern, line);
        }

        Assert.Equal(expected.Select(finding => finding.ToString()).Order(StringComparer.Ordinal), result.Lines.Order(StringComparer.Ordinal));
    }

    private static void AssertInputError(Result result, params string[] messageParts)
    {
        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Lines);
        foreach (string part in messageParts)
        {
            Assert.Contains(part, result.Error, StringComparison.Ordinal);
        }
    }

    private sealed record Result(int ExitCode, string[] Lines, string Error);

    // A resolver that knows which hosts have no address, and remembers what it was asked.
    private sealed class StubResolver(params string[] unknownHosts) : IHostResolver
    {
        public List<string> Asked { get; } = [];

        public bool IsUnknown(string host)
        {
            Asked.Add(host);
            return unknownHosts.Contains(host, StringComparer.OrdinalIgnoreCase);
        }
    }
}
