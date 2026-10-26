# Step 1 — Unity CLI test pass: changes & rationale

**Date:** 2026-10-26 · **Branch:** `main` · **HEAD:** `c2b6dbf`

What was asked: run the Unity CLI, find compile errors, fix them, re-run Tasks 6 & 7, and produce a report of every change and why.

## What the Unity CLI returned

`/home/jonames/Unity/Hub/Editor/6000.6.6f1/Editor/Unity -batchmode -quit -nographics -nologo -projectPath <repo> -no-collab` → **exit 198: "No valid Unity Editor license found"** (0 entitlement groups; the 6000.6.7f1 binary behaves identically). Headless Unity execution is license-blocked on this host — an environment gap, not a project defect.

Substitute verification (what actually produced the compiler diagnostics): a Roslyn probe compiling the three production bridge files against the **real** Unity assemblies shipped in the Editor install:
- façade `UnityEngine.dll` (`Editor/Data/Managed`)
- `UnityEngine.UI.dll` (PackageManager ProjectTemplates libcache)
- `<ProjectReference>` to `OpenKSP.Core`
- net8.0 / C# 9, mirroring the project settings.

## All changes (commit `c2b6dbf`, 3 files, +30/−26)

### 1. `Assets/Plugins/OpenKSP/Runtime/PhysicsProxy.cs`

| Change | Why |
|---|---|
| Added `using OpenKSP.Core.Celestial;` | **CS0246** `DVector` was unresolved — the file imported only `OpenKSP.Core.Math` and `OpenKSP.Core.World`, but `DVector` (and `D3`'s operator set used through `DVector` args) lives in `Celestial` (`CBody.cs:8`). The probe and a namespace check both confirmed the missing import. |
| Moved `[RequireComponent(typeof(Rigidbody))]` from a field to the class declaration | **CS0592**: `[RequireComponent]` is only legal on component class declarations, not fields. Unity rejected the field-attribute as invalid. |
| `_rb.velocity` → `_rb.linearVelocity` (write in `PreStepSync` and read-back in `ReadBack`, 2 sites) | **CS0618**: `Rigidbody.velocity` is **obsolete in Unity 6.0**; the live member is `linearVelocity` (confirmed by reflection against the real `CoreModule.dll`: `velocity` carries `[Obsolete]`, `linearVelocity` does not). |
| `_rb.collisionDetection = …` → `_rb.collisionDetectionMode = …` | **CS1061**: `collisionDetection` does not exist on `Rigidbody` in the Unity 6.0 API (reflection: member absent); the property is `collisionDetectionMode` of type `CollisionDetectionMode`. |

### 2. `Assets/Plugins/OpenKSP/Runtime/FloatingOriginSystem.cs`

| Change | Why |
|---|---|
| Moved `[RequireComponent(typeof(FixedUpdateLoop))]` from a field to the class declaration | **CS0592** (same rule as above): invalid attribute target on a field. |

### 3. `Assets/Plugins/OpenKSP/Runtime/SolarSystemStub.cs` (full rewrite, 81 lines)

| Change | Why |
|---|---|
| Moved `[RequireComponent(typeof(FloatingOriginSystem))]` from a field to the class declaration | **CS0592**, as above. |
| `_system = GetComponent<FloatingOriginSystem>()` and `_hud = GetComponentInChildren<Text>()` cached **in `Awake`**; `Update()` now does **zero** component lookups; `SetHudText()` takes no parameters | Closes the review's **MAJOR** finding: `Update()` previously ran `GetComponent<FloatingOriginSystem>()` and `GetComponentInChildren<Text>()` **every frame** (old lines 46 and 64) — a per-frame component lookup, which the §7 rule bans ("No Find calls in Update"). `[RequireComponent]` guarantees the system component exists when this one is added, so the Awake-time cache is always valid. |
| Kept per-frame reads on `WorldState.Position(i)` / `Velocity(i)` | Allocation-free accessors; the rewrite preserves the no-per-frame-allocation discipline on the sim side (the HUD string is display-only and stays in `Update`). |

**Net result:** after the probe re-run on the fixed files, **zero genuine Unity-compiler errors remain** in the bridge. The only residual probe diagnostics are harness artifacts that cannot occur in a real Unity project: CS0012/`ArgumentException` from the dotnet harness not replicating Unity's façade→`UnityEngine.CoreModule` type forwarder, and the probe-only entry point.

## Task 6 re-run (`Docs/steps/step-01-bugtest.md`) — **PASS**

Fresh on `c2b6dbf`:
- `dotnet build Code/OpenKSP.Core -warnaserror` → 0 warnings, 0 errors; Core is pure C# (0 `UnityEngine` references).
- `dotnet test Code/OpenKSP.Core.Tests -warnaserror` → **Passed 19, Failed 0, Skipped 0**, twice in a row (1 s each); discovery lists exactly T1–T10 (10 classes, 19 methods).
- Hygiene: no `DateTime`/`Random`/`Time.` and no LINQ call sites in Core; `planets.json` parses (sun/earth/moon); hot paths allocation-free.

## Task 7 re-run (`Docs/steps/step-01-review.md`) — verdict: **CHANGES NEEDED (1 MAJOR)**

Cold read of `git diff 3f953ed..main` (41 files, 2,632 lines) against the 7-point checklist. The previous MAJOR (Find-in-Update) is confirmed fixed. One **new** genuine finding:

- **[MAJOR] `CBodyLoader.ResolveDefaultPath()` does not work under Unity** (lines 26, 34–44): it walks 5 parent dirs up from `AppDomain.CurrentDomain.BaseDirectory` (the dotnet build output → repo root), but under the Unity Editor/player that base dir is the editor/`game.data` dir, so `CBodyLoader.Load(null)` throws `FileNotFoundException` in `SolarSystemStub.Awake` before any frame. Suggested fix (next task, coder): the bridge builds the path in `Awake` — `Path.Combine(Application.dataPath, "StreamingAssets", "Planets", "planets.json")` — and passes it explicitly to `Load(path)`; Core's dotnet default stays for tests.

Carry-over minors unchanged: per-frame HUD string allocation (display-only), unused `using System.Linq` in `GravityField.cs`, `Coordinates.Scale` identity-with-dangling-comment, `WorldState.Bodies` allocating-getter risk, public `SetWarpRate`, `OrbitPropagator` body-count `foreach`.

Known gap (pre-existing, carried): `OpenKSP.Core` lives outside `Assets/` — the Unity project needs an asmdef/prebuilt-DLL reference plus a valid license before a real Editor run can happen.

**Status:** fixes committed (`c2b6dbf`); Tasks 6 & 7 re-run and deliverables updated; report generated. The remaining MAJOR (data-path under Unity) is the next coder item.
