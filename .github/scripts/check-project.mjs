// Fast project checks that need no Unity license. CI runs this before the Unity jobs.
//   node .github/scripts/check-project.mjs
// Checks: every Assets/ entry has a .meta (and no orphan .meta files), meta GUIDs are unique,
// asmdefs are valid JSON, every test asmdef is Editor-only behind UNITY_INCLUDE_TESTS,
// and no Rain World content (assembly dumps, decompiled code, game data) is tracked.
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative, sep } from "node:path";

const root = process.cwd();
const errors = [];
const posix = (p) => p.split(sep).join("/");

// Unity skips names that start with "." or end with "~" (and their contents).
const unityIgnored = (name) => name.startsWith(".") || name.endsWith("~");

function walk(dir, out) {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (name.endsWith(".meta")) {
      const target = full.slice(0, -5);
      if (!existsSync(target)) errors.push(`orphan meta: ${posix(relative(root, full))}`);
      continue;
    }
    if (unityIgnored(name)) continue;
    if (!existsSync(full + ".meta")) errors.push(`missing meta: ${posix(relative(root, full))}`);
    out.push(full);
    if (statSync(full).isDirectory()) walk(full, out);
  }
}

const assets = [];
walk(join(root, "Assets"), assets);

const guids = new Map();
for (const file of assets) {
  const meta = file + ".meta";
  if (!existsSync(meta)) continue;
  const match = /^guid:\s*([0-9a-f]{32})\s*$/m.exec(readFileSync(meta, "utf8"));
  const rel = posix(relative(root, meta));
  if (!match) { errors.push(`meta without a guid: ${rel}`); continue; }
  if (guids.has(match[1])) errors.push(`duplicate guid ${match[1]}: ${rel} and ${guids.get(match[1])}`);
  else guids.set(match[1], rel);
}

for (const file of assets.filter((f) => f.endsWith(".asmdef"))) {
  const rel = posix(relative(root, file));
  let asmdef;
  try {
    asmdef = JSON.parse(readFileSync(file, "utf8"));
  } catch (e) {
    errors.push(`invalid JSON in ${rel}: ${e.message}`);
    continue;
  }
  if (rel.startsWith("Assets/Tests/")) {
    const editorOnly = JSON.stringify(asmdef.includePlatforms) === JSON.stringify(["Editor"]);
    if (!editorOnly) errors.push(`${rel}: test asmdefs must be Editor-only (EditMode)`);
    if (!asmdef.defineConstraints?.includes("UNITY_INCLUDE_TESTS"))
      errors.push(`${rel}: test asmdefs need the UNITY_INCLUDE_TESTS define constraint`);
    if (asmdef.autoReferenced !== false) errors.push(`${rel}: test asmdefs must set autoReferenced: false`);
  }
}

// IP policy: nothing from the game may be tracked. Deliberate native plugins are listed here.
const allowedBinaries = new Set([]);
const tracked = execFileSync("git", ["ls-files", "-z"], { encoding: "utf8" }).split("\0").filter(Boolean);
const forbidden = [
  [/\.dll$/i, "assembly"],
  [/(^|\/)Decompiled\//i, "decompiled code"],
  [/(^|\/)GameData\//i, "game data"],
  [/(^|\/)RainWorld_Data\//i, "game install content"],
  // Unity's own Assets/StreamingAssets/ is allowed; a StreamingAssets folder anywhere else is a game copy.
  [/^(?!Assets\/StreamingAssets\/)(.*\/)?StreamingAssets\//i, "game install content"],
  [/Assembly-CSharp/i, "game assembly or its decompilation"],
];
for (const file of tracked) {
  if (allowedBinaries.has(file)) continue;
  for (const [pattern, what] of forbidden) {
    if (pattern.test(file)) errors.push(`tracked ${what} (IP policy): ${file}`);
  }
}

if (errors.length) {
  for (const e of errors) console.error(`error: ${e}`);
  console.error(`\n${errors.length} problem(s) found.`);
  process.exit(1);
}
console.log(`OK: ${assets.length} asset entries, ${guids.size} unique GUIDs, ${tracked.length} tracked files checked.`);
