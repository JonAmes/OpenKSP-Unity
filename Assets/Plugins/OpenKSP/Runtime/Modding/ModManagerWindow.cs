using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

// =============================================================================
// ModManagerWindow — runtime uGUI window listing mods + their part
// definitions (step-03 Task 7b). The manifest scan (Core ModManifest.Scan)
// and part parsing run ONLY on explicit refresh (start-up or button press),
// never per frame: results are cached in fields and the UI is re-rendered
// only when the cache changed. The Update path while clean is a single bool
// check — no allocations.
// =============================================================================

public class ModManagerWindow : MonoBehaviour
{
    [SerializeField] private Text titleText;   // assign in inspector
    [SerializeField] private Text listText;    // assign in inspector
    [SerializeField] private Text errorText;   // assign in inspector
    [SerializeField] private Button scanButton;

    private readonly List<OpenKSP.Core.Modding.ModManifestEntry> _manifest =
        new List<OpenKSP.Core.Modding.ModManifestEntry>();
    private readonly List<PartRow> _rows = new List<PartRow>();
    private string _errorLine = "";
    private bool _dirty = true;

    private sealed class PartRow
    {
        public string ModName = "";
        public string PartId = "";
        public int FileCount;
        public bool Enabled;
    }

    private void OnEnable()
    {
        if (scanButton != null)
            scanButton.onClick.AddListener(Rescan);
        Rescan();
    }

    /// <summary>Re-scan the Mods root and re-parse part configs. On-demand only.</summary>
    public void Rescan()
    {
        _errorLine = "";
        string modsRoot = Path.Combine(UnityEngine.Application.streamingAssetsPath, "Mods");
        try
        {
            _manifest.Clear();
            _manifest.AddRange(OpenKSP.Core.Modding.ModManifest.Scan(modsRoot));
        }
        catch (FormatException ex)
        {
            _errorLine = ex.Message;
        }
        RebuildRows();
        _dirty = true;
    }

    private void RebuildRows()
    {
        _rows.Clear();
        for (int m = 0; m < _manifest.Count; m++)
        {
            OpenKSP.Core.Modding.ModManifestEntry entry = _manifest[m];
            string modsRoot = Path.Combine(UnityEngine.Application.streamingAssetsPath, "Mods");
            for (int f = 0; f < entry.Files.Count; f++)
            {
                OpenKSP.Core.Modding.ModFileEntry file = entry.Files[f];
                if (!file.RelativePath.EndsWith(".cfg", StringComparison.Ordinal))
                    continue;
                string full = Path.Combine(Path.Combine(modsRoot, entry.Name), file.RelativePath);
                try
                {
                    OpenKSP.Core.Modding.ConfigNode node =
                        OpenKSP.Core.Modding.KspConfig.Parse(File.ReadAllText(full), full);
                    OpenKSP.Core.Modding.PartDefinition part =
                        OpenKSP.Core.Modding.PartDefinition.FromConfig(node);
                    PartRow row = new PartRow();
                    row.ModName = entry.Name;
                    row.PartId = part.Id;
                    row.FileCount = entry.Files.Count;
                    row.Enabled = true;
                    _rows.Add(row);
                }
                catch (FormatException ex)
                {
                    // Surface parse errors in the window instead of killing the scan.
                    _errorLine = entry.Name + "/" + file.RelativePath + ": " + ex.Message;
                }
            }
        }
    }

    private void Update()
    {
        // Clean path: one bool, zero allocations. Rendering only on change.
        if (!_dirty)
            return;
        _dirty = false;
        RenderOnce();
    }

    private void RenderOnce()
    {
        if (titleText != null)
            titleText.text = "MOD MANAGER (" + _manifest.Count + " mod" +
                             (_manifest.Count == 1 ? "" : "s") + ")";
        if (listText != null)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _rows.Count; i++)
            {
                PartRow row = _rows[i];
                sb.Append(row.Enabled ? "[x] " : "[ ] ");
                sb.Append(row.ModName);
                sb.Append("  ·  ");
                sb.Append(row.PartId);
                sb.Append("  (");
                sb.Append(row.FileCount);
                sb.Append(" files)\n");
            }
            if (_rows.Count == 0)
                sb.Append("(no parts found under StreamingAssets/Mods)");
            listText.text = sb.ToString();
        }
        if (errorText != null)
            errorText.text = _errorLine;
    }
}
