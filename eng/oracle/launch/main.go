// Command launch is the entry point of the oracle container (docs/design/eng-oracle.md, "Launcher"). It
// runs the steps gofmt, go vet and go build as child processes and passes their lines on, with no check
// logic. Without arguments it serves one session of eng/difftest on port 7070; with arguments it prints
// the step lines on standard error, opens no port and passes the arguments to the oracle.
//
// It runs in eng/oracle and uses the standard library only.
package main

import (
	"bufio"
	"bytes"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"sync"
	"time"
)

const (
	address = ":7070"
	// waitLimit is how long the launcher waits for eng/difftest; the run that started the container
	// may end before it reaches the oracle.
	waitLimit = 15 * time.Minute
)

// layout holds the paths of one run; the launcher runs in eng/oracle of the working copy.
type layout struct {
	module string // eng/oracle
	root   string // the repository root
	output string // artifacts/oracle
	binary string // artifacts/oracle/oracle
}

// step is one child process of the launcher; failed decides on its output and error.
type step struct {
	name    string
	command []string
	failed  func(output []string, err error) bool
	// after runs when the step failed, for example gofmt -d after gofmt -l.
	after *step
}

func main() {
	os.Exit(run(os.Args[1:]))
}

func run(args []string) int {
	module, err := os.Getwd()
	if err != nil {
		fmt.Fprintf(os.Stderr, "launch: %v\n", err)
		return 1
	}
	root := filepath.Clean(filepath.Join(module, "..", ".."))
	output := filepath.Join(root, "artifacts", "oracle")
	paths := layout{module: module, root: root, output: output, binary: filepath.Join(output, "oracle")}
	if len(args) > 0 {
		return runArguments(paths, args)
	}
	return runSession(paths)
}

func steps(paths layout) []step {
	exitCode := func(_ []string, err error) bool { return err != nil }
	return []step{
		{
			name:    "gofmt -l .",
			command: []string{"gofmt", "-l", "."},
			failed:  func(output []string, err error) bool { return err != nil || len(output) != 0 },
			after:   &step{name: "gofmt -d .", command: []string{"gofmt", "-d", "."}},
		},
		{name: "go vet ./...", command: []string{"go", "vet", "./..."}, failed: exitCode},
		{name: "go build", command: []string{"go", "build", "-o", paths.binary, "."}, failed: exitCode},
	}
}

// runSteps deletes the output directory and runs the steps; it returns the name of the failed step.
func runSteps(paths layout, emit func(string)) (string, bool) {
	if err := os.RemoveAll(paths.output); err != nil {
		emit(fmt.Sprintf("cannot delete %s: %v", paths.output, err))
		return "delete " + paths.output, false
	}
	for _, s := range steps(paths) {
		emit("== " + s.name)
		output, err := runStep(paths, s)
		for _, line := range output {
			emit(line)
		}
		if err != nil && !isExitError(err) {
			emit(err.Error())
		}
		if !s.failed(output, err) {
			continue
		}
		if s.after != nil {
			emit("== " + s.after.name)
			afterOutput, _ := runStep(paths, *s.after)
			for _, line := range afterOutput {
				emit(line)
			}
		}
		return s.name, false
	}
	return "", true
}

// runStep runs one step in eng/oracle and returns the lines of its standard output and error.
func runStep(paths layout, s step) ([]string, error) {
	command := exec.Command(s.command[0], s.command[1:]...)
	command.Dir = paths.module
	combined, err := command.CombinedOutput()
	text := strings.TrimRight(string(combined), "\n")
	if text == "" {
		return nil, err
	}
	return strings.Split(text, "\n"), err
}

func isExitError(err error) bool {
	var exitError *exec.ExitError
	return errors.As(err, &exitError)
}

func exitCodeOf(err error) int {
	if err == nil {
		return 0
	}
	var exitError *exec.ExitError
	if errors.As(err, &exitError) {
		return exitError.ExitCode()
	}
	return 1
}

// runArguments prints the step lines on standard error and runs the oracle with the arguments in the
// repository root, with the standard streams of the container.
func runArguments(paths layout, args []string) int {
	emit := func(line string) { fmt.Fprintln(os.Stderr, line) }
	if failed, ok := runSteps(paths, emit); !ok {
		fmt.Fprintf(os.Stderr, "failed: %s\n", failed)
		return 1
	}
	oracle := exec.Command(paths.binary, args...)
	oracle.Dir = paths.root
	oracle.Stdin = os.Stdin
	oracle.Stdout = os.Stdout
	oracle.Stderr = os.Stderr
	err := oracle.Run()
	if err != nil && !isExitError(err) {
		fmt.Fprintf(os.Stderr, "launch: %v\n", err)
	}
	return exitCodeOf(err)
}

// connection writes whole lines to the session connection from several goroutines.
type connection struct {
	mutex sync.Mutex
	conn  net.Conn
}

func (c *connection) writeLine(line []byte) {
	c.mutex.Lock()
	defer c.mutex.Unlock()
	// A write error means eng/difftest closed the connection; the oracle then ends on its input.
	_, _ = c.conn.Write(append(line, '\n'))
}

func (c *connection) writeJSON(key, value string) {
	line, err := json.Marshal(map[string]string{key: value})
	if err != nil {
		panic(err)
	}
	c.writeLine(line)
}

// runSession serves one session of eng/difftest (docs/design/eng-oracle.md, "Launcher").
func runSession(paths layout) int {
	listener, err := net.Listen("tcp", address)
	if err != nil {
		fmt.Fprintf(os.Stderr, "launch: %v\n", err)
		return 1
	}
	if err := listener.(*net.TCPListener).SetDeadline(time.Now().Add(waitLimit)); err != nil {
		fmt.Fprintf(os.Stderr, "launch: %v\n", err)
		return 1
	}
	fmt.Fprintf(os.Stderr, "launch: listening on %s for up to %s\n", address, waitLimit)
	conn, err := listener.Accept()
	_ = listener.Close()
	if err != nil {
		fmt.Fprintf(os.Stderr, "launch: no connection within %s: %v\n", waitLimit, err)
		return 1
	}
	defer conn.Close()
	session := &connection{conn: conn}
	input := bufio.NewReader(conn)

	manifest, err := readStart(input)
	if err != nil {
		session.writeJSON("log", err.Error())
		session.writeJSON("failed", "start")
		fmt.Fprintf(os.Stderr, "launch: %v\n", err)
		return 1
	}
	if failed, ok := runSteps(paths, func(line string) { session.writeJSON("log", line) }); !ok {
		session.writeJSON("failed", failed)
		fmt.Fprintf(os.Stderr, "launch: failed: %s\n", failed)
		return 1
	}
	return relay(paths, session, input, manifest)
}

// readStart reads the first line of the session, {"env":"<manifest path from the repository root>"}.
func readStart(input *bufio.Reader) (string, error) {
	line, err := input.ReadBytes('\n')
	if err != nil && !(errors.Is(err, io.EOF) && len(line) != 0) {
		return "", fmt.Errorf("no first line: %v", err)
	}
	var start struct {
		Env string `json:"env"`
	}
	decoder := json.NewDecoder(strings.NewReader(string(line)))
	decoder.DisallowUnknownFields()
	if err := decoder.Decode(&start); err != nil || start.Env == "" {
		return "", fmt.Errorf(`the first line is not {"env":"<manifest>"}: %q`, strings.TrimRight(string(line), "\r\n"))
	}
	return start.Env, nil
}

// relay runs oracle serve: lines from the connection go to its standard input, its standard output goes
// to the connection unchanged, each line of its standard error as a log line.
func relay(paths layout, session *connection, input *bufio.Reader, manifest string) int {
	oracle := exec.Command(paths.binary, "serve", "--env", manifest)
	oracle.Dir = paths.root
	stdin, err := oracle.StdinPipe()
	if err != nil {
		return failStart(session, err)
	}
	stdout, err := oracle.StdoutPipe()
	if err != nil {
		return failStart(session, err)
	}
	stderr, err := oracle.StderrPipe()
	if err != nil {
		return failStart(session, err)
	}
	if err := oracle.Start(); err != nil {
		return failStart(session, err)
	}

	go func() {
		// The end of the connection ends the standard input of the oracle.
		_, _ = io.Copy(stdin, input)
		_ = stdin.Close()
	}()
	var relays sync.WaitGroup
	relays.Add(2)
	go func() {
		defer relays.Done()
		forEachLine(stdout, func(line []byte) { session.writeLine(line) })
	}()
	go func() {
		defer relays.Done()
		forEachLine(stderr, func(line []byte) { session.writeJSON("log", string(line)) })
	}()
	relays.Wait()
	err = oracle.Wait()
	if err != nil && !isExitError(err) {
		fmt.Fprintf(os.Stderr, "launch: %v\n", err)
	}
	return exitCodeOf(err)
}

func failStart(session *connection, err error) int {
	session.writeJSON("log", "cannot start the oracle: "+err.Error())
	session.writeJSON("failed", "oracle serve")
	fmt.Fprintf(os.Stderr, "launch: cannot start the oracle: %v\n", err)
	return 1
}

// forEachLine calls handle for every line of reader, without its line end; lines may be of any length.
func forEachLine(reader io.Reader, handle func([]byte)) {
	buffered := bufio.NewReader(reader)
	for {
		line, err := buffered.ReadBytes('\n')
		if len(line) != 0 {
			line = bytes.TrimSuffix(line, []byte("\n"))
			handle(bytes.TrimSuffix(line, []byte("\r")))
		}
		if err != nil {
			return
		}
	}
}
