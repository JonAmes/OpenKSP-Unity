using System.Linq;
using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using System;
using System.Collections.Generic;

namespace OpenKSP.Core.Celestial {

// N-body gravity field: acceleration = Σ μ_i * r / |r|³ for each body.
// Uses the body's stored Mu (GM) directly — no need to know G.
public class GravityField
{
    private readonly CBody[] _bodies;

    public int Count => _bodies.Length;
    public CBody[] Bodies => _bodies;

    // <param name="bodies">List of bodies with positions/velocities (copied into an immutable array).</param>
    public GravityField(IEnumerable<CBody> bodies)
    {
        var list = new List<CBody>(bodies);
        _bodies = list.ToArray();
    }

    // Acceleration at `pos` due to all bodies (point-mass: a = μ·r_vec/r³, no G needed).
    public DVector Acceleration(DVector pos)
    {
        double sumX = 0.0, sumY = 0.0, sumZ = 0.0;
        foreach (var body in _bodies)
        {
            var sepX = body.Position.X - pos.X;
            var sepY = body.Position.Y - pos.Y;
            var sepZ = body.Position.Z - pos.Z;

            var rSq = sepX * sepX + sepY * sepY + sepZ * sepZ;
            if (rSq > 1e-18) // avoid singularity
            {
                var r = System.Math.Sqrt(rSq);
                var muDivR3 = body.Mu3PerS2 / (r * r * r);
                sumX += sepX * muDivR3;
                sumY += sepY * muDivR3;
                sumZ += sepZ * muDivR3;
            }
        }
        return new DVector(sumX, sumY, sumZ);
    }

    // Acceleration of body index `i` due to every other body j != i,
    // point-mass: a = Σ_{j≠i} μ_j (r_j − r_i) / |r_j − r_i|³ (attractive, toward j).
    // Static, allocation-free: reads parallel arrays of positions / mu.
    public static DVector BodyAccel(D3[] pos, double[] mu, int i)
    {
        double sx = 0.0, sy = 0.0, sz = 0.0;
        for (var j = 0; j < pos.Length; j++)
        {
            if (j == i) continue;
            var dx = pos[j].X - pos[i].X;
            var dy = pos[j].Y - pos[i].Y;
            var dz = pos[j].Z - pos[i].Z;
            var rSq = dx * dx + dy * dy + dz * dz;
            if (rSq < 1e-18) continue; // avoid singularity
            var invR3 = mu[j] / (rSq * System.Math.Sqrt(rSq));
            sx += dx * invR3;
            sy += dy * invR3;
            sz += dz * invR3;
        }
        return new DVector(sx, sy, sz);
    }

    // Analytic 2-body point-mass acceleration of b2 due to b1:
    // a = mu_b1 · (r_b1 − r_b2) / |r_b2 − r_b1|³ (attractive, toward b1).
    // 
    public DVector TwoBodyAcc(CBody b1, CBody b2)
    {
        var dx = b1.Position.X - b2.Position.X;
        var dy = b1.Position.Y - b2.Position.Y;
        var dz = b1.Position.Z - b2.Position.Z;
        var rSq = dx * dx + dy * dy + dz * dz;
        if (rSq < 1e-24) return DVector.Zero;
        var invR3 = System.Math.Pow(rSq, -1.5);
        var acc = b1.Mu3PerS2 * invR3;
        return new DVector(dx * acc, dy * acc, dz * acc);
    }

    // Distance from body center to position (for local-radius classification).
    public double DistanceFromBody(CBody body, DVector pos)
    {
        var dx = body.Position.X - pos.X;
        var dy = body.Position.Y - pos.Y;
        var dz = body.Position.Z - pos.Z;
        return System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
