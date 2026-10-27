using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using OpenKSP.Core.Orbit;
using System;
using System.Collections.Generic;
using System.IO;

namespace OpenKSP.Core.Celestial {

// =============================================================================
// WorldState — all rigid-body states relative to the world origin (barycenter),
// plus simulation time. Stepped single-threaded with RK4; TotalEnergy per §9.
// ToSnapshot()/FromSnapshot() is the only Phase-4 extension seam.
//
// Live state = parallel arrays _pos / _vel / _mu (allocated once).
// Hot-path Step() touches only those arrays — no per-step allocations.
// Bodies (id / name / radius / mu) is identity, read-time only.
// =============================================================================
public sealed class WorldState
{
    private static readonly double G = 6.67430e-11;

    private readonly OrbitPropagator _propagator;
    private readonly string[] _id;
    private readonly string[] _name;
    private readonly double[] _radius;
    private readonly double[] _mu;
    private readonly int[] _parentIndex;
    private D3[] _pos;
    private DVector[] _vel;
    private double _simTimeSeconds;

    // World time in seconds relative to world start.
    public double SimTimeSeconds => _simTimeSeconds;

    public int BodyCount => _mu.Length;

    // Live body list (positions/velocities tracked with the stepped state).
    // Built on demand from identity + live state — not part of the hot path.
    public CBody[] Bodies
    {
        get
        {
            var n = _mu.Length;
            var arr = new CBody[n];
            for (var i = 0; i < n; i++)
            {
                arr[i] = new CBody(_id[i], _name[i], _pos[i], _vel[i], _radius[i], _mu[i]);
            }
            return arr;
        }
    }

    // Allocation-free reads of the live per-body state (the only no-alloc seam for the
    // Unity bridge; `Bodies` above allocates and must not be called per frame).
    public D3 Position(int index) => _pos[index];
    public DVector Velocity(int index) => _vel[index];

    // Set the live position of body `index` (hot-path write seam, used by the
    // §3 read-back fold: the canonical state absorbs the proxy's read-back value).
    // No allocation; mirrors the allocation-free Position/Velocity accessors.
    public void SetPosition(int index, D3 pos) => _pos[index] = pos;

    // Construct from immutable body list (positions/velocities/mu baked in).
    public WorldState(IEnumerable<CBody> bodies)
    {
        var list = new List<CBody>(bodies);
        var n = list.Count;
        _id = new string[n];
        _name = new string[n];
        _radius = new double[n];
        _mu = new double[n];
        _pos = new D3[n];
        _vel = new DVector[n];
        for (var i = 0; i < n; i++)
        {
            var b = list[i];
            _id[i] = b.Id;
            _name[i] = b.Name;
            _radius[i] = b.Radius;
            _mu[i] = b.Mu3PerS2;
            _pos[i] = b.Position;
            _vel[i] = b.Velocity;
        }
        _propagator = new OrbitPropagator(Bodies);
        _simTimeSeconds = 0.0;
        _parentIndex = ResolveParentIndices(list);
    }

    // Structural parent map: -1 for root bodies, else the index of the
    // parent in the same body list. Computed once in the constructor —
    // the sim never mutates it, and ParentIndex(i) is O(1) and alloc-free.
    // Rejects unknown parents, self-parents, and cycles (a hierarchy must
    // be a forest rooted at null-parent bodies).
    private static int[] ResolveParentIndices(List<CBody> bodies)
    {
        var n = bodies.Count;
        var idToIndex = new Dictionary<string, int>();
        for (var i = 0; i < n; i++)
        {
            idToIndex.Add(bodies[i].Id, i);
        }
        var parentIndex = new int[n];
        for (var i = 0; i < n; i++)
        {
            var pid = bodies[i].ParentId;
            if (pid == null)
            {
                parentIndex[i] = -1;
                continue;
            }
            if (!idToIndex.TryGetValue(pid, out var parent))
            {
                throw new InvalidDataException($"Body '{bodies[i].Id}' has unknown parent '{pid}'.");
            }
            if (parent == i)
            {
                throw new InvalidDataException($"Body '{bodies[i].Id}' cannot be its own parent.");
            }
            parentIndex[i] = parent;
        }
        RejectCycles(parentIndex, bodies);
        return parentIndex;
    }

    // 3-color walk over the parent chains; any revisit is a cycle.
    private static void RejectCycles(int[] parentIndex, List<CBody> bodies)
    {
        // 0 = unvisited, 1 = on current chain, 2 = fully resolved
        var state = new int[parentIndex.Length];
        for (var i = 0; i < parentIndex.Length; i++)
        {
            if (state[i] != 0) continue;
            var cur = i;
            while (cur != -1)
            {
                if (state[cur] == 1)
                {
                    throw new InvalidDataException($"Parent hierarchy contains a cycle involving '{bodies[cur].Id}'.");
                }
                if (state[cur] == 2) break;
                state[cur] = 1;
                cur = parentIndex[cur];
            }
            for (cur = i; cur != -1; cur = parentIndex[cur]) state[cur] = 2;
        }
    }

    // Parent index of body i; -1 for root bodies. O(1), no allocations.
    public int ParentIndex(int index) => _parentIndex[index];

    // Parent-relative orbital elements for body i (the HUD/planning seam, design §6).
    // Focus = the parent body when ParentIndex(i) >= 0, else the barycenter/origin
    // (D3.Zero) for a root body. Relative state r = pos[i] - pos[focus],
    // v = vel[i] - vel[focus]; mu = the focus body's GM (for a root body the body's
    // own GM — an about-origin, degenerate case per §6 open question). No allocations
    // beyond the returned structs; called at most per HUD refresh, never inside Step().
    public OrbitalElements KeplerElements(int index)
    {
        if (index < 0 || index >= _mu.Length)
            throw new ArgumentOutOfRangeException(nameof(index), "body index out of range.");
        var focus = _parentIndex[index];
        D3 focusPos = focus >= 0 ? _pos[focus] : D3.Zero;
        DVector focusVel = focus >= 0 ? _vel[focus] : DVector.Zero;
        D3 r = _pos[index] - focusPos;
        DVector v = _vel[index] - focusVel;
        double mu = focus >= 0 ? _mu[focus] : _mu[index];
        return KeplerianElements.FromState(r, v, mu);
    }

    // One fixed-timestep RK4 advance of every body; sim time advances by dt.
    // No per-step allocations: only the pre-allocated _pos/_vel/_mu are modified.
    // <param name="dt">Fixed timestep in seconds.</param>
    public void Step(double dt)
    {
        _propagator.Step(dt, _pos, _vel, _mu);
        _simTimeSeconds += dt;
    }

    // Σᵢ ½ mᵢ |vᵢ|² − Σᵢ<ⱼ G mᵢ mⱼ / |rᵢ − rⱼ|   (per doc §9; no rotational terms).
    public double TotalEnergy()
    {
        var n = _mu.Length;
        double ke = 0.0;
        for (var i = 0; i < n; i++)
        {
            var v = _vel[i];
            ke += 0.5 * (_mu[i] / G) * (v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        }
        double pe = 0.0;
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                var dx = _pos[i].X - _pos[j].X;
                var dy = _pos[i].Y - _pos[j].Y;
                var dz = _pos[i].Z - _pos[j].Z;
                var r = System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
                pe -= (_mu[i] / G) * (_mu[j] / G) * G / r; // = mu_i * mu_j / (G * r)
            }
        }
        return ke + pe;
    }

    // Snapshot seam: full, bit-exact copy of (sim time, per-body state).
    public WorldSnapshot ToSnapshot()
    {
        var n = _mu.Length;
        var snapPos = new D3[n];
        var snapVel = new DVector[n];
        for (var i = 0; i < n; i++)
        {
            snapPos[i] = _pos[i];
            snapVel[i] = _vel[i];
        }
        return new WorldSnapshot(n, snapPos, snapVel, _simTimeSeconds);
    }

    // Restore from a snapshot (the only extension seam).
    public void FromSnapshot(WorldSnapshot snap)
    {
        if (snap.BodyCount != _mu.Length)
            throw new ArgumentException($"Snapshot has {snap.BodyCount} bodies; world has {_mu.Length}.");
        for (var i = 0; i < _mu.Length; i++)
        {
            _pos[i] = snap.Positions[i];
            _vel[i] = snap.Velocities[i];
        }
        _simTimeSeconds = snap.SimTime;
    }
}

// =============================================================================
// WorldSnapshot — immutable, bit-exact snapshot: body count + per-body (pos, vel) + sim time.
// The only Phase-4 extension seam for lock-free workers / replay.
// =============================================================================
public readonly struct WorldSnapshot : IEquatable<WorldSnapshot>
{
    public readonly int BodyCount;
    public readonly double SimTime;             // seconds

    public D3[] Positions { get; }
    public DVector[] Velocities { get; }

    public WorldSnapshot(int bodyCount, D3[] positions, DVector[] velocities, double simTime)
    {
        BodyCount = bodyCount;
        SimTime = simTime;
        Positions = positions;
        Velocities = velocities;
    }

    // Exact equality: bit-level identity of every stored double + sim time.
    public bool Equals(WorldSnapshot other)
    {
        if (SimTime != other.SimTime) return false;
        if (BodyCount != other.BodyCount) return false;
        for (var i = 0; i < BodyCount; i++)
        {
            var aP = Positions[i];
            var bP = other.Positions[i];
            if (aP.X != bP.X || aP.Y != bP.Y || aP.Z != bP.Z) return false;
            var aV = Velocities[i];
            var bV = other.Velocities[i];
            if (aV.X != bV.X || aV.Y != bV.Y || aV.Z != bV.Z) return false;
        }
        return true;
    }

    public override bool Equals(object? o) => o is WorldSnapshot s && Equals(s);

    public static bool operator ==(WorldSnapshot left, WorldSnapshot right) => left.Equals(right);
    public static bool operator !=(WorldSnapshot left, WorldSnapshot right) => !left.Equals(right);

    // Consistent with bit-exact Equals: equal snapshots hash equal.
    // Uses HashAccum (mscorlib facades only) since System.HashCode requires netstandard2.1+.
    public override int GetHashCode()
    {
        var h = new HashAccum();
        h.Add(SimTime);
        h.Add(BodyCount);
        for (var i = 0; i < BodyCount; i++)
        {
            var p = Positions[i];
            var v = Velocities[i];
            h.Add(p.X); h.Add(p.Y); h.Add(p.Z);
            h.Add(v.X); h.Add(v.Y); h.Add(v.Z);
        }
        return h.ToHash();
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"t={SimTime:F6}s [{BodyCount} bodies] ");
        for (var i = 0; i < BodyCount; i++)
        {
            sb.Append($"b{i}=({Positions[i]}|{Velocities[i]}) ");
        }
        return sb.ToString();
    }
}
}
