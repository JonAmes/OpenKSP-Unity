# Step 1 — Findings feeding the cold review (Task 7)

Recorded by qa (bug tester) from the bug-test gate (`Docs/steps/step-01-bugtest.md`).

## Open findings

### F1. Cosmetic: unused `using System.Linq;` in `GravityField.cs`
- File: `Code/OpenKSP.Core/Celestial/GravityField.cs:1`
- No LINQ call sites exist anywhere in `Code/OpenKSP.Core/` (verified by grep on source, `.cs` only).
- Suggested action: remove the import. No functional impact.

### F2. Cosmetic: bridge `Initialize` could take `IReadOnlyList<CBody>`
- File: `Assets/Plugins/OpenKSP/Runtime/FloatingOriginSystem.cs:58`
- `Initialize(IEnumerable<CBody> loadedBodies)` materializes via `new List<CBody>(...)`.
- `CBodyLoader.Load` returns `IImmutableList<CBody>`, which satisfies `IReadOnlyList<CBody>` (`.Count` + indexing work) — verified by a compile probe.
- Suggested action: narrow the parameter to `IReadOnlyList<CBody>`. No functional impact.

### Known scope gap (not a defect, pre-accepted per §10)
- Unity bridge MonoBehaviours cannot be compiled/run on this host (no Unity assemblies). Validation = static API review + headless Core build/tests.
