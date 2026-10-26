# Step 01 — Phase 1: Core Coordinates — Completion Report

**Branch:** step-01 · **HEAD:** `6040d37` · **Date:** 2026-10-26
**Basis:** `Docs/steps/step-01-plan.md`, `Docs/steps/step-01.md`, `Docs/Phase1-Design.md`

## Status table

| # | Task | Owner | Status | Evidence |
|---|------|-------|--------|----------|
| 1 | Math foundation `D3`, `DTime` (+operators, ToFloat/FromFloat); test scaffold; T1 | coder | ✅ done | `Math/D3.cs`, `DTime.cs`, `F3`; T1 green |
| 2 | Bodies & data driver: `CBody`, `CBodyLoader` (real JSON parse, §6), `planets.json`; T2, T3 | coder | ✅ done | commit `4fc8dc7`; T2×3, T3×3 green |
| 3 | World state, gravity, propagation: `GravityField`, `OrbitPropagator` (RK4), `WorldState` (Step, TotalEnergy, snapshot/restore); T4, T5, T9 | coder | ✅ done | commit `db7fcb6`; T4, T5, T9 green |
| 4 | Floating origin, time warp, kepler round-trip: `FloatingOrigin`, `TimeWarpController`, `KeplerianElements`; T6, T7, T8, T10 | coder | ✅ done | commit `12affd7`; T6, T7 (12 frames @ warp 8), T8 (round-trip < 1e-3), T10 (origin crossing) green |
| 5 | Unity bridge scaffolding: PhysicsProxy, FloatingOriginSystem, SolarSystemStub | coder | ✅ done | commits `2521a66` (+ `6040d37`); three MonoBehaviours under `Assets/Plugins/OpenKSP/Runtime/`; static API review (Unity compile not possible on this host) |
| 6 | Bug-tester gate per `Docs/BugTestPlan.md` | qa | ❌ not done | not started |
| 7 | Cold review of final diff → `docs/steps/step-01-review.md` | reviewer | ❌ not done | not started |

**Result: 5 of 7 complete (Tasks 1–5), 2 of 7 outstanding (Tasks 6–7).**

## Test run (current, re-verified 2026-10-26)

```
dotnet build Code/OpenKSP.Core.Tests -warnaserror   → 0 Warning(s), 0 Error(s)
dotnet test  Code/OpenKSP.Core.Tests               → Passed 19, Failed 0, Skipped 0, Total 19
```

Covers T1–T10 plus the `RelAssert` helpers. Build clean under `-warnaserror`; no UnityEngine references in `OpenKSP.Core`.

## Task 4 — what was delivered

- **`FloatingOrigin`** — `SlideTo(D3)` re-bases the origin (world arrays shifted by the same `D3` delta, so `ToGlobal` round-trips exactly); `DistanceFromOrigin(i)` returns the residual in metres; local (`float`) zone radius = one sun radius. Bodies inside the local zone are IN; outside, OUT.
- **`TimeWarpController`** — fixed-step accumulator: `Accumulate(double dt) → int` returns how many `FixedDelta` (= 1/1200 s) steps to run after multiplying by `WarpRate`; warp changes only at the frame boundary, never inside a step.
- **`KeplerianElements`** — state → elements → state round-trip (T8): Δpos < 1e-3 m for the seeded bodies.

## Task 5 — what was delivered

- **`PhysicsProxy`** (`Assets/Plugins/OpenKSP/Runtime/PhysicsProxy.cs`) — sim→render entry point for one body: `SyncFromGlobal`/`ReadBack` bridge `WorldState` ↔ Unity `Rigidbody`; IN-body writes use `MovePosition` (kinematic), OUT-body writes use `velocity` (discrete); velocity/angular-velocity are pure kinematic deltas — no origin offset applied.
- **`FloatingOriginSystem`** (`Assets/Plugins/OpenKSP/Runtime/FloatingOriginSystem.cs`) — singleton MonoBehaviour + `[RequireComponent(typeof(FixedUpdateLoop))]` (the `FixedUpdateLoop` class lives in the same file, per §7's three-file set). Fixed `FixedUpdate(dt)` cadence forwards Unity frame-time into `Advance(float)`, which calls `TimeWarpController.Accumulate` and steps `WorldState` that many times at `FixedDelta = 1/1200 s`. Seeds sun/earth/moon as circular orbits from `CBodyLoader.Load()`; origin pinned at the sun.
- **`SolarSystemStub`** (`Assets/Plugins/OpenKSP/Runtime/SolarSystemStub.cs`) — editor-scene entry point: loads `planets.json` via `CBodyLoader`, builds the world via `FloatingOriginSystem`, attaches one `PhysicsProxy` per body, and renders a HUD (`UnityEngine.UI.Text`) showing global position, residual distance, and warp rate — display-only, no sim logic.
- **`WorldState` (Core change)** — added allocation-free `Position(int i)` / `Velocity(int i)` accessors used by the bridge's per-frame reads (no `CBody` reconstruction in the hot path).

All bridge calls were verified against the real Core source (`FloatingOrigin`, `CBody`, `WorldState`, `TimeWarpController`, `CBodyLoader`): every signature matches; no invented API.

## What could NOT be verified here (honest gaps)

- **Unity-side compilation:** no Unity runtime/editor on this host; per the plan, Task 5 is "static review (no test here)". Final Unity compile belongs to the human's editor box.
- No `.unity`/`.prefab` scene files were created (forbidden per house rules); scene wiring is expected on the Unity side.
- Cosmetic: `FloatingOriginSystem.Initialize` takes `IEnumerable<CBody>` and materializes a `List` at init; an equivalent `IReadOnlyList<CBody>` form was confirmed valid by a real `dotnet` compile probe (`IImmutableList<T>` satisfies `IReadOnlyList<T>`). Functionally identical; no blocker.
- Untracked local artifacts remain uncommitted per spec: build dirs (`.bin`/`.obj`), `AGENTS.md`, `Docs/*`, `probe.cs` (scratch probe outside the repo tree).

## What is left (not complete)

1. **Task 6** — bug-tester gate per `Docs/BugTestPlan.md` (T1–T10 ×2, build, greps).
2. **Task 7** — cold review of the final diff → `docs/steps/step-01-review.md`.
