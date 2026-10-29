package main

import (
	"encoding/json"
	"fmt"
	"os"

	"cel.dev/cel-go/cel"
	"cel.dev/cel-go/common/env"
	"cel.dev/cel-go/ext"
)

// refusedKeys are top-level keys of a manifest that the oracle refuses: cel-go would ignore
// greatPartsCel silently and read validators differently from the library.
var refusedKeys = []string{"greatPartsCel", "validators"}

// loadEnvironment builds the cel-go environment from a run manifest (docs/design/eng-oracle.md,
// "Environment from the manifest").
func loadEnvironment(path string) (*cel.Env, error) {
	data, err := os.ReadFile(path)
	if err != nil {
		return nil, fmt.Errorf("cannot read the manifest: %v", err)
	}
	var top map[string]json.RawMessage
	if err := json.Unmarshal(data, &top); err != nil {
		return nil, fmt.Errorf("manifest %s is not a JSON object: %v", path, err)
	}
	for _, key := range refusedKeys {
		if _, found := top[key]; found {
			return nil, fmt.Errorf("manifest %s is refused: it has the key %q", path, key)
		}
	}
	config, err := env.ConfigFromYAML(data)
	if err != nil {
		return nil, fmt.Errorf("manifest %s: %v", path, err)
	}
	environment, err := cel.NewCustomEnv(
		cel.FromConfig(config, ext.ExtensionOptionFactory, networkFactory),
		// The environment flags of the cel-go conformance runner (stdOpts of conformance_test.go, v0.32.0).
		cel.EagerlyValidateDeclarations(true),
		cel.EnableErrorOnBadPresenceTest(true),
		cel.EnableIdentifierEscapeSyntax(),
		// A constant pattern that fails to compile is a compile error, as in the library.
		cel.ASTValidators(cel.ValidateRegexLiterals()))
	if err != nil {
		return nil, fmt.Errorf("manifest %s: %v", path, err)
	}
	return environment, nil
}

// networkFactory adds the network extension set, which ext.ExtensionOptionFactory does not know.
func networkFactory(element any) (cel.EnvOption, bool) {
	extension, ok := element.(*env.Extension)
	if !ok || extension.Name != "network" {
		return nil, false
	}
	// FromConfig validates the version of every extension before it calls the factories.
	version, err := extension.VersionNumber()
	if err != nil {
		return nil, false
	}
	return ext.Network(ext.NetworkVersion(version)), true
}
