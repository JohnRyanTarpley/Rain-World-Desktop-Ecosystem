// Fast project checks that need no .NET SDK. CI runs this before the build.
//   node .github/scripts/check-project.mjs
// Checks: every project under src/ and tests/ is in RWDE.sln, and no Rain World content
// (assembly dumps, decompiled code, game data) is tracked.
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";

const errors = [];
const tracked = execFileSync("git", ["ls-files", "-z"], { encoding: "utf8" }).split("\0").filter(Boolean);

const solution = readFileSync("RWDE.sln", "utf8").replace(/\\/g, "/");
const projects = tracked.filter((f) => /^(src|tests)\/.*\.csproj$/.test(f));
for (const project of projects) {
  if (!solution.includes(project)) errors.push(`${project} is not in RWDE.sln (dotnet sln add ${project})`);
}

// IP policy: nothing from the game may be tracked. Deliberate native libraries are listed here.
const allowedBinaries = new Set([]);
const forbidden = [
  [/\.dll$/i, "assembly"],
  [/(^|\/)Decompiled\//i, "decompiled code"],
  [/(^|\/)GameData\//i, "game data"],
  [/(^|\/)RainWorld_Data\//i, "game install content"],
  [/(^|\/)StreamingAssets\//i, "game install content"],
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
console.log(`OK: ${projects.length} projects in the solution, ${tracked.length} tracked files checked.`);
