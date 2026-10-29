// Command oracle is the reference side of the differential run: it answers CEL cases with cel-go
// (docs/design/eng-oracle.md).
//
// Exit codes of serve: 0 when standard input ends; 1 for a malformed request or a result without a
// cel.expr.Value form; 2 for an invalid invocation, a refused manifest, an environment that cel-go
// cannot build, or a missing ZONEINFO file.
//
// Exit codes of conformance: 0 when every case passes; 1 when a case fails; 2 for an invalid
// invocation, a file that is not a SimpleTestFile, or a missing ZONEINFO file.
package main

import (
	"errors"
	"flag"
	"fmt"
	"io"
	"os"
)

const usage = "usage: oracle serve --env <manifest>\n       oracle conformance <file>..."

func main() {
	os.Exit(run(os.Args[1:], os.Stdin, os.Stdout, os.Stderr))
}

func run(args []string, stdin io.Reader, stdout, stderr io.Writer) int {
	if len(args) == 0 {
		fmt.Fprintln(stderr, usage)
		return 2
	}
	switch args[0] {
	case "serve":
		return runServe(args[1:], stdin, stdout, stderr)
	case "conformance":
		return runConformanceCommand(args[1:], stdout, stderr)
	default:
		fmt.Fprintf(stderr, "oracle: unknown command %q\n%s\n", args[0], usage)
		return 2
	}
}

func runServe(args []string, stdin io.Reader, stdout, stderr io.Writer) int {
	flags := flag.NewFlagSet("serve", flag.ContinueOnError)
	flags.SetOutput(stderr)
	manifest := flags.String("env", "", "the run manifest, a JSON environment description")
	if err := flags.Parse(args); err != nil {
		return 2
	}
	if *manifest == "" || flags.NArg() != 0 {
		fmt.Fprintln(stderr, usage)
		return 2
	}
	if err := checkZoneInfo(); err != nil {
		fmt.Fprintf(stderr, "oracle: %v\n", err)
		return 2
	}
	environment, err := loadEnvironment(*manifest)
	if err != nil {
		fmt.Fprintf(stderr, "oracle: %v\n", err)
		return 2
	}
	if err := serve(environment, stdin, stdout); err != nil {
		fmt.Fprintf(stderr, "oracle: %v\n", err)
		return 1
	}
	return 0
}

// runConformanceCommand runs cel-spec test files once by the conformance rule
// (docs/design/eng-oracle.md, "One-time run of test files").
func runConformanceCommand(args []string, stdout, stderr io.Writer) int {
	if len(args) == 0 {
		fmt.Fprintln(stderr, usage)
		return 2
	}
	if err := checkZoneInfo(); err != nil {
		fmt.Fprintf(stderr, "oracle: %v\n", err)
		return 2
	}
	return runConformance(args, stdout, stderr)
}

// checkZoneInfo requires the variable ZONEINFO to name an existing file, so that every time zone
// comes from zoneinfo.zip of the pinned Go (docs/design/eng-oracle.md, "Time zones").
func checkZoneInfo() error {
	path := os.Getenv("ZONEINFO")
	if path == "" {
		return errors.New("ZONEINFO is not set; it must name the zoneinfo.zip file of the pinned Go")
	}
	info, err := os.Stat(path)
	if err != nil {
		return fmt.Errorf("ZONEINFO names no file: %v", err)
	}
	if !info.Mode().IsRegular() {
		return fmt.Errorf("ZONEINFO names no file: %s is not a regular file", path)
	}
	return nil
}
