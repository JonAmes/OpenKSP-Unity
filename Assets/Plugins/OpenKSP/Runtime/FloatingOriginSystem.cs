using System.Collections.Generic;
using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using OpenKSP.Core.Time;
using OpenKSP.Core.World;
using UnityEngine;

// =============================================================================
// FloatingOriginSystem — the Phase 1 Unity bridge singleton (per §7).
// Owns the canonical WorldState, the TimeWarpController, and the FloatingOrigin.
// Drives the sim at a fixed Core timestep independent of frame rate (per §3).
// The sim advances in whole fixed dt steps only — never mid-step.
// Allocation-free hot path: Advance reads only pre-allocated WorldState arrays
// (no Bodies() calls, no LINQ, no boxing).
// =============================================================================
public class FloatingOriginSystem : MonoBehaviour
{
    [RequireComponent(typeof(FixedUpdateLoop))]

    // Core fixed timestep (s): dt = 1/1200 per §3 — independent of frame rate.
    public const double FixedDelta = 1.0 / 1200.0;

    private WorldState _world;
    private TimeWarpController _warp;
    private FloatingOrigin _origin;

    // Canonical state (read live per-body via allocation-free accessors).
    public WorldState World => _world;
    public FloatingOrigin Origin => _origin;
    public double WarpRate => _warp.WarpRate;
    public int BodyCount => _world.BodyCount;

    // Set the warp rate; applies at a step boundary (never mid-step, per §3).
    public void SetWarpRate(double warpRate) => _warp.SetWarpRate(warpRate);

    // global -> local (float) pass-through to the active origin.
    public F3 ToLocal(D3 global) => _origin.ToLocal(global);

    // local -> global pass-through to the active origin.
    public D3 ToGlobal(F3 local) => _origin.ToGlobal(local);

    // Slide the floating origin to a global target (per §3 step 7).
    public void SlideOrigin(D3 target) => _origin = _origin.SlideTo(target);

    // Accumulate real elapsed time into whole fixed steps and run them.
    // Returns the number of Core steps completed this frame (per T7 contract).
    public int Advance(float realDt)
    {
        var steps = _warp.Accumulate(realDt);
        for (var i = 0; i < steps; i++)
            _world.Step(FixedDelta);
        return steps;
    }

    // Seed the canonical sim once (init only). Bodies are loaded from the real
    // data file (CBodyLoader); initial positions/velocities are derived as
    // circular orbits. Sun pinned at origin; origin = FloatingOrigin(sun).
    public void Initialize(IEnumerable<CBody> loadedBodies)
    {
        // Materialize the loaded bodies into a pre-allocated array (init only;
        // CBodyLoader returns an immutable list, so the canonical array is ours).
        var list = new List<CBody>(loadedBodies);
        if (list.Count < 3)
            throw new ArgumentException("planets.json must define sun, earth, and moon.");

        var bodies = new CBody[list.Count];

        // Sun pinned at the origin, zero velocity (loadedBodies[0]).
        bodies[0] = new CBody(
            list[0].Id, list[0].Name,
            D3.Zero, DVector.Zero,
            list[0].Radius, list[0].Mu3PerS2);

        // Earth: circular orbit of radius 1 AU around the sun, velocity in +y.
        bodies[1] = CircularOrbit(list[1], bodies[0], EarthOrbitRadius, +1);

        // Moon: circular orbit of radius 1 lunar distance around the earth;
        // absolute velocity = earth velocity + circular tangential around earth.
        bodies[2] = MoonAroundEarth(list[2], bodies[1]);

        _world = new WorldState(bodies);

        // Pin the floating origin on the sun (sun is at the origin); the local
        // (float) zone is one sun radius — bodies inside it are IN.
        _origin = new FloatingOrigin(D3.Zero, list[0].Radius);
        _warp = new TimeWarpController(1.0);
    }

    // ---- Initial-state seeding constants (canonical SI) ----------------------
    private const double EarthOrbitRadius = 1.496e11;
    private const double MoonOrbitRadius = 3.844e8;

    // Place `body` at distance `radius` along +x from `center`, with circular
    // orbital speed sqrt(mu_center / radius) in dir*(+y).
    private static CBody CircularOrbit(CBody body, CBody center, double radius, int dir)
    {
        var v = System.Math.Sqrt(center.Mu3PerS2 / radius);
        return new CBody(
            body.Id, body.Name,
            new D3(radius, 0.0, 0.0),
            new DVector(0.0, dir * v, 0.0),
            body.Radius, body.Mu3PerS2);
    }

    // Moon seeded relative to the earth: at earth +x MoonOrbitRadius; absolute
    // velocity = earth velocity + circular tangential around the earth in +y.
    private static CBody MoonAroundEarth(CBody moon, CBody earth)
    {
        var relPos = new D3(MoonOrbitRadius, 0.0, 0.0);
        var vM = System.Math.Sqrt(earth.Mu3PerS2 / MoonOrbitRadius);
        var pos = earth.Position + relPos;
        var vel = earth.Velocity + new DVector(0.0, vM, 0.0);
        return new CBody(moon.Id, moon.Name, pos, vel, moon.Radius, moon.Mu3PerS2);
    }
}

// =============================================================================
// FixedUpdateLoop — the fixed-update cadence driven from Unity (companion to
// FloatingOriginSystem, which requires this component). Exposes a fixed update
// that forwards Unity's dt into the owning system's Advance. No Core math here;
// the actual stepping lives in FloatingOriginSystem.Advance.
// =============================================================================
public class FixedUpdateLoop : MonoBehaviour
{
    private void FixedUpdate(float dt)
    {
        var system = GetComponent<FloatingOriginSystem>();
        system.Advance(dt);
    }
}
