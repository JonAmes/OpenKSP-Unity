# Using the OpenKSP Sim — a hands-on guide

Written 2026-10-26, against the code that actually exists. This teaches the *model* first, then shows the
exact API you can call, then shows how to drive the sim in code and how the Unity side is wired up.
Everything here is grounded in the current source (`Code/OpenKSP.Core`, `Assets/Plugins/OpenKSP`).

---

## 1. The mental model (read this first)

The sim is split into **two layers that never know about each other**:

| Layer | Location | Rule |
|---|---|---|
| **Core (the truth)** | `Code/OpenKSP.Core` — pure C#, **no `UnityEngine`** | All state lives in *global* `double` space. RK4 integration, energy, snapshots. Deterministic. |
| **Bridge (the display)** | `Assets/Plugins/OpenKSP/Runtime` — MonoBehaviours | *Only renders* the Core state in a local `float` frame. Never integrates physics. |

The one idea that keeps it correct is the **floating origin**:

- Gravity is computed in the **global double frame** — that is the real, physical position.
- Every rendered body is a Unity `Rigidbody` whose transform holds a **local `float` value**: the global position *subtracted by the origin*, cast to float.
- A body is **IN** the origin's radius (its float zone is meaningful) or **OUT** (its float value would blow up, so it is advanced purely by Core and re-factored through the origin each re-sync).
- When the camera/body of interest moves, the origin **slides** and the whole local frame re-anchors (via `SlideTo` / `SlideOrigin`).

Because velocities are *deltas* (not a position), the origin does **not** offset them — the local velocity is just the global velocity cast to float. That is why the local frame can "follow" a body without corrupting its motion.

Think of it as: **Core says where everything really is (doubles); the bridge decides where to *look from* (the origin) and draws everything relative to that in float.**

---

## 2. The two namespaces you'll actually call

### Core — `OpenKSP.Core.*`

| Namespace | Types |
|---|---|
| `OpenKSP.Core.Math` | `D3` (double vec), `F3` (float vec), `Coordinates`, `DTime` |
| `OpenKSP.Core.Celestial` | `CBody`, `DVector`, `CBodyLoader`, `GravityField`, `WorldState`, `WorldSnapshot` |
| `OpenKSP.Core.Orbit` | `OrbitPropagator`, `KeplerianElements`, `OrbitalElements` |
| `OpenKSP.Core.World` | `FloatingOrigin` |
| `OpenKSP.Core.Time` | `TimeWarpController` |

### Bridge — Unity MonoBehaviours

`FloatingOriginSystem`, `FixedUpdateLoop`, `PhysicsProxy`, `SolarSystemStub` (see §5).

---

## 3. The Core API, in detail

### 3.1 Vectors — `D3` / `F3` / `DVector`

```csharp
// D3          — immutable double vector (the global frame)
var d = D3.Zero;                  // (0,0,0)
var p = new D3(1.0, 2.0, 3.0);
var q = p + r;                    // add/subtract, *scalar, dot( * ), Cross
var lerp = p.Lerp(0.5, r);
float len = ...;                  // DVector.Length(); D3 is magnitude-free by design
// F3 — immutable float vector (the local frame). F3.Dot(a,b)
var f = new F3(1f, 2f, 3f);
```

`DVector` (a double "rate") is used for velocity; it has a `With` sub-struct for implicit widening. Equality on `D3`/`DVector` is exact (bit-level) — which is what makes snapshots compare equal.

### 3.2 `CBody` — a body of mass

```csharp
var c = new CBody("earth", "Earth", pos, vel, 6.371e6, 3.986004418e14);
c.Mu3PerS2;      // GM, m³/s² — the only gravity number that matters
c.Mass;          // derived: Mu3PerS2 / G   (never hand-store G)
```

Two things to know:
- **`mu` is the fundamental field** — mass is *derived*, so you never hard-code `G`.
- Position/velocity start at origin when loaded from JSON; the sim **seeds** them into orbits at world-state init (see §5.3).
- Bodies are compared by **`Id`**, not by value.

### 3.3 `CBodyLoader` — the real data file

```csharp
// Load the §6-schema file you already have:
var bodies = CBodyLoader.Load();            // default: Assets/StreamingAssets/Planets/planets.json
var bodies = CBodyLoader.Load("/abs/path.json");
var json   = File.ReadAllText(...);
var parsed = CBodyLoader.Parse(json);       // IReadOnlyList<CBody>, throws InvalidDataException on bad schema
var earth  = CBodyLoader.GetById(bodies, "earth");
```

`planets.json` schema (what's there now):

```json
{
  "units": { "length": "m", "mass": "kg", "time": "s" },
  "bodies": [
    { "id": "sun",   "name": "Sun",   "mass": 1.989e30, "radius": 6.96e8,  "mu": 1.32712440018e20, "parent": null    },
    { "id": "earth", "name": "Earth", "mass": 5.972e24, "radius": 6.371e6, "mu": 3.986004418e14,  "parent": "sun"   },
    { "id": "moon",  "name": "Moon",  "mass": 7.342e22, "radius": 1.7374e6,"mu": 4.9048695e12,   "parent": "earth" }
  ]
}
```

Required per body: `id, name, mass, radius, mu`. `parent` is future (Phase 2 hierarchy). `mass` and `mu` are both stored but **mu is what drives physics**; `mass` is informational.

### 3.4 `GravityField` — N-body acceleration

```csharp
var field = new GravityField(bodies);
DVector acc = field.Acceleration(posIn);     // net acceleration at a point (DVector in)
DVector acc = GravityField.BodyAccel(posArr, muArr, i); // scalar-core pair loop (no allocation)
DVector acc = GravityField.TwoBodyAcc(b1, b2);         // convenience
double  r   = field.DistanceFromBody(body, posIn);
```

`Acceleration` is the *sum of every body's pull* — this is what an RK4 step feeds.

### 3.5 `OrbitPropagator` — RK4 stepping (the actual integrator)

```csharp
var prop = new OrbitPropagator(bodies);
// Advance the *state arrays in place* — no allocation:
prop.Step(dt, pos, vel, mu);   // D3[] pos, DVector[] vel, double[] mu
```

You rarely call this directly — `WorldState.Step` wraps it. But it's the thing that "does the physics." RK4 on `GravityField.Acceleration`.

### 3.6 `WorldState` — the canonical state

This is the object you hold. It owns the positions, velocities, sim time, and the propagator.

```csharp
var world = new WorldState(bodies);   // seed happens BEFORE this (see 5.3)
world.BodyCount;                       // 3
world.SimTimeSeconds;                  // advances with each Step
world.Position(i);                     // allocation-free D3 per body
world.Velocity(i);                     // allocation-free DVector per body
world.Bodies;                         // CBody[] snapshot (ALLOCS — use Position/Velocity for hot reads)
world.Step(1.0 / 1200.0);             // one fixed RK4 step
double ke = world.TotalEnergy();       // KE + PE per §9 (no rotation)
var snap = world.ToSnapshot();          // bit-exact immutable WorldSnapshot
world.FromSnapshot(snap);              // the ONLY extension seam (replay / lock-free later)
```

**The hot-path rule (and why the tests pass determinism):** step with a *fixed* `dt`, read per-body via `Position(i)`/`Velocity(i)` (no `Bodies()`), no LINQ, no boxing → identical bits every run. `ToSnapshot`/`FromSnapshot` are the seam for Phase 4.

### 3.7 `FloatingOrigin` — the local frame

```csharp
var origin = new FloatingOrigin(sunPos, sunRadius);  // origin = D3, localRadius (IN-zone)
F3  local   = origin.ToLocal(globalPos);   // double → float, relative to origin
D3  global  = origin.ToGlobal(local);       // float → double, back out
var origin2 = origin.SlideTo(newTarget);   // re-anchor the frame (new origin, same radius)
double dist  = origin.DistanceFromOrigin(globalPos);  // for the IN/OUT test
```

`ToLocal = (global − origin)*Scale`, `ToGlobal = origin + local*Scale`, `Scale == 1` for now. `SlideTo` *returns* a new origin (the old one is a value, so it's just re-anchored).

### 3.8 `TimeWarpController` — fixed steps, warp at boundaries

```csharp
var warp = new TimeWarpController(1.0);          // warp 1×
warp.SetWarpRate(8.0);                          // applies at a step boundary (never mid-step)
var steps = warp.Accumulate(realDtSec);       // returns whole fixed steps to run this frame
warp.WarpRate;  warp.CompletedSteps;  warp.PendingSteps;  warp.TotalCompletedSteps;
```

`Accumulate(realDt)` accumulates *real* seconds and hands back an integer number of **fixed** Core steps. This is exactly what makes "8× warp, 12 frames → exact step count" (T7) deterministic.

### 3.9 `KeplerianElements` — state ↔ elements (Phase 3)

```csharp
OrbitalElements el = KeplerianElements.FromState(r, v, mu);   // double-space
D3  pos = KeplerianElements.ToState(el, mu);                  // back to state
DVector vel = KeplerianElements.StateVelocity(el, mu);
// OrbitalElements: A, Ecc, Inc, Mean, Omega, W  (all SI / rad)
```

Round-trip `FromState`→`ToState` is what T8 checks.

---

## 4. Driving the sim in pure Core (a runnable example)

This is the cleanest way to **use** what was built — no Unity required. It mirrors what the bridge does, but in a `Main`/test body.

```csharp
using OpenKSP.Core.Celestial;
using OpenKSP.Core.Time;
using OpenKSP.Core.World;

const double  AU   = 1.496e11;
const double MoonDist = 3.844e8;
const double dt   = 1.0 / 1200.0;   // Core fixed step (per §3)

// 1) Load the real data file.
var data = CBodyLoader.Load();
var sun  = data[0], earth = data[1], moon = data[2];

// 2) Seed positions/velocities (the loader leaves them at origin).
//    Sun pinned at the origin, zero velocity.
var bodies = new CBody[3];
bodies[0] = new CBody(sun.Id, sun.Name, D3.Zero, DVector.Zero, sun.Radius, sun.Mu3PerS2);

//    Earth: circular orbit of radius 1 AU, speed sqrt(mu_sun/AU), dir +y, pos +x.
bodies[1] = new CBody(earth.Id, earth.Name,
    new D3(AU, 0, 0),
    new DVector(0, System.Math.Sqrt(sun.Mu3PerS2 / AU), 0),
    earth.Radius, earth.Mu3PerS2);

//    Moon: at earth +x MoonDist, abs velocity = earth vel + tangential sqrt(mu_e/MoonDist) in +y.
var vM = System.Math.Sqrt(earth.Mu3PerS2 / MoonDist);
bodies[2] = new CBody(moon.Id, moon.Name,
    new D3(AU + MoonDist, 0, 0),
    new DVector(0, System.Math.Sqrt(sun.Mu3PerS2 / AU) + vM, 0),
    moon.Radius, moon.Mu3PerS2);

// 3) Build the canonical world + origin + warp.
var world  = new WorldState(bodies);
var origin = new FloatingOrigin(D3.Zero, sun.Radius);      // follow the sun
var warp   = new TimeWarpController(8.0);                 // 8×

// 4) Run it. Pass real seconds per frame; the controller hands back whole steps.
double frameDt = 1.0 / 60.0;
for (var frame = 0; frame < 12; frame++)
{
    int steps = warp.Accumulate(frameDt);
    for (int i = 0; i < steps; i++)
        world.Step(dt);

    // What a render proxy would show this frame (local float):
    F3 earthLocal = origin.ToLocal(world.Position(1));
    System.Console.WriteLine($"f{frame}  T={world.SimTimeSeconds:F3}s  " +
                             $"earth@({world.Position(1).X:F3},{world.Position(1).Y:F3},{world.Position(1).Z:F3})  " +
                             $"local=({earthLocal.X:F3},{earthLocal.Y:F3},{earthLocal.Z:F3})  E={world.TotalEnergy():F3}");
}
```

Notes:
- **`TotalEnergy()`** is the conservation check — it should sit at nearly a constant (KE + PE). It's what T5 runs over 10⁶ steps.
- This whole block works with **no Unity** — run it in a console app or a test.
- To re-anchor, call `origin = origin.SlideTo(world.Position(1));` and recompute locals.

---

## 5. The Unity side (what's in the scene)

These are the four MonoBehaviours that turn the Core above into something you can *watch*.

### 5.1 `FloatingOriginSystem` — the singleton the scene is built on

Add this one component to your scene root. It **owns the whole sim**:

```csharp
[RequireComponent(typeof(FixedUpdateLoop))]
public class FloatingOriginSystem : MonoBehaviour
{
    public const double FixedDelta = 1.0 / 1200.0;   // dt, frame-rate independent
    public WorldState      World;      // the canonical state
    public FloatingOrigin  Origin;     // the frame you're looking from
    public double          WarpRate;
    public int            BodyCount;

    public void Initialize(IEnumerable<CBody> loadedBodies);  // seed orbits (see 5.3)
    public int  Advance(float realDt);        // whole fixed steps this frame
    public void SetWarpRate(double rate);       // at a step boundary
    public F3   ToLocal(D3 global);           // double → float
    public D3   ToGlobal(F3 local);           // float → double
    public void SlideOrigin(D3 target);        // re-anchor the frame
}
```

### 5.2 `FixedUpdateLoop` — the fixed cadence driver

Companion component (`RequireComponent` makes it required). It forwards Unity's `FixedUpdate` `dt` into the system's `Advance`. **Core never reads `Time.deltaTime` directly** — that's what keeps stepping frame-rate independent.

### 5.3 The scene entry: `SolarSystemStub`

This is the thing you add to a GameObject in the Editor. It does all the wiring once, in `Awake`:

```csharp
[RequireComponent(typeof(FloatingOriginSystem))]
public class SolarSystemStub : MonoBehaviour
{
    Awake():
        _system = GetComponent<FloatingOriginSystem>();
        // Explicit StreamingAssets path (the review MAJOR fix — AppDomain.BaseDirectory
        // is NOT the repo under Unity, so the default path would 404).
        var path = Path.Combine(Application.dataPath, "StreamingAssets", "Planets", "planets.json");
        _system.Initialize(CBodyLoader.Load(path));
        // One PhysicsProxy per body, seeded from the LIVE world state:
        for each body i → go = new GameObject("body_" + id)
                         proxy = go.AddComponent<PhysicsProxy>();
                         proxy.Initialize(_system.Origin,
                                          new GlobalRigidBodyState(world.Position(i),
                                                                  world.Velocity(i), DVector.Zero));
        _hud = GetComponentInChildren<Text>();   // HUD cached here, no Find in Update
}

Update(): for each body → proxy.SyncFromGlobal(live state); proxy.PreStepSync();
          then refresh the HUD (global pos, mapping residual, warp rate).
```

### 5.4 `PhysicsProxy` — the sim→render bridge

One `PhysicsProxy` per body. It implements **§3 ordering**:

```csharp
public struct GlobalRigidBodyState   { D3 GlobalPosition; DVector GlobalVelocity; DVector GlobalAngularVelocity; }

[RequireComponent(typeof(Rigidbody))]
public class PhysicsProxy : MonoBehaviour
{
    public void Initialize(FloatingOrigin origin, GlobalRigidBodyState state);
    public void PreStepSync();          // §3 steps 1–3: classify IN/OUT, MovePosition (IN),
                                        //  set local velocities, make OUT kinematic+discrete
    public GlobalRigidBodyState ReadBack();  // §3 step 5: IN read-back through the origin
    public void SyncFromGlobal(GlobalRigidBodyState state);  // §3 step 7 re-slide
}
```

**Velocity is a delta** — so `ReadBack`/`PreStepSync` do *not* offset velocity by the origin; only position is mapped through it.

---

## 6. How to verify it (you don't need the Editor)

Two green checks, both run on this host:

```bash
# Core gate (the real gate per AGENTS.md):
dotnet build Code/OpenKSP.Core -warnaserror          # 0 warn / 0 err
dotnet test  Code/OpenKSP.Core.Tests -warnaserror     # Passed 19, Failed 0, Skipped 0

# Unity headless compile (resolves the prebuilt DLL + compiles all MonoBehaviours):
~/.local/share/unity… -batchmode -quit -nographics -projectPath /home/jonames/Projects/OpenKSP-Unity …
# → exit 0
```

The **definitive check** is that Unity's own Mono can *JIT* the Core DLL on the exact `Awake` path. `-batchmode -quit` only compiles — it does not run `Awake` — so to prove the original `TypeLoadException` is gone, rebuild the Core as `netstandard2.0`, then run Unity's bundled `mono 6.13.0` on a small net48 harness that calls `CBodyLoader.Load("…/planets.json")` (which reads `System.IO.File`, exactly what `SolarSystemStub.Awake` does). Result: **3 bodies loaded with the correct `mu` values, exit 0, no `TypeLoadException`.**

The test classes (`T1`–`T10`) are what "done" means — they exercise every claim in §9 (energy/Kepler determinism), and T10 is the origin-crossing IN/OUT composition identity. **Don't say it works unless the Core gate, the Unity headless compile, and the Mono JIT check all pass.**

---

## 7. Reference the Core DLL from Unity

Core is consumed from Unity as the **prebuilt** `OpenKSP.Core.dll` (referenced from `Assets/Plugins/OpenKSP/`, no asmdef). That's why everything inside Unity compiles against the real Core types. If you change Core, **rebuild the DLL** (`dotnet build Code/OpenKSP.Core -c Release`) and copy it back to `Assets/Plugins/OpenKSP/OpenKSP.Core.dll`.

The `.csproj` targets `netstandard2.0` and `LangVersion 9` (C# 9 only — no `with` expression).

**Why netstandard2.0 and not net8.0 — this is the fix for the `TypeLoadException`.**
Unity loads the Core DLL through its bundled Mono runtime (6.13.0, mscorlib‑based facades). A **net8.0** build emits IL that references `System.IO.File` (and other framework types) against the **`System.Runtime, Version=8.0.0.0`** assembly — an assembly Unity's Mono cannot resolve at **JIT time**. That is exactly the original crash:

```
TypeLoadException: Could not resolve type with token 0100002c
  (expected class 'System.IO.File' in assembly
   'System.Runtime, Version=8.0.0.0, Culture=neutral,
   PublicKeyToken=b03f5f7f11d50a3a')
SolarSystemStub.Awake ()
```

Retargeting to **netstandard2.0** makes the DLL reference only mscorlib facades Unity *can* resolve (`System.IO.File`, `System.Text.*`) — that is why the retarget fixed the crash.

This also forced the Core to avoid .NET 6+ features Unity's Mono doesn't surface: no `System.Text.Json` (hand‑rolled JSON parser in `CBodyLoader`), no `init;` on readonly structs, no `System.Collections.Immutable`/`IImmutableList`, no `System.HashCode`/`HashCode.Combine`, and no `Math.Clamp` (inlined in `KeplerianElements`). All hashing now runs through `Hash32`/`HashAccum` (`Math/Hash32.cs`), built on `BitConverter.DoubleToInt64Bits`, so `Equals ⇒ equal hash` and the bit‑exact `WorldSnapshot` invariant still hold.

---

## 8. What is *not* wired yet (honest list)

These are the real gaps — small, and exactly how to finish them:

1. **No live playback on this host** — there is no Editor scene here. The sim loop runs, but the *render + physics-proxy read-back* pass hasn't been run live. Finish by opening the project in a licensed Editor and running the `SolarSystemStub` scene.
2. **Read-back not in the live loop** — `PhysicsProxy.ReadBack` and the post-step origin slide (`SlideTo`/`SlideOrigin`) are *implemented* but `SolarSystemStub.Update` currently only drives `PreStepSync`. Wire step 5/step 7 into `Update` to fully close §3 ordering.
3. **HUD drift not computed** — §2.3 wants *drift vs the double reference*; the HUD shows the structural residual + warp rate only. Add the residual term in `SetHudText`.
4. **`WorldState.Bodies` allocates** — it returns a `CBody[]` copy each call. Use `Position(i)`/`Velocity(i)` in the hot path (the tests do).
5. **`Coordinates.Scale`** is a stub (1.0) — the origin-based scaling is "set from origin-based mapping" later.
6. **KeplerianElements** (Phase 3) and **hierarchy `parent`** (Phase 2) are present as seams, not yet used by the live sim.

**Resolved during this iteration (not an open gap):** the net8.0 → netstandard2.0 retarget that eliminated the `System.Runtime, Version=8.0.0.0` `TypeLoadException` in `SolarSystemStub.Awake` is done and verified — `dotnet build` 0 warn / 0 err, `dotnet test` 19/19, Unity headless compile exit 0, and Unity's own Mono JIT‑compiled the exact `Awake` load path (3 bodies, correct `mu`).

---

## 9. Quick API cheat-sheet

| Do | Call |
|---|---|
| Load bodies | `CBodyLoader.Load()` / `.Load(path)` / `.Parse(json)` / `.GetById(list, id)` |
| Seed orbits | build `CBody[]` (positions/velocities) → `new WorldState(bodies)` |
| Step the sim | `world.Step(1.0/1200.0)` |
| Read a body (no alloc) | `world.Position(i)`, `world.Velocity(i)` |
| Whole steps this frame | `warp.Accumulate(realDt)` |
| Set warp | `warp.SetWarpRate(8.0)` |
| Global ↔ local | `origin.ToLocal(g)`, `origin.ToGlobal(l)`, `system.ToLocal/ToGlobal` |
| Re-anchor frame | `origin.SlideTo(target)` / `system.SlideOrigin(target)` |
| Energy check | `world.TotalEnergy()` |
| Snapshot seam | `world.ToSnapshot()` / `world.FromSnapshot(snap)` |
| Orbit state ↔ elements | `KeplerianElements.FromState(r,v,mu)` / `.ToState(el,mu)` / `.StateVelocity(el,mu)` |
| Unity: step the sim | `FixedUpdateLoop` → `system.Advance(dt)` |
| Unity: render a body | `PhysicsProxy.PreStepSync()` / `.ReadBack()` / `.SyncFromGlobal()` |

That's the whole surface. The sim is: **load → seed → `Step` in whole fixed sub-steps → render locally through a slide-able origin.** Everything else is seams for Phases 2–4.
