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
//   node bench/run.mjs --no-thresholds    without the measurement of the limits of the speed-ups
//   node bench/run.mjs --thresholds-only  only the measurement of the limits (no verification, no cases of the rig)
//
// After the runs of the rig the script measures the limits of the speed-ups of the library (bench/ThresholdBench): the search of a
// substring around 64 code units, the build of the automaton of the pattern E5, distinct and sets around the length 24 of a list.
// The job (--job) is the one of the rig. The verdicts are in the section "Limits of the speed-ups" of the summary.
//
// Results go to bench/results/ (ignored by git): results/runs/<k>/ per run, results/summary.md over all runs,
// results/thresholds/ (the reports of BenchmarkDotNet, meta.json and thresholds.json with the verdicts).
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
const thresholdProject = join('ThresholdBench', 'Great.Parts.Cel.ThresholdBench.csproj');
const thresholdsDir = join(results, 'thresholds');
const thresholdsJson = join(thresholdsDir, 'thresholds.json');
const THRESHOLD = 1.1;
// A row has at least MIN_RUNS runs; a row whose runs lie on both sides of the threshold is marked at MAX_RUNS runs.
const MIN_RUNS = 3;
const MAX_RUNS = 5;
// The library whose rows get the closing block of the summary. BENCH_SUBJECT names another one, only to check the summary on saved runs.
// Its variants have short names (BenchmarkDotNet cuts a parameter "library:case" at 20 characters): GreatPartsCel is the variant with
// bindings, GPCel-Input the input of the environment, GPCel-Simple the simple entry.
const SUBJECT = process.env.BENCH_SUBJECT ?? 'GreatPartsCel';
const SUBJECT_VARIANTS = process.env.BENCH_SUBJECT ? [SUBJECT] : [SUBJECT, 'GPCel-Input', 'GPCel-Simple'];
const isSubject = (library) => SUBJECT_VARIANTS.includes(library);
// Limits of the speed-ups that the verdicts of the thresholds check.
const SEARCH_LIMIT = 64;
const AUTOMATON_LIMIT_NS = 1000;
const HASH_LIMIT = 24;

const args = process.argv.slice(2);
const flag = (name) => args.includes(name);
const option = (name) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : undefined;
};

// ---------- the .NET SDK ----------

// The words that start dotnet: BENCH_DOTNET replaces "dotnet" for a check inside a container that has the SDK, for example
// BENCH_DOTNET="docker exec -w /repo/bench <container> dotnet". The paths of the arguments are relative to bench/, which the container
// mounts, so the results appear on the machine; the environment variable BENCH_CASES does not reach the container, so --cases is not
// for this form. The workflow does not set it.
const dotnetWords = (process.env.BENCH_DOTNET ?? 'dotnet').split(/\s+/).filter(Boolean);

// Runs dotnet in the directory of the rig; returns the exit code.
function dotnet(command, env = {}) {
  console.log(`> ${dotnetWords.join(' ')} ${command.join(' ')}`);
  const r = spawnSync(dotnetWords[0], [...dotnetWords.slice(1), ...command], { stdio: 'inherit', cwd: here, env: { ...process.env, ...env } });
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
const runThresholds = (...rest) => ['run', '-c', 'Release', '--no-build', '--project', 'ThresholdBench', '-f', 'net10.0', '--', ...rest];

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
      // A variant of the subject is compared with the other libraries and with itself, not with its own other variants.
      for (const m of measured) {
        const pool = isSubject(m.l) ? measured.filter((x) => !isSubject(x.l) || x === m) : measured;
        const fastest = Math.min(...pool.map((x) => x.time));
        m.ratio = m.time / fastest;
        m.fastestLib = pool.find((x) => x.time === fastest)?.l ?? '?';
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
  lines.push(`- Great.Parts.Cel: built from the sources of this commit`);
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
  lines.push('Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column (for a variant of ${SUBJECT}: to the fastest other library or to itself); "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.\n');
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
  lines.push(`The ratio of a row in a run is to the fastest library with a correct result in the same run (for a variant of ${SUBJECT}: to the fastest other library or to itself, not to its other variants). R is the median of the ratios over the runs; the column "R > 1.1" is filled when the row has ${MIN_RUNS} runs or more; "both sides" means that the ratio is not above 1.1 in some runs and above it in others.\n`);
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
    if (isSubject(r.l)) {
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

// ---------- the limits of the speed-ups ----------

// The rows of the full reports of BenchmarkDotNet of ThresholdBench: class, method, parameters, median (ns), allocated bytes.
function loadThresholdRows() {
  const dir = join(thresholdsDir, 'bdn', 'results');
  if (!existsSync(dir)) return null;
  const rows = [];
  for (const f of readdirSync(dir)) {
    if (!f.endsWith('-report-full.json')) continue;
    const j = JSON.parse(readFileSync(join(dir, f), 'utf8'));
    for (const b of j.Benchmarks) {
      const parameters = {};
      for (const m of (b.Parameters ?? '').matchAll(/(\w+)=([^,]+)/g)) parameters[m[1]] = m[2].trim();
      rows.push({ type: b.Type, method: b.Method, parameters, median: b.Statistics?.Median, alloc: b.Memory?.BytesAllocatedPerOperation });
    }
  }
  return rows;
}

function loadThresholdMeta() {
  const f = join(thresholdsDir, 'meta.json');
  return existsSync(f) ? JSON.parse(readFileSync(f, 'utf8')) : { job: 'Default' };
}

const times = (x) => (x === undefined || x === null || !(x > 0) ? undefined : x);

// The three limits, each with its measurement, its rule and its verdict. A Dry job has no measurement and no verdict.
function thresholdVerdicts(rows, meta) {
  const dry = meta.job === 'Dry';
  const at = (type, method, key, value) => times(rows.find((r) => r.type === type && r.method === method && r.parameters[key] === String(value))?.median);
  const verdicts = [];

  const search = { limit: `hybrid search of a substring, ${SEARCH_LIMIT} code units`, rule: `.NET at M = ${SEARCH_LIMIT} is not slower than Knuth-Morris-Pratt; otherwise the limit is 32`, rows: [] };
  for (const m of [48, 64, 65, 80]) {
    search.rows.push({ m, kmp: at('SubstringBenchmarks', 'Kmp', 'M', m), net: at('SubstringBenchmarks', 'Net', 'M', m), hybrid: at('SubstringBenchmarks', 'Hybrid', 'M', m) });
  }
  const s64 = search.rows.find((r) => r.m === 64);
  search.verdict = dry ? 'not measured (Dry)' : s64?.kmp === undefined || s64?.net === undefined ? 'no result' : s64.net <= s64.kmp ? `holds: ${SEARCH_LIMIT}` : 'does not hold: 32';
  verdicts.push(search);

  const automaton = { limit: 'build of the automaton of the pattern E5', rule: `the median of the build is at most ${AUTOMATON_LIMIT_NS} ns`, rows: [] };
  for (const name of ['E5', 'UUID']) {
    automaton.rows.push({ name, program: at('AutomatonBenchmarks', 'ParseAndBuildProgram', 'Name', name), automaton: at('AutomatonBenchmarks', 'BuildAutomaton', 'Name', name) });
  }
  const e5 = automaton.rows.find((r) => r.name === 'E5');
  automaton.verdict = dry ? 'not measured (Dry)' : e5?.automaton === undefined ? 'no result' : e5.automaton <= AUTOMATON_LIMIT_NS ? `holds: ${e5.automaton.toFixed(0)} ns` : `does not hold: ${e5.automaton.toFixed(0)} ns`;
  verdicts.push(automaton);

  const hashing = { limit: `index of the values of a list (distinct, sets), ${HASH_LIMIT} values`, rule: `the scan (the library's index at N up to ${HASH_LIMIT}) is not slower than the buckets at N = 8, 16 and ${HASH_LIMIT}, for distinct and for sets.contains`, rows: [], evaluation: [] };
  for (const n of [8, 16, 24, 32, 48]) {
    hashing.rows.push({
      n,
      distinct: { library: at('HashingBenchmarks', 'DistinctDefault', 'N', n), buckets: at('HashingBenchmarks', 'DistinctBuckets', 'N', n), constantHash: at('HashingBenchmarks', 'DistinctConstantHash', 'N', n), pairwise: at('HashingBenchmarks', 'DistinctPairwise', 'N', n) },
      contains: { library: at('HashingBenchmarks', 'ContainsDefault', 'N', n), buckets: at('HashingBenchmarks', 'ContainsBuckets', 'N', n), constantHash: at('HashingBenchmarks', 'ContainsConstantHash', 'N', n), pairwise: at('HashingBenchmarks', 'ContainsPairwise', 'N', n) },
    });
  }
  for (const n of [16, 24, 25, 32, 48]) {
    hashing.evaluation.push({ n, distinct: at('HashingEvaluationBenchmarks', 'Distinct', 'N', n), contains: at('HashingEvaluationBenchmarks', 'SetsContains', 'N', n) });
  }
  const scanRows = hashing.rows.filter((r) => r.n <= HASH_LIMIT);
  const slower = [];
  let missing = false;
  for (const r of scanRows) {
    for (const op of ['distinct', 'contains']) {
      const { library, buckets } = r[op];
      if (library === undefined || buckets === undefined) missing = true;
      else if (library > buckets) slower.push(`${op} N = ${r.n}: ${(library / buckets).toFixed(2)}`);
    }
  }
  hashing.verdict = dry ? 'not measured (Dry)' : missing ? 'no result' : slower.length === 0 ? `holds: ${HASH_LIMIT}` : `does not hold, the scan is slower than the buckets (ratio scan / buckets): ${slower.join('; ')}`;
  verdicts.push(hashing);
  return verdicts;
}

function thresholdsSection() {
  const rows = loadThresholdRows();
  if (!rows) return null;
  const meta = loadThresholdMeta();
  const verdicts = thresholdVerdicts(rows, meta);
  const [search, automaton, hashing] = verdicts;
  const ratio = (a, b) => (a === undefined || b === undefined ? '-' : (a / b).toFixed(2));
  const lines = ['## Limits of the speed-ups\n'];
  lines.push(`Job: ${meta.job ?? 'Default'}; started ${meta.started ?? '?'}, finished ${meta.finished ?? '?'}. Medians of BenchmarkDotNet on the code of this commit; the reports are in \`results/thresholds/bdn/results\`.\n`);
  lines.push('| Limit | Rule | Verdict |');
  lines.push('|---|---|---|');
  for (const v of verdicts) lines.push(`| ${v.limit} | ${v.rule} | ${v.verdict} |`);
  lines.push('');

  lines.push(`### Search of a substring, the text (ab)^5000\n`);
  lines.push('M is the length of the pattern, the slowest case for the search of .NET. "Library" is the search as it runs: .NET up to the limit, Knuth-Morris-Pratt above. Net / KMP below 1 means .NET is faster.\n');
  lines.push('| M | KMP | .NET | Library | Net / KMP |');
  lines.push('|--:|--:|--:|--:|--:|');
  for (const r of search.rows) lines.push(`| ${r.m} | ${fmtTime(r.kmp)} | ${fmtTime(r.net)} | ${fmtTime(r.hybrid)} | ${ratio(r.net, r.kmp)} |`);
  lines.push('');

  lines.push('### Build of the automaton\n');
  lines.push('| Pattern | Parse and program | Automaton |');
  lines.push('|---|--:|--:|');
  for (const r of automaton.rows) lines.push(`| ${r.name} | ${fmtTime(r.program)} | ${fmtTime(r.automaton)} |`);
  lines.push('');

  lines.push('### Index of the values: the lists of N integers\n');
  lines.push('"Library" is the index as the library makes it (the scan up to 24 values, the buckets above); "buckets" forces the buckets at any N; "one hash" gives every value the same hash; "pairwise" are integers of 2^53 and more, compared with each other in order. Scan / buckets below 1 means the scan is faster; at N above 24 the library and the buckets are the same index.\n');
  lines.push('| Operation | N | Library | Buckets | One hash | Pairwise | Scan / buckets |');
  lines.push('|---|--:|--:|--:|--:|--:|--:|');
  for (const op of ['distinct', 'contains']) {
    for (const r of hashing.rows) {
      const x = r[op];
      lines.push(`| ${op === 'distinct' ? 'distinct' : 'sets.contains'} | ${r.n} | ${fmtTime(x.library)} | ${fmtTime(x.buckets)} | ${fmtTime(x.constantHash)} | ${fmtTime(x.pairwise)} | ${r.n <= HASH_LIMIT ? ratio(x.library, x.buckets) : '-'} |`);
    }
  }
  lines.push('');

  lines.push('### distinct and sets.contains through the public API\n');
  lines.push('The evaluation of the program on a list of N integers; the time per element shows a jump at the limit, if there is one.\n');
  lines.push('| N | distinct | per element | sets.contains | per element |');
  lines.push('|--:|--:|--:|--:|--:|');
  const perElement = (x, n) => (x === undefined ? '-' : fmtTime(x / n));
  for (const r of hashing.evaluation) lines.push(`| ${r.n} | ${fmtTime(r.distinct)} | ${perElement(r.distinct, r.n)} | ${fmtTime(r.contains)} | ${perElement(r.contains, r.n)} |`);
  lines.push('');

  mkdirSync(thresholdsDir, { recursive: true });
  writeFileSync(thresholdsJson, `${JSON.stringify({ job: meta.job ?? 'Default', verdicts }, null, 2)}\n`);
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
  const thresholds = thresholdsSection();
  if (thresholds) parts.push(thresholds);
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

const thresholdsOnly = flag('--thresholds-only');
const runsOption = option('--runs') ?? '1';
if (!/^[1-9]\d*$/.test(runsOption)) {
  console.error(`--runs needs a positive integer, got '${runsOption}'`);
  process.exit(2);
}

mkdirSync(results, { recursive: true });
if (!thresholdsOnly) {
  dotnetOrExit(['build', project, '-c', 'Release', '-f', 'net10.0', '--nologo', '-v', 'q']);
  dotnetOrExit(run('verify', 'results/verify.json'));
}

let failure = 0;
if (!flag('--verify-only') && !thresholdsOnly) {
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

// The limits of the speed-ups: after the runs of the rig, in the same job, on the same machine. A failed run of the rig does not skip them.
if (!flag('--verify-only') && !flag('--no-thresholds')) {
  if (existsSync(thresholdsDir)) rmSync(thresholdsDir, { recursive: true, force: true });
  mkdirSync(thresholdsDir, { recursive: true });
  dotnetOrExit(['build', thresholdProject, '-c', 'Release', '-f', 'net10.0', '--nologo', '-v', 'q']);
  const meta = { job: option('--job') ?? 'Default', started: new Date().toISOString() };
  const bdnArgs = ['--filter', '*', '--artifacts', 'results/thresholds/bdn'];
  if (option('--job')) bdnArgs.push('--job', option('--job'));
  const status = dotnet(runThresholds(...bdnArgs));
  meta.finished = new Date().toISOString();
  writeFileSync(join(thresholdsDir, 'meta.json'), `${JSON.stringify(meta, null, 2)}\n`);
  failure = failure || status;
}

// The summary is written after a failed run too: the runs that ended are in it.
writeSummary();
if (failure !== 0) {
  console.error(`dotnet exited with code ${failure}`);
  process.exit(failure);
}
