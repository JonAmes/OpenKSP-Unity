using OpenKSP.Core.Math;
using System;
using System.Collections.Generic;

namespace OpenKSP.Core.Celestial {

// D3 + DVector in same namespace for convenience.
public struct DVector : IEquatable<DVector>
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public static readonly DVector Zero = new(0, 0, 0);

    public DVector(double x, double y, double z) => (X, Y, Z) = (x, y, z);

    public override bool Equals(object? o) => o is DVector dv && Equals(dv);
    public bool Equals(DVector other) => System.Math.Abs(X - other.X) < 1e-15 &&
                                            System.Math.Abs(Y - other.Y) < 1e-15 &&
                                            System.Math.Abs(Z - other.Z) < 1e-15;
    public override int GetHashCode() => Hash32.Triple(X, Y, Z);
    public static bool operator ==(DVector left, DVector right) => left.Equals(right);
    public static bool operator !=(DVector left, DVector right) => !left.Equals(right);
    public static double operator *(DVector left, DVector right)
        => left.X * right.X + left.Y * right.Y + left.Z * right.Z;

    // Vector addition
    public static DVector operator +(DVector left, DVector right)
        => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    // Vector subtraction
    public static DVector operator -(DVector left, DVector right)
        => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    // Scalar multiplication
    public static DVector operator *(DVector left, double scalar)
        => new(left.X * scalar, left.Y * scalar, left.Z * scalar);

    // Length of a vector.
    public double Length()
        => System.Math.Sqrt(X * X + Y * Y + Z * Z);
    public override string ToString() => $"({X:F9}, {Y:F9}, {Z:F9})";

    public struct With : IEquatable<With>
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public static implicit operator DVector(With w) => new DVector(w.X, w.Y, w.Z);

        public bool Equals(With other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object? o) => o is With w && Equals(w);
        public override int GetHashCode() => Hash32.Triple(X, Y, Z);
    }
}

// Celestial body with gravity parameter stored directly.
// readonly fields (no `init;`) so the DLL carries only netstandard2.0 facades
// that Unity's Mono runtime resolves.
public readonly struct CBody : IEquatable<CBody>
{
    public readonly string Id;
    public readonly string Name;
    public readonly D3 Position;
    public readonly DVector Velocity;

    // Standard gravitational parameter GM in m³/s².
    public readonly double Mu3PerS2;

    // Radius in m (per §6 schema; used by later phases).
    public readonly double Radius;

    // Parent body id; null for root bodies (Phase 2 hierarchy, §schema "parent").
    // Structural: set once at load, never mutated by the sim.
    public readonly string? ParentId;

    // Mass in kg, derived from mu (mu / G).
    public double Mass => Mu3PerS2 / 6.67430e-11;

    public CBody(string id, string name, double mu)
        : this(id, name, D3.Zero, DVector.Zero, 0.0, mu, null)
    {
    }
    public CBody(string id, string name, DVector pos, DVector vel, double radius, double mu)
        : this(id, name, new D3(pos.X, pos.Y, pos.Z), vel, radius, mu, null)
    {
    }
    public CBody(string id, string name, D3 pos, DVector vel, double radius, double mu)
        : this(id, name, pos, vel, radius, mu, null)
    {
    }
    public CBody(string id, string name, D3 pos, DVector vel, double radius, double mu, string? parentId)
    {
        Id = id;
        Name = name;
        Position = pos;
        Velocity = vel;
        Radius = radius;
        Mu3PerS2 = mu;
        ParentId = parentId;
    }

    public override bool Equals(object? o) => o is CBody b && Id == b.Id;
    public bool Equals(CBody other) => Id == other.Id;
    // Consistent with Equals (which keys on Id only): equal bodies hash equal.
    public override int GetHashCode() => Id.GetHashCode();
    public static bool operator ==(CBody left, CBody right) => left.Equals(right);
    public static bool operator !=(CBody left, CBody right) => !left.Equals(right);
}
}
