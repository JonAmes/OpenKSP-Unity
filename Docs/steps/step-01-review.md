# Step 1 — Cold Review (Task 7, re-run)

**Reviewer** · basis `Docs/agents/reviewer.md`, `git diff 3f953ed..main` (41 files, 2,632 lines, incl. `c2b6dbf`), `Docs/steps/step-01.md`, `step-01-report.md`.
Fresh gate on this pass: `dotnet test Code/OpenKSP.Core.Tests -warnaserror` → **Passed 19, Failed 0, Skipped 0** (1 s); build clean `-warnaserror`.

## VERDICT: CHANGES NEEDED (1 MAJOR; no blockers on the sim side)

The previous MAJOR (Find-in-Update) is resolved by `c2b6dbf`. Cold-reading the fixed tree surfaces **one new MAJOR**, a runtime defect for the Unity play-test; the remainder are carry-over minors.

## Findings

### [MAJOR] CBodyLoader.cs:24-44 — default path resolution does not work under Unity
`ResolveDefaultPath()` walks 5 parent levels up from `AppDomain.CurrentDomain.BaseDirectory` (the dotnet build output dir → repo root) and then reads `Assets/StreamingAssets/Planets/planets.json`. In the Unity Editor/player, `AppDomain.CurrentDomain.BaseDirectory` is the editor/`game.data` directory, **not** the repo, so `CBodyLoader.Load(null)` throws `FileNotFoundException` in `SolarSystemStub.Awake` and the scene breaks before any frame.
Fix: the bridge (not Core) should resolve the StreamingAssets path in Unity — e.g. `Path.Combine(Application.dataPath, "StreamingAssets", "Planets", "planets.json")` in `SolarSystemStub.Awake` and pass it to `CBodyLoader.Load(path)`; keep Core's dotnet default for tests, or make the dotnet branch explicitly a fallback.

### [RESOLVED — was MAJOR] SolarSystemStub.cs — Find-in-Update
`c2b6dbf` caches `_system` and `_hud` in `Awake` (valid via `[RequireComponent]`) and `Update()` now performs **zero** component lookups. Confirmed by re-read of the fixed file.

### [MINOR — carry-over] SolarSystemStub.cs:68-71 — per-frame HUD string allocation
`SetHudText` still interpolates a fresh string every frame (display-only, not sim hot path).
Fix: reuse a `StringBuilder`, or refresh only when `WarpRate`/`Position(0)` actually changed.

### [MINOR — carry-over] GravityField.cs:1 — unused `using System.Linq;`
No LINQ call sites in Core.
Fix: remove the import.

### [MINOR — carry-over] D3.cs (Coordinates.Scale) — hardcoded `1.0` with a dangling "will be set" comment
Identity works today (FloatingOrigin carries the offset; T10 verifies composition ≤ 1e-6), but the comment reads as unfinished work.
Fix: drop the comment or implement the scale mapping so the claim is true.

### [MINOR — carry-over] WorldState.cs:38-49 — `Bodies` property allocates on every access
Allocating getter; bridge and tests use the index accessors. A future per-frame caller would silently allocate.
Fix: keep it read-time-only with a guard note, or remove it in favour of `Position(i)`/`Velocity(i)`.

### [MINOR — carry-over] FloatingOriginSystem.cs:33 — `SetWarpRate` is a public mid-frame API
Warp changes apply at the next fixed step, so behaviour is safe; the API is not gated against `Update` callers.
Fix: document the step-boundary semantics or fold it behind the fixed-update cadence.

### [MINOR — carry-over] OrbitPropagator.cs:16-27 — body count via manual `foreach` instead of `.Count`
Allocation-free and correct; small readability blemish.
Fix: take `IReadOnlyList<CBody>` (or `CBody[]`) and use `.Count`/`.Length`.

## Cross-checks against the §2–§7 rules and checklist

- **Spec conformance — yes.** Core sim per §2–§9; bridge per §3/§7; probe work is scratch-only (not committed). Nothing beyond the spec.
- **Sim/View split — clean.** `Code/OpenKSP.Core/` has **zero** `UnityEngine` references (source-only grep, 0 hits). All double math in Core; View reads via allocation-free accessors.
- **Precision — correct.** World positions/velocities/time are `double` (`D3`, `WorldState._simTimeSeconds`); local float zone is `F3` only.
- **Correctness — correct.** RK4 combine; attractive N-body accel with singularity guards; `TotalEnergy` PE term `−μ_iμ_j/(G·r)`; composition `ToGlobal(ToLocal(p)) == p` ≤ 1e-6 (T10).
- **Tests — behaviour-checked, independent expectations.** T3 recomputes N-body accel by hand; T8 round-trips Kepler elements; T9 asserts **bit-level** snapshot equality, not tolerance. 19/19, 0 skipped, deterministic twice. No weakened or deleted tests.
- **Unity safety — C# 9 only, no per-frame alloc in sim; Find-in-Update fixed.** The probe against the real `UnityEngine.dll`/`CoreModule`/`UnityEngine.UI.dll` assemblies confirmed the fixed bridge compiles with **no genuine errors** (CS0592/CS0246/CS1061/CS0618 all eliminated; residual probe diagnostics are harness artifacts: Unity's façade→module forwarder reference and the implicit BCL, both automatic in real Unity). The one Unity-side runtime hole is the MAJOR above (data path).
- **Readability — good.** Comments explain why; the fixes carry one-line rationale inline (e.g. "velocity is obsolete in Unity 6.0 — linearVelocity is the live member").

## Questions for the coder

1. **Data path (drives the MAJOR):** who owns StreamingAssets path resolution — Core fallback or bridge? Suggested: bridge builds the path in `Awake` and passes it explicitly to `CBodyLoader.Load(path)`; Core's dotnet default stays for tests.
2. **Unity assembly surface:** `OpenKSP.Core` lives outside `Assets/`. How will the Unity project reference it (asmdef + prebuilt DLL under `Assets/Plugins/OpenKSP/`)? Needed before the human play-test can run.
3. `Coordinates.Scale` identity vs origin-driven mapping (carry-over)?
4. `WorldState.Bodies`: keep as documented read-time-only seam or remove (carry-over)?
