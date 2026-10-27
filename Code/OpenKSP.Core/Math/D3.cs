using System;

namespace OpenKSP.Core.Math {

// Double-precision vector for global simulation state.
public struct D3 : IEquatable<D3>
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public static readonly D3 Zero = new(0, 0, 0);

    public D3(double x, double y, double z) => (X, Y, Z) = (x, y, z);

    // Addition
    public static D3 operator +(D3 left, D3 right)
        => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    // Subtraction
    public static D3 operator -(D3 left, D3 right)
        => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    // Negation
    public static D3 operator -(D3 vec) => new(-vec.X, -vec.Y, -vec.Z);

    // Scalar multiplication
    public static D3 operator *(D3 vec, double scalar)
        => new(vec.X * scalar, vec.Y * scalar, vec.Z * scalar);

    // Dot product (returns double)
    public static double operator *(D3 left, D3 right)
        => left.X * right.X + left.Y * right.Y + left.Z * right.Z;

    // Cross product - returns new D3
    public static D3 Cross(D3 left, D3 right)
    {
        return new(
            left.Y * right.Z - left.Z * right.Y,
            left.Z * right.X - left.X * right.Z,
            left.X * right.Y - left.Y * right.X
        );
    }

    // Lerp: left + t*(right-left)
    public D3 Lerp(double t, D3 other)
        => new D3(X + (other.X - X) * t, Y + (other.Y - Y) * t, Z + (other.Z - Z) * t);

    public override string ToString() => $"({X:F9},{Y:F9},{Z:F9})";

    public bool Equals(D3 other)
        => System.Math.Abs(X - other.X) < 1e-14 && 
           System.Math.Abs(Y - other.Y) < 1e-14 && 
           System.Math.Abs(Z - other.Z) < 1e-14;

    public override int GetHashCode() => Hash32.Triple(X, Y, Z);
    
    public static bool operator ==(D3 left, D3 right) => left.Equals(right);
    
    public static bool operator !=(D3 left, D3 right) => !left.Equals(right);
    
    public override bool Equals(object? obj) => obj is D3 d && Equals(d);
}

// Float triple for local-frame proxy state.
public struct F3 : IEquatable<F3>
{
    public float X { get; }
    public float Y { get; }
    public float Z { get; }

    public static readonly F3 Zero = new(0, 0, 0);
    
    public F3(float x, float y, float z) => (X, Y, Z) = (x, y, z);

    // Dot product
    public static float Dot(F3 left, F3 right)
        => left.X * right.X + left.Y * right.Y + left.Z * right.Z;

    public bool Equals(F3 other)
        => System.Math.Abs(X - other.X) < 1e-4f && 
           System.Math.Abs(Y - other.Y) < 1e-4f && 
           System.Math.Abs(Z - other.Z) < 1e-4f;

    public override int GetHashCode() => Hash32.Triple((double)X, (double)Y, (double)Z);
    
    public static bool operator ==(F3 left, F3 right) => left.Equals(right);
    
    public static bool operator !=(F3 left, F3 right) => !left.Equals(right);
    
    public override bool Equals(object? obj) => obj is F3 f && Equals(f);
}

// Scales between local F3 and global D3 coordinate spaces.
public static class Coordinates
{
    // Scales from local F3 to global D3 space
    public static readonly double Scale = 1.0; // Will be set from origin-based mapping
    
    // Convert local float proxy state back to global double position.
    // <param name="local">Local frame value in F3</param>
    // <param name="originOrigin">Global offset point</param>
    // <returns>Global D3 position</returns>
    public static D3 ToFloat(F3 local, D3 originOrigin)
    {
        // Simple identity mapping for now - unit conversion is scale-dependent
        return new D3(
            (double)local.X * Scale + 0.0,
            (double)local.Y * Scale + 0.0,
            (double)local.Z * Scale + 0.0
        );
    }

    // Convert global double vector to local float proxy state
    // <param name="global">Global D3 value</param>
    // <param name="origin">Local frame origin point (will use 0,0,0 base for identity)</param>
    // <returns>Local F3 position relative to origin</returns>
    public static F3 FromFloat(D3 global, D3 origin)
    {
        var deltaX = global.X - origin.X;
        var deltaY = global.Y - origin.Y;
        var deltaZ = global.Z - origin.Z;

        return new F3(
            (float)(deltaX / Scale),
            (float)(deltaY / Scale),
            (float)(deltaZ / Scale)
            );
        }
    }
}
