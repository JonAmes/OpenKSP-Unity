# Step 1 — Bug-test gate results

Owner: qa (bug tester). Gate per `Docs/BugTestPlan.md`. Run 2026-10-26 on `main` @ `e7a1e26`.
Verdict: **PASS** — all gate items green; no failing tests; production code untouched.

## Item 1 — Build + pure-C# core
- `dotnet build Code/OpenKSP.Core -warnaserror` → **0 Error(s)** (fresh rebuild, clean).
- `dotnet build Code/OpenKSP.Core.Tests -warnaserror` → clean.
- `grep -rn "UnityEngine" Code/OpenKSP.Core/ --include='*.cs'` → **no hits** — Core is pure C#.

## Item 2 — Test discovery + suite, twice in a row
- `dotnet test Code/OpenKSP.Core.Tests -t` → exactly **10 test classes** discovered: T1, T2, T3, T4, T5, T6, T7, T8, T9, T10 (19 methods total).
- Run 1: **Passed — Failed: 0, Passed: 19, Skipped: 0, Total: 19** (1 s).
- Run 2: **Passed — Failed: 0, Passed: 19, Skipped: 0, Total: 19** (1 s) — same result twice.
- No test skipped (`Skipped: 0`), no `[Fact(Skip=...)]` or `Assert.Skip` in the suite.

## Item 3 — Tolerance vs exactness hygiene
- `grep -rn "DateTime" Code/OpenKSP.Core/ --include='*.cs'` → none.
- `grep -rn "Random" Code/OpenKSP.Core/ --include='*.cs'` → none.
- `grep -rn "Time\." Code/OpenKSP.Core/ --include='*.cs'` → none (Core reads no Unity time; bridge never reads `Time.deltaTime`).
- T9 source spot-check (`Code/OpenKSP.Core.Tests/T9.cs`):
  - Line 51: `Assert.True(snapA == snapB);` — **exact bit-level equality** on the snapshot, not `Assert.True(Math.Abs(...) < 1e-8)` or any tolerance. ✔
  - Also asserts `!snapA.Equals(default)` (real state, not the default struct) and guards the state is non-trivial. ✔
- Determinism: T9 re-run result is identical across the two full-suite runs (19/19). ✔

## Item 4 — Allocation-free spot check + data file
- `OrbitPropagator.Step` (`Code/OpenKSP.Core/Orbit/OrbitPropagator.cs`): RK4 scratch arrays (`_k1Pos…_k4Pos`, `_k1Vel…_k4Vel`, `_pScratch`) allocated **once in the constructor**; the per-step loop performs only double math on pre-allocated arrays. No LINQ, no boxing on the hot path. ✔
- `WorldState.Step` / `TotalEnergy` / hot-path `Position(i)`/`Velocity(i)`: parallel-array reads only; `Bodies()` (the allocating getter) is **not** called per frame by the bridge. ✔
- Bridge no-alloc reads: `SolarSystemStub.Update` uses only `world.Position(i)` / `world.Velocity(i)` per frame. ✔
- `planets.json` (`Assets/StreamingAssets/Planets/planets.json`) parses and contains all three bodies with the required fields (`id`, `name`, `mass`, `radius`, `mu`): sun, earth, moon — VALID. ✔
- Bridge MonoBehaviours (`PhysicsProxy`, `FloatingOriginSystem`, `SolarSystemStub`) do **not** read `Time.deltaTime` for the Core step; stepping is fixed-dt via `TimeWarpController.Accumulate` + `FixedUpdateLoop.FixedUpdate` → `Advance`. ✔
- `grep -rn "System.Linq"` in Core source: one `using System.Linq;` in `GravityField.cs`, **no LINQ call sites** — effectively a unused import (see findings).

## Observations (recorded per BugTestPlan, item 4 → review material)
1. **Cosmetic: `using System.Linq;` in `GravityField.cs` is unused** — no LINQ call sites anywhere in Core. Not a functional defect; flag as a clean-up candidate in the cold review.
2. **Cosmetic (bridge):** `FloatingOriginSystem.Initialize(IEnumerable<CBody>)` accepts a wider interface than the real `CBodyLoader.Load` return (`IImmutableList<CBody>`); a compile probe (scratch, removed) confirmed `IImmutableList<T>` satisfies `IReadOnlyList<T>`, so tightening to `IReadOnlyList<CBody>` is valid. Not a functional defect.
3. **Known gap (per §10, pre-accepted):** the Unity bridge MonoBehaviours cannot be compiled or run on this host (no `UnityEngine*.dll` / Burst / Editor DLLs). Validation here is static API review + headless Core build/tests only.
