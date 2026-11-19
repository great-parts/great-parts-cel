// The gate of the publish of the Blazor WebAssembly application (the job wasm of ci.yml): the log of the publish has to hold no line with
// a warning IL2xxx or IL3xxx, which are the warnings of the trimmer and of the AOT analysis. The job runs the gate on the real publish and,
// as a control example, on the publish of the same application with one intentional warning (tests/Great.Parts.Cel.Wasm.App/IlControl),
// where the gate has to turn red.
//
// Usage: node .github/scripts/il-gate.mjs <log of the publish>
// Exit code: 0 when the log has no such line, 1 when it has, 2 when the log does not exist.
import { existsSync, readFileSync } from 'node:fs';

const shown = 20;
const log = process.argv[2];
if (!log || !existsSync(log)) {
  console.error(`il-gate: the log ${log ?? '(none given)'} does not exist`);
  process.exit(2);
}

const lines = readFileSync(log, 'utf8')
  .split(/\r?\n/)
  .filter((line) => /IL[23][0-9]{3}/.test(line));

console.log(`warnings IL2xxx and IL3xxx in the publish: ${lines.length}`);
for (const line of lines.slice(0, shown)) {
  console.log(line);
}

process.exit(lines.length === 0 ? 0 : 1);
