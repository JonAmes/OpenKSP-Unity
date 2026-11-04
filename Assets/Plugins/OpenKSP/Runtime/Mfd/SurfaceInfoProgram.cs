using System.Globalization;
using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using UnityEngine;

// =============================================================================
// SurfaceInfoProgram — sample MFD program #1 (step-03 Task 6).
// Readout for the focused body (index 0): speed, altitude above the nearest
// body surface, vertical speed, and local g at that altitude. The sim has no
// ground-contact state, so "surface info" is computed from orbital state:
// altitude = min over bodies of (|r_focus - r_body| - radius_body), and g is
// mu/r^2 from the same nearest body. Numbers formatted with InvariantCulture
// so readouts are locale-independent.
// =============================================================================

public class SurfaceInfoProgram : MfdProgram
{
    private const int MaxLines = 5;

    // Line buffers owned by the program; Render hands these to the display.
    private readonly string[] _lines = new string[MaxLines];
    private readonly CultureInfo _inv = CultureInfo.InvariantCulture;

    public string Name { get { return "Surface Info"; } }

    public void Run(MfdDisplay display, WorldState world, ref MfdState state)
    {
        state.LineCount = 0;
        if (world == null || world.BodyCount == 0)
        {
            state.Line0 = "SURFACE INFO";
            state.Line1 = "(no world)";
            state.LineCount = 2;
            return;
        }

        CBody[] bodies = world.Bodies;
        D3 focusPos = world.Position(0);
        double speed = world.Velocity(0).Length();

        // Nearest body surface under the focus body.
        int nearest = -1;
        double nearestAlt = double.PositiveInfinity;
        D3 nearestPos = D3.Zero;
        for (int i = 1; i < bodies.Length; i++)
        {
            double dx = focusPos.X - world.Position(i).X;
            double dy = focusPos.Y - world.Position(i).Y;
            double dz = focusPos.Z - world.Position(i).Z;
            double alt = System.Math.Sqrt(dx * dx + dy * dy + dz * dz) - bodies[i].Radius;
            if (alt < nearestAlt)
            {
                nearestAlt = alt;
                nearest = i;
                nearestPos = world.Position(i);
            }
        }

        // Vertical speed: radial component of focus velocity around nearest body.
        double vr = 0.0;
        double g = 0.0;
        if (nearest >= 0)
        {
            D3 rel = focusPos - nearestPos;
            double r = System.Math.Sqrt(rel.X * rel.X + rel.Y * rel.Y + rel.Z * rel.Z);
            if (r > 1e-9)
            {
                DVector vel = world.Velocity(0) - world.Velocity(nearest);
                vr = (vel.X * rel.X + vel.Y * rel.Y + vel.Z * rel.Z) / r;
                g = bodies[nearest].Mu3PerS2 / (r * r);
            }
        }

        // Scratch carries the previous altitude through the state struct.
        state.B = state.A;   // previous altitude
        state.A = nearestAlt;
        state.Tick++;

        _lines[0] = "SURFACE INFO";
        _lines[1] = "Speed    " + speed.ToString("F1", _inv) + " m/s";
        _lines[2] = nearest >= 0
            ? "Alt      " + nearestAlt.ToString("F1", _inv) + " m  (" + bodies[nearest].Name + ")"
            : "Alt      (no body)";
        _lines[3] = "Vert     " + vr.ToString("F1", _inv) + " m/s";
        _lines[4] = "g        " + (g / 9.80665).ToString("F3", _inv) + " g";
        state.LineCount = MaxLines;
        state.Line0 = _lines[0];
        state.Line1 = _lines[1];
        state.Line2 = _lines[2];
        state.Line3 = _lines[3];
        state.Line4 = _lines[4];
    }

    public string[] Render(MfdState state)
    {
        // Copy the state's line slots back out (state is the authority).
        _lines[0] = state.Line0;
        if (state.LineCount > 1) _lines[1] = state.Line1;
        if (state.LineCount > 2) _lines[2] = state.Line2;
        if (state.LineCount > 3) _lines[3] = state.Line3;
        if (state.LineCount > 4) _lines[4] = state.Line4;
        return _lines;
    }
}
