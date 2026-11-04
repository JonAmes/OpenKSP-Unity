// =============================================================================
// MfdProgram / MfdState — the modder-facing MFD contract (step-03 Task 6).
// A program computes into a small line buffer (state), and Render hands the
// display a string[] to show. MfdState is the program-owned scratch struct a
// program mutates between renders: the display holds one per bound program
// and passes it by ref, so per-frame scratch (previous values, smoothing,
// cached lines) survives between frames without the display knowing anything
// about the program's internals.
// Unity-side only; compiles against UnityEngine + the netstandard2.0 Core DLL.
// =============================================================================

/// <summary>
/// Program-owned state carried between renders. Line slots hold the last
/// rendered lines (Render returns them); the double/int slots are free
/// scratch (previous values, timers, counters). Programs needing richer
/// state keep it in their own fields — one instance per bound display.
/// </summary>
public struct MfdState
{
    public int LineCount;
    public string Line0;
    public string Line1;
    public string Line2;
    public string Line3;
    public string Line4;
    public double A;
    public double B;
    public double C;
    public int Tick;
}

/// <summary>
/// One MFD program: Run advances/mutates the state from the world each
/// frame; Render hands back the line buffer to display. Bind via
/// MfdDisplay.Program.
/// </summary>
public interface MfdProgram
{
    string Name { get; }

    /// <summary>Called once per display frame. Mutate state; do not touch UI.</summary>
    void Run(MfdDisplay display, OpenKSP.Core.Celestial.WorldState world, ref MfdState state);

    /// <summary>Called after Run; returns the lines to show (may be the state's own buffer).</summary>
    string[] Render(MfdState state);
}
