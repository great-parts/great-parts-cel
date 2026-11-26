#!/usr/bin/env node
// Builds the report of a run of bench.yml for the branch `reports` (docs/design/eng-release.md, "Reports of the runs").
//
//   node .github/scripts/bench-report.mjs --results bench/results --output <directory> [--runs <n>] [--job <job>] [--runtime jit|aot] [--cases <ids>]
//
// The runtime (jit, the default, or aot: Native AOT) is in the title of report.md and in the inputs of report.json.
//
// The directory gets <stamp>/ with report.json (the date and the time of the report in UTC, the commit, the run, the inputs, the
// runs), report.md (the summary of the rig, with the column Allocated of the table over the runs), verify.json, thresholds.json (the
// verdicts on the limits of the speed-ups and the diagnostics without a verdict, when the run measured them; report.json lists the
// limit and the verdict of each) and runs/<k>/ with run.json and summary.md; the stamp is yyyy-MM-ddTHHmmssZ. The full reports of BenchmarkDotNet stay in the artifact bench-results of the run. The workflow
// publish-report.yml writes the directory to the branch. No npm dependencies.

import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const args = process.argv.slice(2);
const option = (name) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : undefined;
};

const resultsPath = option('--results');
const outputPath = option('--output');
if (!resultsPath || !outputPath) {
  console.error('Usage: bench-report.mjs --results <directory> --output <directory> [--runs <n>] [--job <job>] [--runtime jit|aot] [--cases <ids>]');
  process.exit(2);
}

const runtime = option('--runtime') ?? 'jit';
if (runtime !== 'jit' && runtime !== 'aot') {
  console.error(`--runtime is jit or aot, got '${runtime}'`);
  process.exit(2);
}
const runtimeTitle = runtime === 'aot' ? 'Native AOT on .NET 11' : 'JIT on .NET 11';

const summaryPath = join(resultsPath, 'summary.md');
const verifyPath = join(resultsPath, 'verify.json');
const runsPath = join(resultsPath, 'runs');
for (const required of [summaryPath, verifyPath, runsPath]) {
  if (!existsSync(required)) {
    console.error(`The result of the rig is incomplete: ${required} is missing.`);
    process.exit(1);
  }
}

const runNumbers = readdirSync(runsPath).filter((d) => /^\d+$/.test(d)).map(Number).sort((a, b) => a - b);
if (runNumbers.length === 0) {
  console.error('The rig has no run.');
  process.exit(1);
}
const runs = runNumbers.map((k) => JSON.parse(readFileSync(join(runsPath, String(k), 'run.json'), 'utf8')));

const e = process.env;
const server = e.GITHUB_SERVER_URL ?? 'https://github.com';
const repository = e.GITHUB_REPOSITORY ?? 'great-parts/great-parts-cel';
const run = e.GITHUB_RUN_ID
  ? { id: Number(e.GITHUB_RUN_ID), attempt: Number(e.GITHUB_RUN_ATTEMPT ?? 1), event: e.GITHUB_EVENT_NAME ?? null, url: `${server}/${repository}/actions/runs/${e.GITHUB_RUN_ID}` }
  : null;
const commit = e.GITHUB_SHA ?? null;

const generatedAt = new Date().toISOString().slice(0, 19) + 'Z';
const stamp = `${generatedAt.slice(0, 10)}T${generatedAt.slice(11, 19).replaceAll(':', '')}Z`;

const thresholdsPath = join(resultsPath, 'thresholds', 'thresholds.json');
const thresholds = existsSync(thresholdsPath) ? JSON.parse(readFileSync(thresholdsPath, 'utf8')) : null;

const report = {
  schema: 1,
  kind: 'bench',
  generatedAtUtc: generatedAt,
  greatPartsCelCommit: commit,
  inputs: { runs: option('--runs') ? Number(option('--runs')) : null, job: option('--job') ?? null, runtime, cases: option('--cases') || null },
  runs,
  thresholds: thresholds ? { job: thresholds.job, verdicts: thresholds.verdicts.map((v) => ({ limit: v.limit, verdict: v.verdict })) } : null,
  files: {
    summary: 'report.md',
    verification: 'verify.json',
    thresholds: thresholds ? 'thresholds.json' : null,
    runs: 'runs/<k>/run.json and runs/<k>/summary.md',
  },
  run,
};

const header = [
  `# Benchmark rig: ${runtimeTitle}`,
  '',
  `- Date and time of the report (UTC): ${generatedAt.replace('T', ' ').replace('Z', '')}`,
  commit ? `- Commit of ${repository}: [\`${commit}\`](${server}/${repository}/commit/${commit})` : '- Commit of the repository: not a GitHub Actions run',
  `- Runs: ${runs.length}`,
  run ? `- Run: ${run.url} (GitHub keeps a run for 90 days; this report does not depend on it)` : '- Run: not a GitHub Actions run',
  '',
  'The summary of the rig follows. The full reports of BenchmarkDotNet are in the artifact `bench-results` of the run, for 90 days.',
  '',
  '',
].join('\n');

const directory = join(outputPath, stamp);
mkdirSync(join(directory, 'runs'), { recursive: true });
writeFileSync(join(directory, 'report.json'), JSON.stringify(report, null, 2) + '\n');
writeFileSync(join(directory, 'report.md'), header + readFileSync(summaryPath, 'utf8'));
cpSync(verifyPath, join(directory, 'verify.json'));
if (thresholds) cpSync(thresholdsPath, join(directory, 'thresholds.json'));
for (const k of runNumbers) {
  const target = join(directory, 'runs', String(k));
  mkdirSync(target, { recursive: true });
  cpSync(join(runsPath, String(k), 'run.json'), join(target, 'run.json'));
  if (existsSync(join(runsPath, String(k), 'summary.md'))) cpSync(join(runsPath, String(k), 'summary.md'), join(target, 'summary.md'));
}
console.log(`Report ${stamp}: ${runs.length} runs`);
