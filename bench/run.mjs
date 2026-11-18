#!/usr/bin/env node
// Runner of the benchmark rig: builds and runs SpeedBench with the .NET SDK on the machine, then builds the summary.
// The workflow .github/workflows/bench.yml starts it on a GitHub-hosted runner (docs/design/eng-bench.md).
//
//   node bench/run.mjs                    verify, then one measurement run of all cases (Job.Default)
//   node bench/run.mjs --runs 3           verify, then three separate measurement runs one after another
//   node bench/run.mjs --verify-only      only the verification
//   node bench/run.mjs --job Dry          another BenchmarkDotNet job (Dry: one call per case, a check without measurement)
//   node bench/run.mjs --cases E1,E5      only these cases
//   node bench/run.mjs --filter '*Evaluate*'   BenchmarkDotNet filter (default '*')
//   node bench/run.mjs --summary-only     rebuild the summaries from saved runs
//
// Results go to bench/results/ (ignored by git): results/runs/<k>/ per run, results/summary.md over all runs.
// No npm dependencies.

import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const project = join('SpeedBench', 'SpeedBench.csproj');
const results = join(here, 'results');
const runsDir = join(results, 'runs');
const verifyJson = join(results, 'verify.json');
const summaryMd = join(results, 'summary.md');
const THRESHOLD = 1.1;
// A row has at least MIN_RUNS runs; a row whose runs lie on both sides of the threshold is marked at MAX_RUNS runs.
const MIN_RUNS = 3;
const MAX_RUNS = 5;
// The library whose rows get the closing block of the summary. BENCH_SUBJECT names another one, only to check the summary on saved runs.
const SUBJECT = process.env.BENCH_SUBJECT ?? 'GreatPartsCel';

const args = process.argv.slice(2);
const flag = (name) => args.includes(name);
const option = (name) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : undefined;
};

// ---------- the .NET SDK ----------

// Runs dotnet in the directory of the rig; returns the exit code.
function dotnet(command, env = {}) {
  console.log(`> dotnet ${command.join(' ')}`);
  const r = spawnSync('dotnet', command, { stdio: 'inherit', cwd: here, env: { ...process.env, ...env } });
  if (r.error) {
    console.error(`cannot start dotnet: ${r.error.message}`);
    process.exit(1);
  }
  return r.status ?? 1;
}

function dotnetOrExit(command, env = {}) {
  const status = dotnet(command, env);
  if (status !== 0) {
    console.error(`dotnet exited with code ${status}`);
    process.exit(status);
  }
}

const run = (...rest) => ['run', '-c', 'Release', '--no-build', '--project', 'SpeedBench', '-f', 'net10.0', '--', ...rest];

// ---------- formatting ----------

function fmtTime(ns) {
  if (ns === undefined || ns === null || Number.isNaN(ns)) return '-';
  if (Math.abs(ns) >= 1e6) return `${(ns / 1e6).toFixed(2)} ms`;
  if (Math.abs(ns) >= 1e3) return `${(ns / 1e3).toFixed(2)} us`;
  return `${ns.toFixed(1)} ns`;
}

function fmtBytes(b) {
  if (b === undefined || b === null || Number.isNaN(b)) return '-';
  if (Math.abs(b) >= 1024 * 1024) return `${(b / 1024 / 1024).toFixed(2)} MB`;
  if (Math.abs(b) >= 1024) return `${(b / 1024).toFixed(2)} KB`;
  return `${b} B`;
}

const esc = (s) => s.replaceAll('|', '\\|');
const cut = (s, n = 80) => (s.length > n ? `${s.slice(0, n)}...` : s);

function median(values) {
  const v = [...values].sort((a, b) => a - b);
  const m = Math.floor(v.length / 2);
  return v.length % 2 === 1 ? v[m] : (v[m - 1] + v[m]) / 2;
}

// ---------- loading ----------

// [BenchmarkDotNet method, label, whether the cost of text creation is subtracted]
const OPERATIONS = [
  ['Compile', 'compile', true],
  ['Evaluate', 'evaluate', false],
  ['FullCycle', 'full cycle', true],
];

function loadVerify() {
  return existsSync(verifyJson) ? JSON.parse(readFileSync(verifyJson, 'utf8')) : null;
}

function runNumbers() {
  if (!existsSync(runsDir)) return [];
  return readdirSync(runsDir).filter((d) => /^\d+$/.test(d)).map(Number).sort((a, b) => a - b);
}

function loadRunMeta(k) {
  const f = join(runsDir, String(k), 'run.json');
  return existsSync(f) ? JSON.parse(readFileSync(f, 'utf8')) : { run: k };
}

function loadBdn(bdnDir) {
  const dir = join(bdnDir, 'results');
  if (!existsSync(dir)) return null;
  const out = { host: null, ops: [], text: [] };
  for (const f of readdirSync(dir)) {
    if (!f.endsWith('-report-full.json')) continue;
    const j = JSON.parse(readFileSync(join(dir, f), 'utf8'));
    out.host = out.host ?? j.HostEnvironmentInfo;
    for (const b of j.Benchmarks) {
      const row = {
        method: b.Method,
        param: /(?:Target|Case)=([^\s,]+)/.exec(b.Parameters)?.[1] ?? b.Parameters,
        median: b.Statistics?.Median,
        alloc: b.Memory?.BytesAllocatedPerOperation,
      };
      if (b.Type === 'TextBench') out.text.push(row);
      else out.ops.push(row);
    }
  }
  return out;
}

// One run: per scenario the text row and, per operation, the measured libraries with the ratio
// to the fastest correct library of the same run (decisive column: minus text creation).
function measure(bdn, verify) {
  const scenarios = [...new Set([...bdn.text.map((r) => r.param), ...bdn.ops.map((r) => r.param.split(':')[1])])];
  const order = (verify ?? []).map((r) => r.Scenario);
  scenarios.sort((a, b) => order.indexOf(a) - order.indexOf(b));
  const libraries = [...new Set((verify ?? []).map((r) => r.Library))];
  return scenarios.map((s) => {
    const text = bdn.text.find((r) => r.param === s);
    const operations = [];
    for (const [method, label, withText] of OPERATIONS) {
      const measured = libraries
        .map((l) => ({ l, r: bdn.ops.find((x) => x.method === method && x.param === `${l}:${s}`) }))
        .filter((x) => x.r)
        .map(({ l, r }) => ({
          l,
          raw: r.median,
          rawAlloc: r.alloc,
          time: withText ? r.median - (text?.median ?? NaN) : r.median,
          alloc: withText ? r.alloc - (text?.alloc ?? NaN) : r.alloc,
        }));
      if (measured.length === 0) continue;
      const fastest = Math.min(...measured.map((m) => m.time));
      const fastestLib = measured.find((m) => m.time === fastest)?.l ?? '?';
      for (const m of measured) {
        m.ratio = m.time / fastest;
        m.fastestLib = fastestLib;
      }
      operations.push({ method, label, withText, measured });
    }
    return { s, text, operations };
  });
}

// ---------- summary ----------

// Versions of the packages of the rig, from Directory.Packages.props.
function packageVersions() {
  const text = readFileSync(join(here, 'Directory.Packages.props'), 'utf8');
  return [...text.matchAll(/<PackageVersion\s+Include="([^"]+)"\s+Version="([^"]+)"/g)].map((m) => `${m[1]} ${m[2]}`);
}

function conditionsSection() {
  const e = process.env;
  const lines = ['## Conditions\n'];
  lines.push(`- Date of the summary: ${new Date().toISOString()}`);
  lines.push(`- Commit: ${e.GITHUB_SHA ?? 'not a GitHub Actions run'}`);
  if (e.GITHUB_SERVER_URL && e.GITHUB_REPOSITORY && e.GITHUB_RUN_ID) {
    lines.push(`- Run: ${e.GITHUB_SERVER_URL}/${e.GITHUB_REPOSITORY}/actions/runs/${e.GITHUB_RUN_ID}`);
  }
  lines.push(`- Runner: ${e.RUNNER_OS ?? '?'} ${e.RUNNER_ARCH ?? '?'}, image ${e.ImageOS ?? '?'} ${e.ImageVersion ?? '?'}`);
  lines.push(`- Packages: ${packageVersions().join('; ')}\n`);
  return lines.join('\n');
}

function verifySection(verify) {
  const libraries = [...new Set(verify.map((r) => r.Library))];
  const scenarios = [...new Set(verify.map((r) => r.Scenario))];
  const lines = ['## Verification\n'];
  lines.push('Correct: all three variants gave the expected value: the original text (compile and evaluate), the text with a tail (compile and evaluate), the text with a tail (full cycle).\n');
  lines.push(`| Scenario | Expression | Lines | Expected | ${libraries.join(' | ')} |`);
  lines.push(`|---|---|--:|---|${libraries.map(() => '---').join('|')}|`);
  for (const s of scenarios) {
    const rows = verify.filter((r) => r.Scenario === s);
    const first = rows[0];
    const cells = libraries.map((l) => {
      const r = rows.find((x) => x.Library === l);
      if (!r) return '-';
      if (r.Correct) return 'correct';
      const wrong = r.Variants.filter((v) => !v.Correct);
      return esc(cut(`wrong (${wrong.length} of 3): ${wrong[0].Result}`));
    });
    lines.push(`| ${s} | \`${esc(first.Expression)}\` | ${first.Lines} | ${esc(cut(first.Expected, 40))} | ${cells.join(' | ')} |`);
  }
  const excluded = verify.filter((r) => !r.Correct);
  lines.push('');
  lines.push(excluded.length === 0
    ? 'No library is excluded: every library gave the expected value in every scenario.'
    : `Excluded from the measurement (wrong result): ${excluded.map((r) => `${r.Library} - ${r.Scenario}`).join('; ')}.`);
  lines.push('');
  return lines.join('\n');
}

function environmentSection(host) {
  const h = host ?? {};
  const lines = ['## Measurement environment\n'];
  lines.push(`- BenchmarkDotNet ${h.BenchmarkDotNetVersion ?? '?'}; OS: ${h.OsVersion ?? '?'}; processor: ${h.ProcessorName ?? '?'}`);
  lines.push(`- Cores: ${h.PhysicalCoreCount ?? '?'} physical, ${h.LogicalCoreCount ?? '?'} logical; architecture: ${h.Architecture ?? '?'}`);
  lines.push(`- Runtime: ${h.RuntimeVersion ?? '?'}; SDK: ${h.DotNetCliVersion ?? '?'}; configuration: ${h.Configuration ?? '?'}\n`);
  return lines.join('\n');
}

const runLine = (m) =>
  `- Run ${m.run}: ${m.started ?? '?'} - ${m.finished ?? '?'}; job: ${m.job ?? 'Default'}; cases: ${m.cases ?? 'all'}; filter: ${m.filter ?? '*'}`;

// Table of one run (the per-run summary, results/runs/<k>/summary.md).
function runSection(k, bdn, verify) {
  const meta = loadRunMeta(k);
  const lines = [`## Run ${k}: scenario x operation\n`, runLine(meta), ''];
  lines.push('Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column; "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.\n');
  lines.push('| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |');
  lines.push('|---|---|---|--:|--:|--:|--:|--:|---|');
  for (const { s, text, operations } of measure(bdn, verify)) {
    lines.push(`| ${s} | text creation | - | - | ${fmtTime(text?.median)} | - | ${fmtBytes(text?.alloc)} | - | |`);
    for (const { label, withText, measured } of operations) {
      for (const m of measured) {
        lines.push(`| ${s} | ${label} | ${m.l} | ${fmtTime(m.time)} | ${withText ? fmtTime(m.raw) : '-'} | ${fmtBytes(m.alloc)} | ${withText ? fmtBytes(m.rawAlloc) : '-'} | ${m.ratio.toFixed(2)} | ${m.ratio > THRESHOLD ? 'yes' : ''} |`);
      }
      for (const r of (verify ?? []).filter((x) => x.Scenario === s && !x.Correct)) {
        lines.push(`| ${s} | ${label} | ${r.Library} | - | - | - | - | excluded | |`);
      }
    }
  }
  lines.push('');
  return lines.join('\n');
}

// Summary over all runs: per row "scenario, operation, library" the ratios of the runs that measured it, R = median of the ratios,
// spread = max / min ratio, both sides = some ratio <= 1.1 and some > 1.1.
function overRunsSection(runs, verify) {
  const rows = new Map();
  for (const { k, bdn } of runs) {
    for (const { s, operations } of measure(bdn, verify)) {
      for (const { label, measured } of operations) {
        for (const m of measured) {
          const key = `${s}|${label}|${m.l}`;
          if (!rows.has(key)) rows.set(key, { s, label, l: m.l, per: [] });
          rows.get(key).per.push({ k, ratio: m.ratio, time: m.time, fastestLib: m.fastestLib });
        }
      }
    }
  }
  const lines = ['## Summary over the runs\n'];
  lines.push(`The ratio of a row in a run is to the fastest library with a correct result in the same run. R is the median of the ratios over the runs; the column "R > 1.1" is filled when the row has ${MIN_RUNS} runs or more; "both sides" means that the ratio is not above 1.1 in some runs and above it in others.\n`);
  lines.push('| Scenario | Operation | Library | Runs | Ratios by run | R | Spread | Fastest by run | Median time over the runs | R > 1.1 | Both sides of 1.1 |');
  lines.push('|---|---|---|--:|---|--:|--:|---|--:|---|---|');
  const over = [];
  const subject = { seen: false, over: [], both: [], short: [] };
  for (const r of rows.values()) {
    const ratios = r.per.map((p) => p.ratio);
    const R = median(ratios);
    // Non-positive ratios only come from a job without a real measurement (Dry): no spread then.
    const spread = ratios.every((x) => x > 0) ? Math.max(...ratios) / Math.min(...ratios) : NaN;
    const both = ratios.some((x) => x <= THRESHOLD) && ratios.some((x) => x > THRESHOLD);
    const enough = ratios.length >= MIN_RUNS;
    const fastest = [...new Set(r.per.map((p) => p.fastestLib))].join(', ');
    const where = `${r.s}, ${r.label}, ${r.l}`;
    if (enough && R > THRESHOLD) over.push(`${where}: ${R.toFixed(2)}`);
    if (r.l === SUBJECT) {
      subject.seen = true;
      if (!enough) subject.short.push(`${where} (${ratios.length})`);
      else {
        if (R > THRESHOLD) subject.over.push(`${where}: ${R.toFixed(2)}`);
        if (both) subject.both.push(`${where}: ${R.toFixed(2)}`);
      }
    }
    const sideMark = !both ? '' : ratios.length >= MAX_RUNS ? 'yes, at the threshold' : 'yes';
    const flagOver = !enough ? `no result (runs: ${ratios.length})` : R > THRESHOLD ? 'yes' : 'no';
    lines.push(`| ${r.s} | ${r.label} | ${r.l} | ${ratios.length} | ${r.per.map((p) => `${p.k}: ${p.ratio.toFixed(2)}`).join('; ')} | ${R.toFixed(2)} | ${Number.isNaN(spread) ? '-' : spread.toFixed(2)} | ${fastest} | ${fmtTime(median(r.per.map((p) => p.time)))} | ${flagOver} | ${sideMark} |`);
  }
  lines.push('');
  lines.push(over.length === 0 ? 'R above 1.1 (all libraries): none.' : `R above 1.1 (all libraries): ${over.join('; ')}.`);
  lines.push('');
  if (!subject.seen) {
    lines.push(`No rows of ${SUBJECT} in the measurement.`);
  } else {
    lines.push(`**Rows of ${SUBJECT}.**\n`);
    lines.push(subject.over.length === 0 ? '- R above 1.1: none.' : `- R above 1.1: ${subject.over.join('; ')}.`);
    lines.push(subject.both.length === 0 ? '- Ratios on both sides of 1.1: none.' : `- Ratios on both sides of 1.1: ${subject.both.join('; ')}.`);
    if (subject.short.length > 0) lines.push(`- Fewer than ${MIN_RUNS} runs, no R above 1.1 is stated: ${subject.short.join('; ')}.`);
  }
  lines.push('');
  return lines.join('\n');
}

function writeSummary() {
  const verify = loadVerify();
  const runs = runNumbers()
    .map((k) => ({ k, bdn: loadBdn(join(runsDir, String(k), 'bdn')) }))
    .filter((r) => r.bdn);
  for (const { k, bdn } of runs) {
    writeFileSync(join(runsDir, String(k), 'summary.md'), runSection(k, bdn, verify));
  }
  const parts = [conditionsSection()];
  if (verify) parts.push(verifySection(verify));
  if (runs.length > 0) {
    parts.push(environmentSection(runs[0].bdn.host));
    parts.push(['## Runs\n', ...runs.map(({ k }) => runLine(loadRunMeta(k))), '', 'The table of each run is `results/runs/<k>/summary.md`.\n'].join('\n'));
    parts.push(overRunsSection(runs, verify));
  }
  const text = parts.join('\n');
  mkdirSync(results, { recursive: true });
  writeFileSync(summaryMd, text);
  console.log(`\nSummary written to ${summaryMd}\n`);
  console.log(text);
}

// ---------- main ----------

if (flag('--summary-only')) {
  writeSummary();
  process.exit(0);
}

const runsOption = option('--runs') ?? '1';
if (!/^[1-9]\d*$/.test(runsOption)) {
  console.error(`--runs needs a positive integer, got '${runsOption}'`);
  process.exit(2);
}

mkdirSync(results, { recursive: true });
dotnetOrExit(['build', project, '-c', 'Release', '-f', 'net10.0', '--nologo', '-v', 'q']);
dotnetOrExit(run('verify', 'results/verify.json'));

let failure = 0;
if (!flag('--verify-only')) {
  if (existsSync(runsDir)) rmSync(runsDir, { recursive: true, force: true });
  for (let k = 1; k <= Number(runsOption) && failure === 0; k++) {
    const dir = join(runsDir, String(k));
    mkdirSync(dir, { recursive: true });
    const meta = { run: k, job: option('--job') ?? 'Default', cases: option('--cases') ?? null, filter: option('--filter') ?? '*', started: new Date().toISOString() };
    const bdnArgs = ['bench', '--filter', meta.filter, '--artifacts', `results/runs/${k}/bdn`];
    if (option('--job')) bdnArgs.push('--job', option('--job'));
    failure = dotnet(run(...bdnArgs), option('--cases') ? { BENCH_CASES: option('--cases') } : {});
    meta.finished = new Date().toISOString();
    writeFileSync(join(dir, 'run.json'), `${JSON.stringify(meta, null, 2)}\n`);
  }
}

// The summary is written after a failed run too: the runs that ended are in it.
writeSummary();
if (failure !== 0) {
  console.error(`dotnet exited with code ${failure}`);
  process.exit(failure);
}
