using OpenKSP.Core.Celestial;
using UnityEngine;
using UnityEngine.UI;

// =============================================================================
// MfdDisplay — a uGUI text element bound to one MfdProgram (step-03 Task 6).
// Awake caches the Text component (no Find/GetComponent in Update). Each
// frame: Program.Run(display, world, ref state) then Program.Render(state);
// the lines are joined into the label. Only the final assignment into the
// Text component allocates; component references are cached once.
// The WorldState is injected by the sim host (SetWorld); while null the
// display idles and shows the program name.
// =============================================================================

[RequireComponent(typeof(Text))]
public class MfdDisplay : MonoBehaviour
{
    private Text _text;
    private MfdProgram _program;
    private WorldState _world;
    private MfdState _state;
    private string _lastJoined = "";
    // Reused across frames; only the final ToString into the Text may allocate.
    private readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder();

    // The program currently bound (mode switching at runtime).
    public MfdProgram Program
    {
        get { return _program; }
        set
        {
            _program = value;
            _state = new MfdState();  // fresh program-owned state on rebind
        }
    }

    public WorldState World { get { return _world; } }
    public MfdState State { get { return _state; } }

    // Injected once by the sim host (or a scene setup script).
    public void SetWorld(WorldState world) { _world = world; }

    private void Awake()
    {
        _text = GetComponent<Text>();
    }

    private void Update()
    {
        if (_program == null)
            return;
        _program.Run(this, _world, ref _state);
        string[] lines = _program.Render(_state);
        string joined = JoinLines(lines);
        // Assigning the Text text re-rebuilds the mesh; skip unchanged.
        if (joined != _lastJoined)
        {
            _lastJoined = joined;
            _text.text = joined;
        }
    }

    private string JoinLines(string[] lines)
    {
        if (lines == null || lines.Length == 0)
            return "";
        _sb.Length = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) _sb.Append('\n');
            _sb.Append(lines[i]);
        }
        return _sb.ToString();
    }
}
