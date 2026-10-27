using System.IO;
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
//
// §3 heartbeat (per fixed step), all allocation-free:
//   Update ()      -> steps 1-2-3: classify IN/OUT, pre-step snap (IN/OUT).
//   FixedUpdate   -> steps 3-4:   Core RK4 Advance (via FixedUpdateLoop) + Unity step.
//   LateUpdate ()  -> steps 5-7:  IN read-back, OUT advance, origin-slide re-snapshot.
// Update runs before FixedUpdate and LateUpdate after it (Unity's per-frame order),
// so the pre-step snap brackets the Advance and the post-step pass follows it.
// No per-frame allocation: per-frame reads use the allocation-free WorldState
// .Position/.Velocity accessors and WorldState.SetPosition (no Bodies() call).
// No Find calls: the system and HUD references are cached in Awake.
// =============================================================================
[RequireComponent(typeof(FloatingOriginSystem))]
public class SolarSystemStub : MonoBehaviour
{
    private PhysicsProxy[] _proxies;
    private FloatingOriginSystem _system;
    private Text _hud;
    // Cached per-frame HUD metrics buffer; the string assignment to Text is the
    // only per-frame allocation, and the metric math itself is allocation-free
    // (no Bodies() call, no LINQ, no boxing).
    private System.Text.StringBuilder _hudBuffer = new(128);

    private void Awake()
    {
        // RequireComponent guarantees FloatingOriginSystem is present when the
        // component is added, so the Awake-time cache is always valid.
        _system = GetComponent<FloatingOriginSystem>();
        // The data file lives at Assets/StreamingAssets/Planets/planets.json in the
        // project. AppDomain.BaseDirectory is not the repo under Unity, so the
        // bridge resolves the StreamingAssets path explicitly (review MAJOR);
        // Core's dotnet default remains available for tests.
        var planetsPath = Path.Combine(
            Application.dataPath, "StreamingAssets", "Planets", "planets.json");
        var loaded = CBodyLoader.Load(planetsPath);
        _system.Initialize(loaded);

        // One render proxy per body (sun, earth, moon), seeded from the live
        // world state via allocation-free Position/Velocity reads.
        _proxies = new PhysicsProxy[_system.BodyCount];
        var origin = _system.Origin;
        for (var i = 0; i < _system.BodyCount; i++)
        {
            var world = _system.World;
            var go = new GameObject($"body_{loaded[i].Id}");
            var proxy = go.AddComponent<PhysicsProxy>();
            proxy.Initialize(
                origin,
                new GlobalRigidBodyState(world.Position(i), world.Velocity(i), DVector.Zero));
            _proxies[i] = proxy;
        }

        // HUD reference cached once here so no Find calls happen per frame.
        _hud = GetComponentInChildren<Text>();
        SetHudText();
    }

    // §3 steps 1-2-3: classify IN/OUT against the active origin and pre-step
    // snap each render transform to the canonical local state (before this
    // frame's Advance). Allocation-free.
    private void Update()
    {
        var world = _system.World;
        var origin = _system.Origin;
        for (var i = 0; i < world.BodyCount; i++)
        {
            var globalState = new GlobalRigidBodyState(
                world.Position(i), world.Velocity(i), DVector.Zero);
            _proxies[i].PreStepSync(origin, globalState);
        }
    }

    // §3 steps 5-7: post-step pass, run after this frame's Advance (which fired
    // in FixedUpdate via FixedUpdateLoop). IN read-back folds the local Unity
    // state back into the canonical double state; OUT bodies are repositioned
    // through the active origin from the advanced canonical state; an origin
    // slide re-snapshots every transform from canonical state in the same pass.
    // Allocation-free.
    private void LateUpdate()
    {
        var world = _system.World;
        var origin = _system.Origin;
        var n = world.BodyCount;
        bool originMoved = origin.Origin != _proxies[0].LastSyncOrigin;

        for (var i = 0; i < n; i++)
        {
            var globalState = new GlobalRigidBodyState(
                world.Position(i), world.Velocity(i), DVector.Zero);

            var isIn = origin.DistanceFromOrigin(globalState.GlobalPosition) < origin.LocalRadius;

            if (!isIn)
            {
                // 6. OUT advance: reposition the transform through the active origin.
                _proxies[i].AdvanceOut(origin, globalState);
                continue;
            }

            // 7. Origin slide: re-snapshot this transform when the origin moved.
            if (originMoved)
            {
                _proxies[i].SyncFromGlobal(origin, globalState);
            }

            // 5. IN read-back: fold the local state back into the canonical state.
            var readBack = _proxies[i].ReadBack(origin);
            world.SetPosition(i, readBack.GlobalPosition);
        }

        SetHudText();
    }

    // HUD per §2.3: sun global position (at origin), the structural mapping
    // residual (actually computed per body, not hardwired), drift vs the
    // pure-double reference integrated in lockstep, and warp rate.
    // Metric math is allocation-free (per-body .Position(i) reads, no
    // Bodies() call); the cached StringBuilder is the only scratch buffer,
    // and the final string assignment to Text is the only per-frame allocation.
    private void SetHudText()
    {
        if (_hud == null) return;

        var world = _system.World;
        var reference = _system.Reference;
        var origin = _system.Origin;
        var n = world.BodyCount;
        var pos0 = world.Position(0); // sun (at origin)

        // Structural mapping residual: float32 round-trip
        // |pos − (origin + FromFloat(ToLocal(pos)))|. Structurally ~0 — bounded
        // by ½-ulp of float32 near the origin scale (T10/T14 verify). Computed
        // per body over all bodies; worst case reported.
        double residual = 0.0;
        for (var i = 0; i < n; i++)
        {
            var p = world.Position(i);
            var back = origin.ToGlobal(origin.ToLocal(p));
            var d = p - back;
            var d2 = d.X * d.X + d.Y * d.Y + d.Z * d.Z;
            if (d2 > residual)
                residual = System.Math.Sqrt(d2);
        }

        // Drift vs pure-double reference: worst case |world − reference| over
        // bodies. The reference is a fresh, bit-identical initial-world stepped
        // in lockstep and never folded from the Unity proxy, so this stays
        // bounded/flat under stepping (§2.3: no unbounded compounding).
        double drift = 0.0;
        for (var i = 0; i < n; i++)
        {
            var w = world.Position(i);
            var r = reference.Position(i);
            var d = w - r;
            var d2 = d.X * d.X + d.Y * d.Y + d.Z * d.Z;
            if (d2 > drift)
                drift = System.Math.Sqrt(d2);
        }

        _hudBuffer.Clear();
        _hudBuffer.Append("pos (")
            .Append(pos0.X.ToString("F9")).Append(", ")
            .Append(pos0.Y.ToString("F9")).Append(", ")
            .Append(pos0.Z.ToString("F9")).Append(") ")
            .Append("residual ").Append(residual.ToString("F3")).Append(" ")
            .Append("drift ").Append(drift.ToString("F3")).Append(" ")
            .Append("warp ").Append(_system.WarpRate.ToString("F2"));
        _hud.text = _hudBuffer.ToString();
    }
}
