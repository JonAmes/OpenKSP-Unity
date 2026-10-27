using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using OpenKSP.Core.World;
using UnityEngine;

// =============================================================================
// PhysicsProxy — the sim -> render entry point (per §7, implements §3 ordering).
// Holds one Unity Rigidbody transform. Unity transforms hold LOCAL values only;
// global math is the Core's. Velocity/angular-velocity are deltas (not
// positions), so the origin does not offset them — the local form is just the
// global value cast to float.
// Allocation-free hot path: reads pre-allocated Core state; no per-frame alloc.
// The active FloatingOrigin is passed per call (it can slide between steps).
// =============================================================================

// Canonical per-body state in global double space. The proxy reads this and
// re-syncs the Unity transform; OUT bodies are advanced by Core RK4 and
// folded back through the active origin.
public struct GlobalRigidBodyState
{
    public D3 GlobalPosition;
    public DVector GlobalVelocity;
    public DVector GlobalAngularVelocity;

    public GlobalRigidBodyState(D3 pos, DVector vel, DVector angVel)
    {
        GlobalPosition = pos;
        GlobalVelocity = vel;
        GlobalAngularVelocity = angVel;
    }
}

[RequireComponent(typeof(Rigidbody))]
public class PhysicsProxy : MonoBehaviour
{
    private Rigidbody _rb;
    private FloatingOrigin _origin;
    private GlobalRigidBodyState _state;
    // The origin this proxy last synced against — used to detect an origin slide.
    private D3 _lastSyncOrigin;

    // The origin origin this proxy last synced against.
    public D3 LastSyncOrigin => _lastSyncOrigin;

    // Set up this proxy with its origin and initial global state (init only).
    public void Initialize(FloatingOrigin origin, GlobalRigidBodyState state)
    {
        _rb = GetComponent<Rigidbody>();
        _origin = origin;
        _state = state;
        _lastSyncOrigin = origin.Origin;
    }

    // Pre-step snap (per §3 steps 1-2): classify IN/OUT against the active origin,
    // then snap the Unity rigidbody to the canonical local state.
    public void PreStepSync(FloatingOrigin origin, GlobalRigidBodyState state)
    {
        _origin = origin;
        _state = state;
        _lastSyncOrigin = origin.Origin;

        var localPos = origin.ToLocal(state.GlobalPosition);
        var isIn = origin.DistanceFromOrigin(state.GlobalPosition) < origin.LocalRadius;

        if (isIn)
        {
            // IN: MovePosition avoids solver interference; velocities are local.
            _rb.MovePosition(ToUnity(localPos));
        }

        // Velocity and angular velocity are pure deltas (no origin offset).
        // velocity is obsolete in Unity 6.0 — linearVelocity is the live member.
        _rb.linearVelocity = ToUnityVel(state.GlobalVelocity);
        _rb.angularVelocity = ToUnityVel(state.GlobalAngularVelocity);

        if (!isIn)
        {
            // OUT: zero Unity integration; kinematic + discrete collision.
            _rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            _rb.isKinematic = true;
        }
    }

    // Post-step read-back (per §3 step 5, IN): fold the local Unity state back
    // into the canonical global state via the active origin. Position is mapped
    // through the origin; velocity/angular are pure deltas (no origin offset).
    public GlobalRigidBodyState ReadBack(FloatingOrigin origin)
    {
        var localPos = FromUnity(_rb.position);
        var globalPos = origin.ToGlobal(localPos);

        return new GlobalRigidBodyState(
            globalPos,
            FromUnityVel(_rb.linearVelocity),
            FromUnityVel(_rb.angularVelocity));
    }

    // Post-step OUT advance (per §3 step 6): reposition the OUT transform through
    // the active origin from the advanced canonical state:
    // rb.position = (globalPos' − origin).ToFloat(). Velocity/angular are pure
    // deltas (no origin offset); the body stays kinematic (zero Unity
    // integration). No per-call allocations (struct copy only).
    public void AdvanceOut(FloatingOrigin origin, GlobalRigidBodyState state)
    {
        _state = state;
        _origin = origin;
        _lastSyncOrigin = origin.Origin;
        _rb.position = ToUnity(origin.ToLocal(state.GlobalPosition));
        _rb.linearVelocity = ToUnityVel(state.GlobalVelocity);
        _rb.angularVelocity = ToUnityVel(state.GlobalAngularVelocity);
        _rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        _rb.isKinematic = true;
    }

    // Re-slide this proxy from the canonical global state (per §3 step 7) —
    // called when the origin moved this step; the transform re-syncs against the
    // new active origin.
    public void SyncFromGlobal(FloatingOrigin origin, GlobalRigidBodyState state)
    {
        _state = state;
        _origin = origin;
        _lastSyncOrigin = origin.Origin;
        _rb.position = ToUnity(origin.ToLocal(state.GlobalPosition));
        _rb.linearVelocity = ToUnityVel(state.GlobalVelocity);
        _rb.angularVelocity = ToUnityVel(state.GlobalAngularVelocity);
    }

    // Cast a Core double velocity vector to a Unity local Vector3.
    private static Vector3 ToUnityVel(DVector v)
        => new Vector3((float)v.X, (float)v.Y, (float)v.Z);

    // Cast a Unity local Vector3 to a Core double velocity vector.
    private static DVector FromUnityVel(Vector3 v)
        => new DVector((double)v.x, (double)v.y, (double)v.z);

    // Cast a Core float position (F3) to a Unity local Vector3.
    private static Vector3 ToUnity(F3 l) => new Vector3(l.X, l.Y, l.Z);

    // Cast a Unity local Vector3 to a Core float position (F3).
    private static F3 FromUnity(Vector3 v) => new F3(v.x, v.y, v.z);
}
