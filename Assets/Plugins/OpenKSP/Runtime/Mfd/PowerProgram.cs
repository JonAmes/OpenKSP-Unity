using System.Globalization;
using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using UnityEngine;

// =============================================================================
// PowerProgram — sample MFD program #2 (step-03 Task 6).
// Power readout. The Core sim has no reactor/EC model yet (Phase 2 is
// orbital mechanics), so this is the scaffold readout the plan asks for:
// solar input ~ 1/r^2 from the focus body's nearest body (placeholder for
// "the sun"), a constant dummy load, and a charge meter integrated in the
// program-owned state slots. When a power model lands in Core, Run reads it
// and nothing in the display contract changes.
// =============================================================================

public class PowerProgram : MfdProgram
{
    private const double DummyLoadW = 1200.0;    // constant placeholder draw
    private const double BatteryCapWh = 50000.0; // placeholder capacity
    private const double RefDistM = 600000000.0; // "1 AU" scale for input curve

    private readonly string[] _lines = new string[5];
    private readonly CultureInfo _inv = CultureInfo.InvariantCulture;

    public string Name { get { return "Power"; } }

    public void Run(MfdDisplay display, WorldState world, ref MfdState state)
    {
        if (world == null || world.BodyCount < 2)
        {
            state.LineCount = 0;
            state.Line0 = "POWER";
            state.Line1 = "(no world)";
            state.LineCount = 2;
            return;
        }

        // Solar input placeholder: inverse-square on distance to body 1.
        CBody[] bodies = world.Bodies;
        D3 rel = world.Position(0) - world.Position(1);
        double r = System.Math.Sqrt(rel.X * rel.X + rel.Y * rel.Y + rel.Z * rel.Z);
        double refFlux = 1000.0; // W/m^2 placeholder
        double input = r > 1.0 ? refFlux * (RefDistM * RefDistM) / (r * r) : refFlux;

        // Charge integration in the state's scratch slot (fraction, clamped).
        double dt = Time.deltaTime;
        double chargeWh = state.A;
        chargeWh += (input - DummyLoadW) * dt / 3600.0;
        if (chargeWh < 0.0) chargeWh = 0.0;
        if (chargeWh > BatteryCapWh) chargeWh = BatteryCapWh;
        state.A = chargeWh;
        state.B = input;
        state.Tick++;

        double pct = chargeWh / BatteryCapWh * 100.0;
        string bus = input >= DummyLoadW ? "CHARGING" : "DISCHARGING";

        _lines[0] = "POWER";
        _lines[1] = "Input    " + input.ToString("F0", _inv) + " W";
        _lines[2] = "Load     " + DummyLoadW.ToString("F0", _inv) + " W";
        _lines[3] = "Charge   " + pct.ToString("F1", _inv) + " %";
        _lines[4] = bus;
        state.LineCount = 5;
        state.Line0 = _lines[0];
        state.Line1 = _lines[1];
        state.Line2 = _lines[2];
        state.Line3 = _lines[3];
        state.Line4 = _lines[4];
    }

    public string[] Render(MfdState state)
    {
        _lines[0] = state.Line0;
        if (state.LineCount > 1) _lines[1] = state.Line1;
        if (state.LineCount > 2) _lines[2] = state.Line2;
        if (state.LineCount > 3) _lines[3] = state.Line3;
        if (state.LineCount > 4) _lines[4] = state.Line4;
        return _lines;
    }
}
