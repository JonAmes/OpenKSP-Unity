# Step 1 — Bug-test gate results (re-run)

Owner: qa (bug tester). Gate per `Docs/BugTestPlan.md`. Re-run 2026-10-26 on `main` @ `c2b6dbf` (post-Unity-fix).
Verdict: **PASS** — all gate items green; no failing tests; production code untouched by this gate.

Context: the Unity-side fixes from the prior pass (commits `c2b6dbf` touching `PhysicsProxy.cs`, `FloatingOriginSystem.cs`, `SolarSystemStub.cs`) do not touch `Code/OpenKSP.Core/`; this re-run re-verifies the gate on the current tree and records the Unity-side verification that motivated the fixes.

## Item 1 — Build + pure-C# core
- `dotnet build Code/OpenKSP.Core -warnaserror` → **Build succeeded, 0 Warning(s), 0 Error(s)** (fresh rebuild).
- `dotnet test` build (test csproj) → clean under `-warnaserror`.
- `grep -rn "UnityEngine" Code/OpenKSP.Core/ --include='*.cs'` → **0 hits** — Core is pure C#.

## Item 2 — Test discovery + suite, twice in a row
- `dotnet test Code/OpenKSP.Core.Tests -t` → exactly **10 test classes** discovered: T1, T2, T3, T4, T5, T6, T7, T8, T9, T10 (19 methods total).
- Run 1: **Passed — Failed: 0, Passed: 19, Skipped: 0, Total: 19** (1 s).
- Run 2: **Passed — Failed: 0, Passed: 19, Skipped: 0, Total: 19** (1 s) — identical twice.
- No test skipped; no `[Fact(Skip=...)]` or `Assert.Skip` in the suite.

## Item 3 — Tolerance vs exactness hygiene
- `grep -rnE "DateTime|Random|Time\." Code/OpenKSP.Core/ --include='*.cs'` → **0 hits** (no Unity time / wall clock / randomness in Core).
- T9 spot-check unchanged: bit-level snapshot equality `snapA == snapB` on a non-trivial two-body run; asserts the state is non-default. ✔
- Determinism: T9 result identical across both full-suite runs. ✔

## Item 4 — Allocation-free spot check + data file
- `OrbitPropagator.Step`: RK4 scratch arrays pre-allocated in the constructor; per-step loop is double math on pre-allocated arrays only; no LINQ/boxing on the hot path. ✔
- `WorldState`: hot-path `Position(i)`/`Velocity(i)` are index reads into pre-allocated arrays; `Bodies()` (allocating getter) is not called by the bridge per frame. ✔
- Bridge hot path: `SolarSystemStub.Update` per-frame reads use `_system.World.Position(i)` / `Velocity(i)` only (allocation-free); no component lookups in `Update()` after the fix (references cached in `Awake`). ✔
- `planets.json` (`Assets/StreamingAssets/Planets/planets.json`) parses cleanly; sun, earth, moon all with `id`, `name`, `mass`, `radius`, `mu`. ✔
- Fixed-dt cadence intact: `FixedUpdateLoop.FixedUpdate` → `FloatingOriginSystem.Advance` → `TimeWarpController.Accumulate` + whole `FixedDelta` steps. ✔
- `grep -rn "System.Linq" Code/OpenKSP.Core/ --include='*.cs'` → one `using` in `GravityField.cs`, **no call sites** (carry-over cosmetic note). ✔

## Unity-side verification (what triggered the fixes)

The host Unity CLI (`/home/jonames/Unity/Hub/Editor/6000.6.6f1/Editor/Unity -batchmode -quit -nographics -projectPath … -no-collab`) exits **198 — "No valid Unity Editor license found"** (0 entitlement groups, no offline license). Headless Unity execution is environment-blocked, not a project defect.

Substitute verification: a Roslyn probe (`scratch/unity-bridge-probe2`) compiles the three production bridge files against the **real** Unity assemblies (`UnityEngine.dll` façade, `UnityEngine.UI.dll`, plus a `<ProjectReference>` to `OpenKSP.Core`) on `net8.0` / C# 9. Findings and fixes:

| # | Genuine error | Location | Fix |
|---|---|---|---|
| 1 | CS0592 `[RequireComponent]` on a field | `PhysicsProxy.cs:32` | attribute moved to class declaration |
| 2 | CS0592 `[RequireComponent]` on a field | `FloatingOriginSystem.cs:16` | attribute moved to class declaration |
| 3 | CS0592 `[RequireComponent]` on a field | `SolarSystemStub.cs:16` | attribute moved to class declaration |
| 4 | CS0246 `DVector` unresolved | `PhysicsProxy.cs` (imported only `Math` + `World`) | added `using OpenKSP.Core.Celestial;` |
| 5 | CS1061 `Rigidbody.collisionDetection` does not exist | `PhysicsProxy.cs:68` | `_rb.collisionDetectionMode = CollisionDetectionMode.Discrete;` |
| 6 | CS0618 `Rigidbody.velocity` obsolete in Unity 6.0 | `PhysicsProxy.cs:62,83` | `_rb.linearVelocity` (write and read-back) |

Members were confirmed by reflection against the real assemblies: `collisionDetection` absent, `velocity` marked obsolete, `linearVelocity` and `angularVelocity` live.

Post-fix probe: **zero genuine bridge errors remain.** The only residual diagnostics are harness artifacts that disappear in a real Unity project: CS0012 (`MonoBehaviour` resolves through Unity's façade→`UnityEngine.CoreModule` forwarder, which dotnet does not replicate) and the probe-only entry point.

## Observations
1. **Cosmetic: `using System.Linq;` in `GravityField.cs` is unused** — carry-over note, no functional defect.
2. **Known gap (carried):** the Unity bridge cannot run headlessly on this host (license) and `OpenKSP.Core` sits outside `Assets/` (no asmdef/DLL under `Assets/`); in real Unity the Core must be reachable from the project (e.g. a `Plugins/OpenKSP/Core` asmdef referencing the built Core assembly, or a prebuilt DLL under `Assets/Plugins/OpenKSP/Core/`). See the Unity play-test checklist in the review re-run.
