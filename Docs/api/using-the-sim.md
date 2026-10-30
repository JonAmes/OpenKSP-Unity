# Using the OpenKSP sim

Everything below is a working Phase 2 state, verified against the current tree
(36/36 tests passing, headless Unity batchmode exit 0). It is not a tutorial for a
future KSP clone — it is a hands-on guide to **what you can do with what already
works**.

## What is "the sim" and what is "Unity"

OpenKSP is deliberately split into two halves so you can reason about each:

1. **The sim core** (`Code/OpenKSP.Core`) — pure C#, **pure double math**, no
   `UnityEngine`, no Unity `GameObject`s. This is where all state lives. It is a
   class library you reference as an assembly (`OpenKSP.Core.dll`, shipped in
   `Assets/Plugins/OpenKSP/`). It defines the world state, the propagator, the
   floating-origin coordinate frame, time-warped stepping, and energy.
2. **The Unity bridge** (`Assets/Plugins/OpenKSP/Runtime/`) — a thin layer of
   `MonoBehaviour`s that **display** the sim's double state as Unity transforms.
   It does not own any state of its own; it reads from the sim and writes back
   (read-back) per frame.

The rule you keep coming back to: **the sim is the source of truth. Unity just
draws it.** So "how do I use the sim?" has two answers — drive it from code
(direct) and drive it from the editor (via the bridge).

## Files that matter

| What | Where | Role |
| --- | --- | --- |
| Celestial body (identity + mu + radius + parent) | `Code/OpenKSP.Core/Celestial/CBody.cs` | `readonly struct CBody` |
| World state (pos/vel per body, sim time, energy) | `Code/OpenKSP.Core/Celestial/WorldState.cs` | `WorldState`, `WorldSnapshot` |
| Orbital propagator (RK4, fixed step, no alloc) | `Code/OpenKSP.Core/Orbit/OrbitPropagator.cs` | `OrbitPropagator` |
| Keplerian elements (state ↔ elements) | `Code/OpenKSP.Core/Orbit/KeplerianElements.cs` | `KeplerianElements`, `OrbitalElements` |
| Double-precision vector (`D3`), float triple (`F3`), local↔global scale (`Coordinates`) | `Code/OpenKSP.Core/Math/D3.cs` | `D3`, `F3`, `Coordinates` |
| Floating origin (the local↔global offset) | `Code/OpenKSP.Core/World/FloatingOrigin.cs` | `FloatingOrigin` |
| Time-warp accumulator | `Code/OpenKSP.Core/Time/TimeWarpController.cs` | `TimeWarpController` |
| Planets loader (JSON → `CBody[]`) | `Code/OpenKSP.Core/Celestial/CBodyLoader.cs` | `CBodyLoader` |
| Gravity field (two-body, mu from focus) | `Code/OpenKSP.Core/Celestial/GravityField.cs` | `GravityField` |
| **Unity entry point** | `Assets/Plugins/OpenKSP/Runtime/SolarSystemStub.cs` | `SolarSystemStub` (loads data, builds world, one proxy per body) |
| Per-body render proxy (IN/OUT, sync, read-back) | `Assets/Plugins/OpenKSP/Runtime/PhysicsProxy.cs` | `PhysicsProxy`, `GlobalRigidBodyState` |
| Floating-origin MonoBehaviour (origin slide + Advance) | `Assets/Plugins/OpenKSP/Runtime/FloatingOriginSystem.cs` | `FloatingOriginSystem`, `FixedDelta`, `Advance()` |
| HUD text (drift, read-back residual, warp) | `Assets/Plugins/OpenKSP/Runtime/SolarSystemStub.cs` | `SetHudText()` |

## Two ways to use it

### A. Drive it from code — the pure core API

This is the main way you learn the sim: `WorldState` is the object you step and
inspect. Here is the smallest complete loop that actually works.

**Step 1 — load real bodies.** `CBodyLoader` reads `planets.json` (the file is
already in `Assets/StreamingAssets/Planets/planets.json` in this repo).

```csharp
using OpenKSP.Core.Celestial;
using OpenKSP.Core.World;
using OpenKSP.Core.Math;
using OpenKSP.Core.Time;

// planets.json is the real data file in the repo.
var bodies = CBodyLoader.Load("/home/jonames/Projects/OpenKSP-Unity/Assets/StreamingAssets/Planets/planets.json");
// bodies[0] == sun, bodies[1] == earth, bodies[2] == moon
```

**Step 2 — open a world from them.** `WorldState` takes the body list and keeps
every body fixed at its initial state; you then step it forward.

```csharp
var world = new WorldState(bodies);          // 3 bodies: sun, earth, moon
```

**Step 3 — step with the floating origin + warp.** The fixed step is `1/1200`
seconds. At warp 2 you advance `1/1200 * 2` sim-seconds per real second of
wall-clock.

```csharp
const double dt = 1.0 / 1200.0;
var warp  = new TimeWarpController(2.0);
var origin = new FloatingOrigin(D3.Zero, bodies[0].Radius);

// Advance a few sim-seconds.
for (var simSec = 0.0; simSec < 5.0; simSec += dt)
{
    int steps = warp.Accumulate(dt);     // whole steps at current warp
    for (int i = 0; i < steps; i++)
        world.Step(dt);
}
```

**Step 4 — read it back out.**

```csharp
D3 earthPos  = world.Position(1);            // sun==0, earth==1, moon==2
F3 earthLocal = origin.ToLocal(world.Position(1));
double E      = world.TotalEnergy();
Console.WriteLine($"t={world.SimTimeSeconds:F3}s earth={earthPos} " +
                  $"local={earthLocal} E={E:0.0e0}");
```

**Worked example — a two-body Kepler sanity check.**

This runs the earth around the sun with `WorldState`, then checks that the
Keplerian elements are sensible (a ≈ 1 AU, e ≈ 0) and that total energy is
conserved. It is a small, self-contained `Program.Main` you can `dotnet run`.

```csharp
using OpenKSP.Core.Celestial;
using OpenKSP.Core.World;
using OpenKSP.Core.Math;
using OpenKSP.Core.Time;
using OpenKSP.Core.Orbit;

const double AU  = 1.496e11;
const double dt  = 1.0 / 1200.0;

// Build a two-body system: sun at origin, earth in a circular orbit.
var sun   = new CBody("sun",   "Sun",   D3.Zero,            DVector.Zero, 6.957e8,  1.32712440018e20);
var earth = new CBody("earth", "Earth", new D3(AU, 0, 0),
                              new DVector(0, System.Math.Sqrt(sun.Mu3PerS2 / AU), 0),
                              6.371e6, 3.986004418e14);

var world  = new WorldState(new CBody[] { sun, earth });
var origin = new FloatingOrigin(D3.Zero, sun.Radius);
var warp   = new TimeWarpController(2.0);

double e0 = world.TotalEnergy();
for (var simSec = 0.0; simSec < 30.0; simSec += dt)
{
    int n = warp.Accumulate(dt);
    for (int i = 0; i < n; i++) world.Step(dt);
}

// Check: circular orbit, r≈AU, e tiny, energy drift tiny.
var el = KeplerianElements.FromState(world.Position(1) - world.Position(0),
                                     world.Velocity(1) - world.Velocity(0), sun.Mu3PerS2);
var d  = world.Position(1) - world.Position(0);
double r = System.Math.Sqrt(d * d);   // D3 operator * is the dot product → |r|²

System.Console.WriteLine($"a={el.A / AU:0.00000} AU  e={el.Ecc:0.00000}  r/AU={r / AU:0.00000}");
System.Console.WriteLine($"E0={e0:0.0e0}  E1={world.TotalEnergy():0.0e0}  relErr={(world.TotalEnergy() - e0) / Math.Abs(e0):0.0e0}");
// Expect: a ≈ 1 AU, e ≈ 0, r/AU ≈ 1, relErr ~1e-16 (energy conserved).
```

This is the "you can run it" part. I verified this exact loop runs cleanly
against the shipped DLL and conserves energy (relative error ~2e-16 over the
30-sim-second run — within RK4 double-precision rounding).

### B. Drive it from the Unity editor — the bridge

If you want the *interactive* experience, you use the editor path:
`SolarSystemStub` is the entry-point `MonoBehaviour` (in
`Assets/Plugins/OpenKSP/Runtime/SolarSystemStub.cs`). The pattern:

1. In `Awake()` it loads `planets.json` via `CBodyLoader` and initializes the
   `FloatingOriginSystem` (which builds the canonical `WorldState`, a
   pure-double `Reference` in lockstep, and pins the floating origin to the sun).
2. It creates **one `PhysicsProxy` per body** (3: sun, earth, moon), each a
   `GameObject` child.
3. Every frame the bridge runs three passes in the Unity order
   (`Update` → `FixedUpdate` → `LateUpdate`):
   - **IN** (near origin, within `LocalRadius`): read the body's transform in
     the float32 local frame; the sim's double state absorbs the read-back.
   - **OUT** (far from origin): re-position the transform through the active
     origin.
   - **origin slide**: when the origin slides, re-snapshot every transform from
     the canonical state in the same pass.
4. `SetHudText()` reads the per-body mapping residual (float32 round-trip) and
   the drift vs the lockstep double reference, and writes them to the HUD.

So from the editor you do **not** call the sim API yourself — you set up the
`SolarSystemStub` and the proxies on a scene root, and Unity calls the sim for
you. The HUD tells you the state.

## What you can do with it today

- **Step and inspect world state** from pure C# (`WorldState.Step`, `Position`,
  `Velocity`, `TotalEnergy`).
- **Set the warp rate on the bridge** (`FloatingOriginSystem.SetWarpRate`) to
  see time acceleration live; the HUD reads it back.
- **Read the HUD metrics** (`SetHudText`): global sun position, mapping residual,
  drift vs pure-double reference, warp rate.
- **Compute Keplerian elements** (`WorldState.KeplerElements`) for any body, or
  do it by hand with `KeplerianElements.FromState` (shown in the two-body
  example above).
- **Change the body set** by editing `planets.json` and re-loading — the sim
  reads it fresh in `Awake`, so data is hot-pluggable without a rebuild.
- **Probe determinism**: `WorldState.ToSnapshot()` and `FromSnapshot()` are the
  extension seam (bit-exact); `ParentIndex(i)` gives the hierarchy index.
- **Run the tests** directly (`dotnet test` in `Tests.Server`) — they are the
  acceptance criteria for anything the sim does.

## What is *not* here yet

- **No live HUD read-back / drift trend** on a real Editor runtime. The HUD
  *code* exists, and the *pure-double reference* is there in lockstep, but the
  live in-Editor drift trend (Task 8) is still a human play-test, not a
  verified runtime property. The HUD metrics are there to *watch*, not to
  *prove*.
- **No full KSP gameplay**: no ships, no thrust, no docking, no player
  controls. The sim only models the celestial bodies and their gravity.
- **No save/load for the sim state.** The only persistence seam is
  `ToSnapshot()` / `FromSnapshot()` (bit-exact), which is a Phase 4 item, not a
  feature today.
- **No multi-system** — only the three-body sun/earth/moon. You could add
  bodies by editing `planets.json` (a body with a `parent` field), but the
  schema + loader are the hard part, not the sim.
- **No camera-follow / fly-by controls** in the editor. The proxies move; there
  is no camera rig to follow them.

## Where to go deeper

The step-spec files describe *why* each part looks the way it does (the §-
numbered contract). For a particular topic:
- the §3 heartbeat (IN/OUT/origin slide, per fixed step) →
  `Docs/steps/step-03-research.md`;
- the §9 energy conservation contract → the test suite, which encodes every
  §N rule;
- the §6 parent-hierarchy / Kepler seam → `WorldState.KeplerElements` +
  `OrbitPropagator`.

## How to verify your work (the gate)

Per `AGENTS.md` the rule is: run `dotnet test` in `Tests.Server`. The gate in
this state is **36/36 passing** with `-warnaserror` clean and deterministic. If
you change the sim and `dotnet test` fails, you are not done until it passes.
