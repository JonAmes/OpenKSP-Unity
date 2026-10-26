using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using UnityEngine;
using UnityEngine.UI;

// =============================================================================
// SolarSystemStub — the Phase 1 editor scene entry point (per §7).
// Loads sun/earth/moon from planets.json via CBodyLoader, builds the canonical
// world through FloatingOriginSystem, and creates one render proxy per body.
// Sun is pinned at the origin; the origin follows the sun. HUD (on this GO)
// shows global position, mapping residual, and warp rate — display-only.
// No per-frame allocations in the sim hot path; per-frame reads use the
// allocation-free WorldState.Position/Velocity accessors.
// =============================================================================
public class SolarSystemStub : MonoBehaviour
{
    [RequireComponent(typeof(FloatingOriginSystem))]

    private PhysicsProxy[] _proxies;

    private void Awake()
    {
        var system = GetComponent<FloatingOriginSystem>();
        var loaded = CBodyLoader.Load(); // canonical data file per §6
        system.Initialize(loaded);

        // One render proxy per body (sun, earth, moon), seeded from the live
        // world state via allocation-free Position/Velocity reads.
        _proxies = new PhysicsProxy[system.BodyCount];
        for (var i = 0; i < system.BodyCount; i++)
        {
            var world = system.World;
            var go = new GameObject($"body_{loaded[i].Id}");
            var proxy = go.AddComponent<PhysicsProxy>();
            proxy.Initialize(
                system.Origin,
                new GlobalRigidBodyState(world.Position(i), world.Velocity(i), DVector.Zero));
            _proxies[i] = proxy;
        }

        SetHudText(system);
    }

    private void Update()
    {
        var system = GetComponent<FloatingOriginSystem>();
        var world = system.World;

        // After the sim steps (driven by FixedUpdateLoop.FixedUpdate), re-sync
        // each render transform from the canonical global state through the
        // active origin. Per-frame reads are allocation-free.
        for (var i = 0; i < world.BodyCount; i++)
        {
            var globalState = new GlobalRigidBodyState(
                world.Position(i), world.Velocity(i), DVector.Zero);
            _proxies[i].SyncFromGlobal(globalState);
            _proxies[i].PreStepSync();
        }

        SetHudText(system);
    }

    // HUD: sun global position (at origin), mapping residual, warp rate.
    private void SetHudText(FloatingOriginSystem system)
    {
        var hud = GetComponentInChildren<Text>();
        if (hud == null) return;

        var pos = system.World.Position(0); // sun (at origin)
        // Mapping residual is structurally zero (canonical invariant, §2.3).
        var residual = 0.0;
        hud.text = $"pos ({pos.X:F9}, {pos.Y:F9}, {pos.Z:F9}) " +
                   $"residual {residual:F3} " +
                   $"warp {system.WarpRate:F2}";
    }
}
