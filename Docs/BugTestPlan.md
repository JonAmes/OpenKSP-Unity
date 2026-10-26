# OpenKSP — Phase 1: Bug-Tester Plan (gate before merge)

**Role:** solo bug tester, after coder delivery. Runs everything on this host (no Unity editor needed for the gate).
**Gate:** ALL items below must pass — then report results; any failure is a blocking defect back to coder.

## Environment

```
export PATH=/home/jonames/.dotnet:$PATH
cd /home/jonames/Projects/OpenKSP-Unity/Code
```

## 1. Build verification

- [ ] `dotnet build -warnaserror` on `OpenKSP.Core` and `OpenKSP.Core.Tests` → exit 0, **zero warnings** (warnings are failures).
- [ ] `dotnet build` on `OpenKSP.sln` clean.
- [ ] `grep -rn UnityEngine Code/OpenKSP.Core/` must return nothing.

## 2. Test suite execution (the doc's T1–T10)

```
dotnet test Code/OpenKSP.Core.Tests/
```

- [ ] **T1** D3 float round-trip — per-conversion relative error < 1e-6.
- [ ] **T2** single-body gravity vs `GM/r²` — rel. error < 1e-9.
- [ ] **T3** 3-body sum vs independent pairwise — < 1e-9.
- [ ] **T4** Core-only RK4 circular orbit, 1 year, dt=1/1200 s, vs analytic — max radial residual < 100 m.
- [ ] **T5** energy conservation, 10⁶ steps, `TotalEnergy` per doc §9 — ΔE/E < 1e-6.
- [ ] **T6** origin-slide round-trip — identity ≤ 1e-6 relative.
- [ ] **T7** warp=8, 12 frames — step count exact.
- [ ] **T8** KeplerianElements round-trip — Δpos < 1e-3 m.
- [ ] **T9** determinism — two runs, bit-identical snapshot.
- [ ] **T10** origin-crossing — identity ≤ 1e-6 every step, no discontinuities beyond integrator error.

All 10 must be present in the suite (test names discoverable via `dotnet test -t` / source inspection) — a suite missing T10 is a failure even if 9 pass.

## 3. Determinism cross-check (independent of the suite)

- [ ] Run `dotnet test` **twice** in a row; both runs must report 10/10 passed (no flakiness).
- [ ] Spot-check T9 by reading the test source: it must compare two full snapshots for **exact** equality (not tolerance).

## 4. Hot-path hygiene (doc §4 constraints)

- [ ] `grep` Core sources for `LINQ` usage in hot-path files (`OrbitPropagator`, `WorldState`) — flag any per-step allocations.
- [ ] `grep -rn "DateTime" Code/OpenKSP.Core/` → must be empty (no wall-clock reads, doc constraint).
- [ ] `grep -rn "Random" Code/OpenKSP.Core/` → must be empty (non-deterministic sources).

## 5. Unity-side code review (no compile here — static review only)

- [ ] MonoBehaviours live under `Assets/Plugins/OpenKSP/Runtime/`; reference `OpenKSP.Core` types with a proper using/import path.
- [ ] Verify §3 ordering in `PhysicsProxy`: classify → pre-snap IN (MovePosition+vel) / OUT (kinematic, discrete collision) → Unity step → post-read-back IN / Core advance OUT → origin slide re-snap.
- [ ] Verify `FloatingOriginSystem` has `RequireComponent(FixedUpdateLoop)`, owns `TimeWarpController`, and **Core never reads `Time.deltaTime`** (grep Core for `Time.`).
- [ ] `planets.json` exists with required fields `id,name,mass,radius,mu` (validate with `python3 -c "import json; json.load(open(...))"` + field check).
- [ ] Core project must not be referenced by Unity MonoBehaviours in a way that breaks the pure library (separate folder is fine — Unity will resolve via asmdef or project reference in the Unity project; flag if an asmdef is missing — Unity project can define one later).

## 6. Report format (final deliverable)

```
PASS/FAIL summary per item (1–6)
- Test results: 10/10 expected, list any failures with the failing test name + measured vs required value
- Build: warning count (must be 0)
- Hygiene greps: any hits listed
- Unity-side: list any ordering violations vs doc §3
- Verdict: MERGE-READY / BLOCKED (with numbered defect list)
```

**Escalation rule:** if a doc tolerance is genuinely unattainable (e.g. T4 exceeds 100 m with the Core-only integrator), do NOT modify code or doc — record the measured value, mark the item BLOCKED, and report the discrepancy for the designer to adjudicate.
