using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using System;

namespace OpenKSP.Core.Orbit
{
    /// <summary>
    /// Orbital elements (a, e, i, M, Omega, W) in meters / radians.
    /// M  = mean anomaly;  Omega = longitude of the ascending node;
    /// W  = argument of pericenter;  i = inclination.
    /// </summary>
    public readonly struct OrbitalElements
    {
        public readonly double A;     // semi-major axis (m)
        public readonly double Ecc;   // eccentricity (-)
        public readonly double Inc;   // inclination (rad)
        public readonly double Mean;  // mean anomaly (rad)
        public readonly double Omega; // ascending node (rad)
        public readonly double W;     // arg of pericenter (rad)

        public OrbitalElements(double a, double e, double i, double m, double omegaNode, double w)
        {
            A = a; Ecc = e; Inc = i; Mean = m; Omega = omegaNode; W = w;
        }
    }

    /// <summary>
    /// KeplerianElements: relative state (r, v about the primary at the origin) <->
    /// classic elements, both from the standard orbital-mechanics definitions so
    /// FromState -> ToState reproduces the original state. Pure double math, D3.
    /// </summary>
    public static class KeplerianElements
    {
        private const double Eps = 1e-9;

        private static double Len(D3 v)
            => System.Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

        // Solve M = E - e sin E for E.
        private static double SolveE(double M, double e)
        {
            double E = M;
            for (var k = 0; k < 100; k++)
            {
                double f = E - e * System.Math.Sin(E) - M;
                double fp = 1.0 - e * System.Math.Cos(E);
                double dE = -f / fp;
                E += dE;
                if (System.Math.Abs(dE) < 1e-13) break;
            }
            return E;
        }

        /// <summary>
        /// Relative state (r, v about the primary) -> classic elements.
        /// </summary>
        public static OrbitalElements FromState(D3 r, DVector v, double mu)
        {
            double rMag = Len(r);
            double rHatX = r.X / rMag, rHatY = r.Y / rMag, rHatZ = r.Z / rMag;

            // h = r x v
            D3 h = D3.Cross(r, new D3(v.X, v.Y, v.Z));
            double hMag = Len(h);
            double hx = h.X / hMag, hy = h.Y / hMag, hz = h.Z / hMag;

            // specific energy -> semi-major axis
            double vSq = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
            double eps = 0.5 * vSq - mu / rMag;
            double a = -mu / (2.0 * eps);

            // eccentricity vector e_vec = (v x h)/mu - rhat ; pericenter unit p_hat
            D3 vCrossH = D3.Cross(new D3(v.X, v.Y, v.Z), h);
            double ex = vCrossH.X / mu - rHatX;
            double ey = vCrossH.Y / mu - rHatY;
            double ez = vCrossH.Z / mu - rHatZ;
            double eMag = System.Math.Sqrt(ex * ex + ey * ey + ez * ez);
            double px = ex / eMag, py = ey / eMag, pz = ez / eMag;

            // inclination
            double i = System.Math.Acos(System.Math.Clamp(hz, -1.0, 1.0));

            // ascending node; node unit n
            double omega = 0.0;
            double nMag = System.Math.Sqrt(hx * hx + hy * hy);
            double nx = 0.0, ny = 0.0;
            if (nMag > Eps)
            {
                omega = System.Math.Atan2(-hx, hy);
                nx = System.Math.Cos(omega);
                ny = System.Math.Sin(omega);
            }

            // node-frame in-plane unit q0 = h x n
            double qx = -hz * ny;
            double qy =  hz * nx;
            double qz =  hx * ny - hy * nx;
            double qNorm = System.Math.Sqrt(qx * qx + qy * qy + qz * qz);
            qx /= qNorm; qy /= qNorm; qz /= qNorm;

            // argument of pericenter W: in-plane angle from node n to pericenter p
            double w = 0.0;
            if (eMag > Eps)
                w = System.Math.Atan2(px * qx + py * qy + pz * qz, px * nx + py * ny);

            // Pericenter-frame second unit vector q_hat = cos w q0 - sin w n (unit, in-plane).
            double qhx = System.Math.Cos(w) * qx - System.Math.Sin(w) * nx;
            double qhy = System.Math.Cos(w) * qy - System.Math.Sin(w) * ny;
            double qhz = System.Math.Cos(w) * qz;

            // r/a = 1 - e cosE ;  r hat . q_hat = sqrt(1-e^2) sinE * (r/a)
            double cosE = (1.0 - rMag / a) / eMag;
            double sinE = (rMag / a) * (rHatX * qhx + rHatY * qhy + rHatZ * qhz)
                         / System.Math.Sqrt(1.0 - eMag * eMag);
            double E = System.Math.Atan2(sinE, cosE);
            double m = E - eMag * System.Math.Sin(E);

            return new OrbitalElements(a, eMag, i, m, omega, w);
        }

        /// <summary>
        /// Elements -> relative position state about the primary at the origin.
        /// </summary>
        public static D3 ToState(OrbitalElements el, double mu)
        {
            double a = el.A, e = el.Ecc, i = el.Inc, omega = el.Omega, w = el.W;
            double M = el.Mean;
            double E = SolveE(M, e);
            double cosE = System.Math.Cos(E), sinE = System.Math.Sin(E);

            // node unit n, in-plane q0
            double nx = System.Math.Cos(omega), ny = System.Math.Sin(omega);
            double hx = -System.Math.Sin(i) * System.Math.Sin(omega);
            double hy =  System.Math.Sin(i) * System.Math.Cos(omega);
            double hz =  System.Math.Cos(i);
            double qx = -hz * ny;
            double qy =  hz * nx;
            double qz =  hx * ny - hy * nx;
            double qNorm = System.Math.Sqrt(qx * qx + qy * qy + qz * qz);
            qx /= qNorm; qy /= qNorm; qz /= qNorm;

            // pericenter-frame basis: p_hat = cos w n + sin w q0 ; q_hat = cos w q0 - sin w n
            double cw = System.Math.Cos(w), sw = System.Math.Sin(w);
            double pxx = cw * nx + sw * qx;
            double pyy = cw * ny + sw * qy;
            double pzz = sw * qz;
            double qhx = cw * qx - sw * nx;
            double qhy = cw * qy - sw * ny;
            double qhz = cw * qz;

            // r = a (cosE - e) p_hat + a sqrt(1-e^2) sinE q_hat
            double cnu = cosE - e;
            double se = sinE * System.Math.Sqrt(1.0 - e * e);
            return new D3(a * (cnu * pxx + se * qhx),
                         a * (cnu * pyy + se * qhy),
                         a * (cnu * pzz + se * qhz));
        }

        /// <summary>
        /// Elements -> relative velocity state (same state as ToState).
        /// v = sqrt(mu/a)/(1 - e cosE) (-sinE p_hat + sqrt(1-e^2) cosE q_hat)
        /// </summary>
        public static DVector StateVelocity(OrbitalElements el, double mu)
        {
            double a = el.A, e = el.Ecc, i = el.Inc, omega = el.Omega, w = el.W;
            double M = el.Mean;
            double E = SolveE(M, e);
            double cosE = System.Math.Cos(E), sinE = System.Math.Sin(E);

            double nx = System.Math.Cos(omega), ny = System.Math.Sin(omega);
            double hx = -System.Math.Sin(i) * System.Math.Sin(omega);
            double hy =  System.Math.Sin(i) * System.Math.Cos(omega);
            double hz =  System.Math.Cos(i);
            double qx = -hz * ny;
            double qy =  hz * nx;
            double qz =  hx * ny - hy * nx;
            double qNorm = System.Math.Sqrt(qx * qx + qy * qy + qz * qz);
            qx /= qNorm; qy /= qNorm; qz /= qNorm;

            double cw = System.Math.Cos(w), sw = System.Math.Sin(w);
            double pxx = cw * nx + sw * qx;
            double pyy = cw * ny + sw * qy;
            double pzz = sw * qz;
            double qhx = cw * qx - sw * nx;
            double qhy = cw * qy - sw * ny;
            double qhz = cw * qz;

            double scale = System.Math.Sqrt(mu / a) / (1.0 - e * cosE);
            double se = sinE, cse = System.Math.Sqrt(1.0 - e * e) * cosE;
            return new DVector(
                scale * (-se * pxx + cse * qhx),
                scale * (-se * pyy + cse * qhy),
                scale * (-se * pzz + cse * qhz));
        }
    }
}
