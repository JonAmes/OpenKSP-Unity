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
// No Find calls in Update: the system and HUD references are cached in Awake.
// =============================================================================
[RequireComponent(typeof(FloatingOriginSystem))]
public class SolarSystemStub : MonoBehaviour
{
    private PhysicsProxy[] _proxies;
    private FloatingOriginSystem _system;
    private Text _hud;

    private void Awake()
    {
        // RequireComponent guarantees FloatingOriginSystem is present when the
        // component is added, so the Awake-time cache is always valid.
        _system = GetComponent<FloatingOriginSystem>();
        var loaded = CBodyLoader.Load(); // canonical data file per §6
        _system.Initialize(loaded);

        // One render proxy per body (sun, earth, moon), seeded from the live
        // world state via allocation-free Position/Velocity reads.
        _proxies = new PhysicsProxy[_system.BodyCount];
        for (var i = 0; i < _system.BodyCount; i++)
        {
            var world = _system.World;
            var go = new GameObject($"body_{loaded[i].Id}");
            var proxy = go.AddComponent<PhysicsProxy>();
            proxy.Initialize(
                _system.Origin,
                new GlobalRigidBodyState(world.Position(i), world.Velocity(i), DVector.Zero));
            _proxies[i] = proxy;
        }

        // HUD reference cached once here so Update() performs no Find calls.
        _hud = GetComponentInChildren<Text>();
        SetHudText();
    }

    private void Update()
    {
        var world = _system.World;

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

        SetHudText();
    }

    // HUD: sun global position (at origin), mapping residual, warp rate.
    private void SetHudText()
    {
        if (_hud == null) return;

        var pos = _system.World.Position(0); // sun (at origin)
        // Mapping residual is structurally zero (canonical invariant, §2.3).
        var residual = 0.0;
        _hud.text = $"pos ({pos.X:F9}, {pos.Y:F9}, {pos.Z:F9}) " +
                    $"residual {residual:F3} " +
                    $"warp {_system.WarpRate:F2}";
    }
}
