using OpenKSP.Core.Math;
using OpenKSP.Core.World;
using UnityEngine;

// =============================================================================
// PhysicsProxy — the sim -> render entry point (per §7, implements §3 ordering).
// Holds a GlobalRigidBodyState and drives one Unity Rigidbody transform.
// Unity transforms hold LOCAL values only; global math is the Core's.
// Velocity/angular-velocity are deltas (not positions), so the origin does not
// offset them — the local form is just the global value cast to float.
// Allocation-free hot path: reads pre-allocated Core state; no per-frame alloc.
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

public class PhysicsProxy : MonoBehaviour
{
    [RequireComponent(typeof(Rigidbody))]

    private Rigidbody _rb;
    private FloatingOrigin _origin;
    private GlobalRigidBodyState _state;

    // Set up this proxy with its origin and initial global state (init only).
    public void Initialize(FloatingOrigin origin, GlobalRigidBodyState state)
    {
        _rb = GetComponent<Rigidbody>();
        _origin = origin;
        _state = state;
    }

    // Pre-step snap (per §3 steps 1-2): classify IN/OUT against the origin, then
    // snap the Unity rigidbody to the canonical local state.
    public void PreStepSync()
    {
        var localPos = _origin.ToLocal(_state.GlobalPosition);
        var isIn = _origin.DistanceFromOrigin(_state.GlobalPosition) < _origin.LocalRadius;

        if (isIn)
        {
            // IN: MovePosition avoids solver interference; velocities are local.
            _rb.MovePosition(ToUnity(localPos));
        }

        // Velocity and angular velocity are pure deltas (no origin offset).
        _rb.velocity = ToUnityVel(_state.GlobalVelocity);
        _rb.angularVelocity = ToUnityVel(_state.GlobalAngularVelocity);

        if (!isIn)
        {
            // OUT: zero Unity integration; kinematic + discrete collision.
            _rb.collisionDetection = CollisionDetectionMode.Discrete;
            _rb.isKinematic = true;
        }
    }

    // Post-step read-back (per §3 step 5, IN): fold the local Unity state back
    // into the canonical global state via the active origin. Position is mapped
    // through the origin; velocity/angular are pure deltas (no origin offset).
    public GlobalRigidBodyState ReadBack()
    {
        var localPos = FromUnity(_rb.position);
        var globalPos = _origin.ToGlobal(localPos);

        return new GlobalRigidBodyState(
            globalPos,
            FromUnityVel(_rb.velocity),
            FromUnityVel(_rb.angularVelocity));
    }

    // Re-slide this proxy from the canonical global state (per §3 step 7) —
    // called within the same ordering when the origin moved this step.
    public void SyncFromGlobal(GlobalRigidBodyState state)
    {
        _state = state;
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
