using System;
using System.Collections.Generic;
using System.IO;
using OpenKSP.Core.Modding;
using UnityEngine;

// =============================================================================
// ModDirectoryService — cached view of StreamingAssets/Mods (step-03 Task 8).
// Owns the manifest scan cache (Core ModManifest.Scan) plus parsed
// PartDefinition list. Everything is built once per explicit Rescan();
// queries hit cached lists only, so gameplay Update paths that ask
// "which parts exist / where is this file" allocate nothing.
// =============================================================================

public sealed class ModDirectoryService
{
    private readonly List<ModManifestEntry> _manifest = new List<ModManifestEntry>();
    private readonly List<PartDefinition> _parts = new List<PartDefinition>();
    private readonly List<string> _errors = new List<string>();
    private string _modsRoot = "";
    private bool _scanned;

    public IReadOnlyList<ModManifestEntry> Manifest { get { EnsureScan(); return _manifest; } }
    public IReadOnlyList<PartDefinition> Parts { get { EnsureScan(); return _parts; } }
    public IReadOnlyList<string> Errors { get { EnsureScan(); return _errors; } }

    public string ModsRoot
    {
        get { return _modsRoot; }
        set { _modsRoot = value; _scanned = false; }
    }

    public ModDirectoryService()
    {
        _modsRoot = Path.Combine(UnityEngine.Application.streamingAssetsPath, "Mods");
    }

    /// <summary>Drop the cache; the next query re-scans. Call when the mod
    /// set changes (Mod Manager toggle), never from a frame loop.</summary>
    public void Invalidate() { _scanned = false; }

    private void EnsureScan()
    {
        if (_scanned)
            return;
        _scanned = true;   // set before work: a throwing scan must not loop
        _manifest.Clear();
        _parts.Clear();
        _errors.Clear();
        try
        {
            _manifest.AddRange(ModManifest.Scan(_modsRoot));
        }
        catch (FormatException ex)
        {
            _errors.Add(ex.Message);
            return;
        }
        for (int m = 0; m < _manifest.Count; m++)
        {
            ModManifestEntry entry = _manifest[m];
            for (int f = 0; f < entry.Files.Count; f++)
            {
                ModFileEntry file = entry.Files[f];
                if (!file.RelativePath.EndsWith(".cfg", StringComparison.Ordinal))
                    continue;
                string full = Path.Combine(Path.Combine(_modsRoot, entry.Name), file.RelativePath);
                try
                {
                    ConfigNode node = KspConfig.Parse(File.ReadAllText(full), full);
                    _parts.Add(PartDefinition.FromConfig(node));
                }
                catch (FormatException ex)
                {
                    _errors.Add(entry.Name + "/" + file.RelativePath + ": " + ex.Message);
                }
            }
        }
    }

    /// <summary>Look up a manifest entry by mod name (Ordinal).</summary>
    public bool TryGetMod(string name, out ModManifestEntry entry)
    {
        IReadOnlyList<ModManifestEntry> list = Manifest;
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].Name, name, StringComparison.Ordinal))
            {
                entry = list[i];
                return true;
            }
        }
        entry = null;
        return false;
    }

    /// <summary>Read one mod file's bytes (path is mod-relative; traversal is
    /// refused). Returns false with a filled error when the path is not in
    /// the manifest or unreadable.</summary>
    public bool TryReadFile(string modName, string relativePath, out byte[] bytes, out string error)
    {
        bytes = null;
        error = null;
        ModManifestEntry entry;
        if (!TryGetMod(modName, out entry))
        {
            error = "unknown mod '" + modName + "'";
            return false;
        }
        string full = Path.GetFullPath(Path.Combine(Path.Combine(_modsRoot, entry.Name), relativePath));
        string root = Path.GetFullPath(Path.Combine(_modsRoot, entry.Name));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            error = "path escapes mod directory: " + relativePath;
            return false;
        }
        for (int i = 0; i < entry.Files.Count; i++)
        {
            if (string.Equals(entry.Files[i].RelativePath, relativePath, StringComparison.Ordinal))
            {
                if (!File.Exists(full))
                {
                    error = "missing file: " + full;
                    return false;
                }
                bytes = File.ReadAllBytes(full);
                return true;
            }
        }
        error = "not in manifest: " + modName + "/" + relativePath;
        return false;
    }
}
