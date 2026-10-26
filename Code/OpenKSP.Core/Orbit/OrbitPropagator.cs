using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using System;
using System.Collections.Generic;

namespace OpenKSP.Core.Orbit
{
    // RK4 integrator for N-body orbital propagation.
    // The state is the full (D3[] pos, DVector[] vel). Each stage displaces the ENTIRE
    // state; body i's acceleration is then evaluated at the full displaced position array.
    // Scratch arrays (full position scratch + per-stage increments) are allocated once,
    // so the hot path performs no per-step allocations.
    public sealed class OrbitPropagator
    {
        private readonly int _n;
        private readonly DVector[] _k1Pos, _k2Pos, _k3Pos, _k4Pos;
        private readonly DVector[] _k1Vel, _k2Vel, _k3Vel, _k4Vel;
        private readonly D3[] _pScratch;   // reused full-position scratch

        public OrbitPropagator(IEnumerable<CBody> bodies)
        {
            var n = 0;
            foreach (var _ in bodies) n++;
            _n = n;
            _k1Pos = new DVector[n]; _k2Pos = new DVector[n]; _k3Pos = new DVector[n]; _k4Pos = new DVector[n];
            _k1Vel = new DVector[n]; _k2Vel = new DVector[n]; _k3Vel = new DVector[n]; _k4Vel = new DVector[n];
            _pScratch = new D3[n];
        }

        // One RK4 step. pos/vel updated in place. mu: per-body GM (m^3/s^2);
        // body i feels every body j != i (GravityField.BodyAccel).
        public void Step(double dt, D3[] pos, DVector[] vel, double[] mu)
        {
            var h = dt;
            var half = h * 0.5;

            // Stage 1: f(x0) = (v0, acc(x0)).
            for (var i = 0; i < _n; i++)
            {
                _k1Pos[i] = vel[i];
                _k1Vel[i] = GravityField.BodyAccel(pos, mu, i);
            }

            // Stage 2: full intermediate position p2[j] = pos[j] + h/2 * k1[j] for every j.
            for (var j = 0; j < _n; j++)
                _pScratch[j] = new D3(pos[j].X + _k1Pos[j].X * half,
                                     pos[j].Y + _k1Pos[j].Y * half,
                                     pos[j].Z + _k1Pos[j].Z * half);
            for (var i = 0; i < _n; i++)
            {
                _k2Pos[i] = vel[i] + _k1Vel[i] * half;
                _k2Vel[i] = GravityField.BodyAccel(_pScratch, mu, i);
            }

            // Stage 3: p3[j] = pos[j] + h/2 * k2[j].
            for (var j = 0; j < _n; j++)
                _pScratch[j] = new D3(pos[j].X + _k2Pos[j].X * half,
                                     pos[j].Y + _k2Pos[j].Y * half,
                                     pos[j].Z + _k2Pos[j].Z * half);
            for (var i = 0; i < _n; i++)
            {
                _k3Pos[i] = vel[i] + _k2Vel[i] * half;
                _k3Vel[i] = GravityField.BodyAccel(_pScratch, mu, i);
            }

            // Stage 4: p4[j] = pos[j] + h * k3[j].
            for (var j = 0; j < _n; j++)
                _pScratch[j] = new D3(pos[j].X + _k3Pos[j].X * h,
                                     pos[j].Y + _k3Pos[j].Y * h,
                                     pos[j].Z + _k3Pos[j].Z * h);
            for (var i = 0; i < _n; i++)
            {
                _k4Pos[i] = vel[i] + _k3Vel[i] * h;
                _k4Vel[i] = GravityField.BodyAccel(_pScratch, mu, i);
            }

            // Combine: x1 = x0 + h/6 (k1 + 2k2 + 2k3 + k4).
            for (var i = 0; i < _n; i++)
            {
                double cx = _k1Pos[i].X + 2.0 * _k2Pos[i].X + 2.0 * _k3Pos[i].X + _k4Pos[i].X;
                double cy = _k1Pos[i].Y + 2.0 * _k2Pos[i].Y + 2.0 * _k3Pos[i].Y + _k4Pos[i].Y;
                double cz = _k1Pos[i].Z + 2.0 * _k2Pos[i].Z + 2.0 * _k3Pos[i].Z + _k4Pos[i].Z;
                pos[i] = new D3(pos[i].X + cx * (h / 6.0),
                              pos[i].Y + cy * (h / 6.0),
                              pos[i].Z + cz * (h / 6.0));
                double vx = _k1Vel[i].X + 2.0 * _k2Vel[i].X + 2.0 * _k3Vel[i].X + _k4Vel[i].X;
                double vy = _k1Vel[i].Y + 2.0 * _k2Vel[i].Y + 2.0 * _k3Vel[i].Y + _k4Vel[i].Y;
                double vz = _k1Vel[i].Z + 2.0 * _k2Vel[i].Z + 2.0 * _k3Vel[i].Z + _k4Vel[i].Z;
                vel[i] = new DVector(vel[i].X + vx * (h / 6.0),
                                   vel[i].Y + vy * (h / 6.0),
                                   vel[i].Z + vz * (h / 6.0));
            }
        }
    }
}
