# OpenKSP — Phase 1 Design: Floating Origin Coordinates & Localized Physics Proxy

**Status:** v1.1 — review-approved (all 10 reviewer findings addressed)
**Author:** lead developer
**Scope:** Phase 1 of the roadmap ("Core Coordinates"). Foundation for Phases 2–18 — they consume these APIs; out of scope here.

---

## 1. Design goals

1. **Arbitrary-range simulation.** Orbital mechanics must use double precision (`double3`) for positions, velocities, and time. Unity's float physics loses precision beyond ~10^5 m from the origin, so orbital dynamics never run in Unity physics.
2. **Unity physics stays useful.** Local interactive physics (RCS, landing, collisions, buoyancy) runs in a *localized frame* around an active origin, bridged back to the double world state.
3. **Determinism-ready.** All math is pure C# with **zero Unity dependencies** — unit-testable headless (`dotnet test`), later unmodified on the headless server (Phase 4).
4. **Moddable from day one.** Celestial bodies are data-driven (JSON), nothing hardcoded.
5. **Single-threaded Core.** Core is single-threaded by contract: no locks, no wall-clock reads. Phase 4 servers step the world serially on one worker thread; `ToSnapshot()/FromSnapshot()` is the seam for future lock-free workers.

## 2. Coordinate architecture

### 2.1 Two layers

| Layer | Type | Domain | Owner |
|---|---|---|---|
| **Global state** | `D3` (double) + `double` simTime | all entities vs. system barycenter | `OpenKSP.Core` (`WorldState`) |
| **Local physics frame** | Unity `Vector3` (float), relative to active origin | entities within `localRadius` (default 1e5 m) | Unity MonoBehaviours (`FloatingOriginSystem`, `PhysicsProxy`) |

**Active origin** = `D3` point the local frame is anchored to. It slides so the player's ship stays near it. Sliding is pure bookkeeping — no physics changes, only the local↔global mapping.

### 2.2 Local ↔ global mapping

```
local:  Vector3 l = (global.D3 - origin.D3)   // float, near zero
global: D3 g      = origin.D3 + D3.FromFloat(l)
```

All rendered positions come from the global layer, converted through the active origin. **Unity `Rigidbody` transforms/velocities are always local values; never store global values in a transform.**

### 2.3 Precision model (review-corrected)

- float32 spacing at magnitude X is `2^-23·X ≈ 1.19e-7·X` (1 ulp); worst-case double→float roundoff ≈ ½ ulp ≈ `5.96e-8·X`.
- **At X = 1e5 m: 1-ulp ≈ 0.012 m (~1.2 cm); ½-ulp ≈ 0.006 m (~6 mm).** (earlier draft's "0.01 mm" claim was off by ~30×.)
- Since local position `l` is near zero, local→global conversion error is dominated by `l`'s quantization, **≤ ~6 mm per step** — that is the per-step precision budget.
- **Canonical invariant:** the double state is always exactly `origin + FromFloat(local_proxy_state)`. Drift against a double-reference integrator is bounded per step and measured by T4/T10, never assumed away; bodies leaving the local frame are re-derived through the exact mapping, so error does not compound unboundedly.
- The HUD displays two metrics: the (structurally zero) mapping residual, and drift-vs-double-reference.

## 3. Physics Proxy heartbeat sync — mandatory ordering (review-specified)

Per fixed step:

1. **Classify.** For each proxy: `d = |globalPos − origin|` (double). `IN` if `d < localRadius`, else `OUT`.
2. **Pre-step snap (IN).** `rb.MovePosition(localPos)`; `rb.velocity = globalVel.ToFloat()`; `rb.angularVelocity = globalAngVel.ToFloat()`. `MovePosition` avoids solver interference.
3. **Pre-step snap (OUT).** Set `rb.velocity/angularVelocity` from global state; `rb.collisionDetection = CollisionDetectionMode.Discrete`; `rb.isKinematic = true` — zero Unity integration for OUT bodies.
4. **Unity step.** One fixed physics step (`dt`).
5. **Post-step read-back (IN).** Read `rb.position/velocity/angularVelocity` (local); fold into canonical double state: `globalPos = origin + FromFloat(localPos)`; same for velocity/angular.
6. **Post-step advance (OUT).** Advance canonical state with Core double RK4; `rb.transform.position = (globalPos' − origin).ToFloat()`.
7. **Origin slide.** If the active origin moved this step, re-snapshot all transforms from canonical state within the same ordering (O(N); spatial-hash optimized later).

**Time warp:** Core advances in fixed `dt = 1/1200 s` steps independent of frame rate; `Time.timeScale = warp` is applied on variable-step boundaries only; warp changes never take effect mid-step. Core **never** reads `Time.deltaTime` — it owns its unscaled fixed timestep.

## 4. Pure C# core library — `OpenKSP.Core`

**Layout (no UnityEngine refs, .NET 8, `-warnaserror`):**
```
OpenKSP.Core/
  Math/D3.cs                    // double vector: +,-,*,·,×, lerp, ToFloat/FromFloat
  Math/DTime.cs                 // double seconds, formatting
  Celestial/CBody.cs            // data-driven: id, name, mass, radius, mu, parent?
  Celestial/CBodyLoader.cs      // JSON loader (schema in §6)
  Celestial/GravityField.cs     // point-mass N-body acceleration; analytic 2-body helper
  Propagation/OrbitPropagator.cs  // RK4 on D3 using GravityField
  Propagation/KeplerianElements.cs // state↔elements (a,e,i,M,Ω,ω) — UI/planning (Phase 2)
  World/WorldState.cs           // all rigid states + barycenter; Step(dt); TotalEnergy()
  World/FloatingOrigin.cs       // origin D3 + localRadius + SlideTo(target)
  World/WorldSnapshot.cs         // simTime, origin, per-body states
  Time/TimeWarpController.cs     // fixed-step accumulator, warp rate
```

Constraints:
- **No allocations in the hot path**: value types only, no LINQ/boxing; `ImmutableArray<CBody>` for the body list.
- **Single-threaded contract**: no thread-safety guarantee; `WorldState` is only stepped on one thread; `ToSnapshot()/FromSnapshot()` is the only Phase-4 extension seam.
- **No wall-clock reads anywhere in Core.**
- **Determinism:** identical inputs → bit-identical outputs (verified by T9).

### 4.1 API surface

```csharp
// Core (pure)
var g = new GravityField(bodies);            // bodies: ImmutableArray<CBody>
var a = g.Acceleration(pos);                // D3
world.Step(dt);                             // advances all states
var local = origin.ToLocal(pos);            // Vector3 (float)
var global = origin.ToGlobal(local);        // D3
var snap = world.ToSnapshot();              // WorldSnapshot
WorldState.FromSnapshot(snap);              // restore

// Unity bridge (MonoBehaviour)
floatingOrigin.SlideTo(ship.GlobalPos);
proxy.SyncFromGlobal(state);   // pre-step (order 2/3)
proxy.SyncToGlobal(state);     // post-step (order 5/6)
```

## 5. Threading & serialization contract

- Core: single-threaded, lock-free, no locks.
- `WorldState.ToSnapshot()` → immutable `WorldSnapshot` (simTime, origin, per-body states). `WorldState.FromSnapshot(snap)` restores. These are the **only** seams Phase 4 servers/clients need.
- Doubles serialized as **full-precision decimal text** in JSON — lossless saves.
- `CBodyLoader` parses per §6; no body constants in code.

## 6. Data-driven body JSON schema

File: `Assets/StreamingAssets/Planets/planets.json`
```json
{
  "units": { "length": "m", "mass": "kg", "time": "s" },
  "bodies": [
    { "id": "sun",   "name": "Sun",   "mass": 1.989e30, "radius": 6.96e8, "mu": 1.32712440018e20, "parent": null },
    { "id": "earth", "name": "Earth", "mass": 5.972e24, "radius": 6.371e6, "mu": 3.986004418e14,  "parent": "sun" }
  ]
}
```
Required: `id`, `name`, `mass`, `radius`, `mu` (GM, m³/s²). Optional: `parent` (Phase 2 hierarchy), `atmosphere` (Phase 7). All derived values (surface gravity, escape velocity, orbital elements) computed from `mu` in Core, never stored.

## 7. Unity integration — `Assets/Plugins/OpenKSP/Runtime`

- `FloatingOriginSystem` (singleton; `RequireComponent(FixedUpdateLoop)`): owns `FloatingOrigin`, drives `TimeWarpController`, exposes `ToLocal/ToGlobal`.
- `PhysicsProxy`: MonoBehaviour + Rigidbody implementing §3 ordering; holds `GlobalRigidBodyState`.
- `SolarSystemStub` (editor scene): Sun + Earth loaded from `planets.json`; initial ship; HUD shows global position (full-precision doubles), mapping residual, drift-vs-reference, warp rate. Lets the team eyeball the bridge without Phase 2.
- **URP note (resolved, no custom shaders needed):** camera transform is set per frame from the global layer through `ToLocal`; all rendered geometry stays inside the local frame, so depth is consistent. No floating-origin visual corrections in Phase 1; Phase 13 per-body culling is the first place it matters.
- **Compatibility:** Unity 6.0.0f1 / URP 17.6.0; only stable MonoBehaviour + `MovePosition` APIs.

## 8. Deterministic test plan (xUnit — `dotnet test`, headless, no Unity)

| # | Test | Acceptance |
|---|---|---|
| T1 | `D3` round-trip `ToFloat`/`FromFloat` | per-conversion relative error < 1e-6 (covers 1 ulp ≈ 1.19e-7) |
| T2 | Single-body gravity vs closed-form `GM/r²` | rel. error < 1e-9 |
| T3 | N-body sum (3 bodies) vs independent pairwise sum | < 1e-9 |
| T4 | Core-only RK4, circular orbit r=7.35e6 m around μ_earth, dt=1/1200 s, 1 year, vs analytic Kepler position | max radial residual < 100 m. **Core integrator only — no proxy involved.** ~8.6e3 steps |
| T5 | Energy conservation, 2-body, dt=1/1200 s, 10⁶ steps, `TotalEnergy() = Σ½mᵢvᵢ² − Σᵢ<ⱼ Gmᵢmⱼ/rᵢⱼ` | ΔE/E < 1e-6 |
| T6 | Origin-slide round-trip (local → slide → global) | identity within 1e-6 relative |
| T7 | Warp: 12 variable frames at warp=8 → Core advanced exactly dt·8 | step count exact |
| T8 | KeplerianElements state→elements→state round-trip | Δpos < 1e-3 m |
| T9 | Determinism: two identical runs → bit-identical final snapshot | exact equality |
| T10 | Origin crossing: body near local-radius threshold; small origin slides cross IN/OUT; composition identity ≤ 1e-6 every step; canonical state continuous (no jumps beyond integrator error) | pass |

**Bug-tester gate:** T1–T10 all pass; zero compiler warnings under `-warnaserror`; T10 present in the suite.

## 9. Energy accounting (T5 dependency)

`WorldState.TotalEnergy()`: `Σᵢ ½·mᵢ·|vᵢ|² − Σᵢ<ⱼ G·mᵢ·mⱼ / |rᵢ − rⱼ|`.
Excluded and documented: rotational kinetic energy, gravitational self-energy, internal energy. T5 applies to the point-mass system; rotational states exist but are not energy-accounted in Phase 1.

## 10. Delivery checklist (this iteration)

- [ ] `OpenKSP.Core` compiles under .NET 8, zero UnityEngine refs, `-warnaserror` clean
- [ ] T1–T10 pass via `dotnet test` on this host
- [ ] Unity-side MonoBehaviours written (no editor on this host; code must be Unity 6.0.0f1 / URP 17.6 compatible)
- [ ] `planets.json` + `CBodyLoader` per §6
- [ ] `WorldSnapshot` / `ToSnapshot` / `FromSnapshot` present
- [ ] Reviewer sign-off on API surface — **obtained** (v1.1 addresses all 10 review items)

## 11. Explicit non-goals (Phase 1)

No orbital UI, ship model, mod config parser, networking, or multiplayer. Just the double-precision world, localized proxy, and tests. Phases 2+ consume these APIs.
