# Step 1 — Cold Review (Task 7)

**Reviewer** · basis `Docs/agents/reviewer.md`, `git diff 3f953ed..main` (41 files, 2,589 lines), `Docs/steps/step-01.md`, `step-01-report.md`.
Fresh gate on this pass: `dotnet test Code/OpenKSP.Core.Tests -warnaserror` → **Passed 19, Failed 0, Skipped 0** (1 s).

## VERDICT: PASS

All seven checklist points are met. The findings below are cosmetic/minor; none is a blocker.

## Findings

### [MAJOR] SolarSystemStub.cs:46,64 — component lookups inside `Update()` (Find-in-Update)
`Update()` calls `GetComponent<FloatingOriginSystem>()` (line 46) and, via `SetHudText`, `GetComponentInChildren<Text>()` (line 66) **every frame**. The §7 rule is "no Find calls in Update." Both are per-frame component lookups.
Fix: cache the `FloatingOriginSystem` reference and the HUD `Text` once in `Awake` (already available there) and reuse the fields in `Update`.

### [MINOR] SolarSystemStub.cs:72-74 — per-frame string allocation in `Update()`
`SetHudText` interpolates a fresh string (`$"..."`) every frame — a per-frame allocation. It is display-only, not the sim hot path, so it does not break the no-alloc sim rule, but still re-allocates each frame.
Fix: build the HUD text into a reused `StringBuilder`, or only refresh when `WarpRate`/residual actually changes.

### [MINOR] GravityField.cs:1 — unused `using System.Linq;`
No LINQ call sites exist in `Code/OpenKSP.Core/` (source-only grep, `.cs`). Cosmetic.
Fix: remove the import.

### [MINOR] FloatingOriginSystem.cs:58 — `IEnumerable<CBody>` could be `IReadOnlyList<CBody>`
`CBodyLoader.Load` returns `IImmutableList<CBody>` (satisfies `IReadOnlyList` — verified by a `dotnet` compile probe). Narrowing the parameter matches the real return type and keeps `.Count`/indexing available without a `List` materialisation at init.
Fix: change the parameter to `IReadOnlyList<CBody>`.

### [MINOR] D3.cs:98,105 — `Coordinates.Scale` is a hardcoded `1.0` with a dangling "will be set" comment
`Scale = 1.0` with comment "Will be set from origin-based mapping" signals an incomplete mapping. Identity works today (FloatingOrigin carries the offset; T10 verifies composition ≤ 1e-6), so it is not a correctness failure, but the comment reads as unfinished work.
Fix: either drop the "will be set" comment or implement the scale mapping so the comment is true.

### [MINOR] WorldState.cs:38-49 — `Bodies` property allocates on every access and is easy to misuse
`Bodies` builds a fresh `CBody[]` per get, and the bridge hot path is documented to use `Position(i)`/`Velocity(i)`. Nothing misuses it today (bridge + tests use the index accessors), but a future per-frame caller would silently allocate.
Fix: keep a guard note / make it clearly read-time-only, or remove the property in favour of the index accessors.

### [MINOR] FloatingOriginSystem.cs:34 — `SetWarpRate` exposes a mid-frame public API
`Advance` (called by FixedUpdateLoop) is the step entry; a caller invoking `SetWarpRate` from `Update` would still only apply at the next fixed step (§3 boundary), so it is safe, but the API is not gated against use from a non-fixed callback. Minor.
Fix: document that warp changes take effect at the next fixed step, or fold it behind the fixed-update cadence.

### [MINOR] OrbitPropagator.cs:16-27 — scratch arrays sized from `new List`-free manual count
`OrbitPropagator` counts bodies with a manual `foreach` loop instead of taking `.Count`. Allocation-free and correct, but the manual count is a small readability blemish given `IEnumerable<CBody>` is materialised elsewhere.
Fix: accept `IReadOnlyList<CBody>` (or `CBody[]`) and use `.Length`/`.Count` directly.

## Cross-checks against the §2–§7 rules and checklist

- **Sim/View split — clean.** `Code/OpenKSP.Core/` has **zero** `UnityEngine` references (source-only grep). All double math lives in the Core; the bridge reads it through allocation-free `WorldState.Position/Velocity`.
- **Precision — correct.** World positions/velocities/time are `double` (`D3`, `WorldState._simTimeSeconds`); only the View local frame is `float` (`F3`).
- **Correctness — correct.** RK4 is the standard 4-stage combine (`OrbitPropagator.Step`); N-body `BodyAccel` is attractive and allocation-free; singularity guards present (`rSq` thresholds in `GravityField`); `TotalEnergy` PE term simplifies to `−μ_iμ_j/(G·r)`; `FloatingOrigin.SlideTo` preserves `LocalRadius`; composition `ToGlobal(ToLocal(p)) == p` verified to ≤ 1e-6 (T10).
- **Determinism — exact.** T9 asserts **bit-level** snapshot equality (`snapA == snapB`), not tolerance, on a non-trivial two-body run. PASS.
- **Tests check behaviour with independent expectations.** T3 recomputes N-body acceleration by hand and compares to `GravityField.Acceleration`; T8 round-trips Kepler elements (self-consistency, not copied from the code). 19/19, 0 skipped. No weakened or deleted tests.
- **Unity safety — one real issue (the MAJOR above).** C# 9 only (`LangVersion 9`, builds clean under `-warnaserror`); no hand-edited `.unity/.prefab/.meta`; sim hot path (`Advance`, `Step`, `PreStepSync`) is allocation-free. The only Update-time lookups are the MAJOR.
- **Readability — good.** Names, sizes, and "why" comments are clear throughout; comments explain intent (e.g. IN→MovePosition, OUT→kinematic, scratch pre-allocation).

## Questions for the coder

1. SolarSystemStub: is the HUD `Text` guaranteed to be present at `Awake` (i.e. a child GO created before `Awake` runs)? If yes, caching the reference is trivial; if the HUD GO is created later, we need a different anchor.
2. `Coordinates.Scale`: is the identity (scale 1.0) the final Phase 1 intent, or should it be driven by the origin mapping? (Drives finding #5.)
3. `WorldState.Bodies`: should it be removed in Phase 1 to force the index accessors, or kept as a documented read-time-only seam?
