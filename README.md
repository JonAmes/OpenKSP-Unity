# OpenKSP

A *Kerbal Space Program*-style space simulation built in **Unity 6.0.0f1** (URP 17.6), with the heavy lifting done by a small, pure, double-precision C# core.

**Version: 0.1.0** (core project version)

## What is in this repo (as of step 1)

| Area | Path | Description |
|---|---|---|
| **Core simulation** | `Code/OpenKSP.Core/` | Pure C# library — no `UnityEngine` refs. Double-precision N-body orbital dynamics: `D3`/`F3` coordinate types, `CBody` body records, `GravityField`, `OrbitPropagator` (RK4, whole-state displacement, no per-step allocations), `WorldState` (step, `TotalEnergy` per design §9, bit-exact `WorldSnapshot`), `FloatingOrigin` (float-local zone, origin sliding), `TimeWarpController` (fixed 1/1200 s steps, warp change at frame boundaries), `KeplerianElements` (state↔elements round-trip). |
| **Test suite** | `Code/OpenKSP.Core.Tests/` | xUnit; T1–T10 cover math round-trip, JSON loading, RK4 circular-orbit residual, two-body energy conservation, determinism (bit-identical snapshots), floating-origin sliding, time-warp stepping, Kepler round-trip, origin-crossing. |
| **Unity bridge** | `Assets/Plugins/OpenKSP/Runtime/` | MonoBehaviours (C# 9): `PhysicsProxy` (sim → one `Rigidbody`), `FloatingOriginSystem` (fixed-dt stepping + floating origin + time warp, owns `FixedUpdateLoop` in the same file), `SolarSystemStub` (editor-scene entry, HUD). |
| **Body data** | `Assets/StreamingAssets/Planets/planets.json` | sun / earth / moon, loaded by `CBodyLoader` (real JSON parse per design §6). |
| **Docs** | `Docs/` | Design specs (`Phase1-Design.md` v1.1), step plans & reports (`steps/`), role definitions (`agents/`), bug-test plan. `IDEA.md` holds the high-level feature vision. |

## Core invariants

- **Double math only** in the sim core; Unity objects only *display* it.
- **C# 9** throughout (no `with` expressions or target-typed new).
- **Allocation-free hot paths** — no per-frame `CBody` reconstruction in `WorldState.Step` or in the bridge's per-frame reads.
- **Bit-exact snapshots** — `WorldSnapshot` compares by 64-bit pattern, so two identical runs produce exactly-equivalent snapshots.
- **C# 9 + .NET 8**; `OpenKSP.Core` targets both **netstandard2.1** and **net8.0**.

## Build & test (headless — no Unity editor required)

```bash
export PATH=/home/jonames/.dotnet:$PATH
dotnet build Code/OpenKSP.Core.Tests -warnaserror
dotnet test  Code/OpenKSP.Core.Tests
```

Current status: **0 warnings, 0 errors; 19/19 tests pass** (T1–T10).

> The Unity-side bridge (`Assets/Plugins/OpenKSP/Runtime/`) compiles only inside the Unity editor — it is validated here by static review against the real Core API.

## House rules (from AGENTS.md)

1. Sim state lives in plain C# under `Code/`; no `UnityEngine`.
2. Never hand-edit `.unity`, `.prefab`, `.meta`, `ProjectSettings`.
3. C# 9 only.
4. `dotnet test` in `Code/OpenKSP.Core.Tests` — never claim work done unless it passed.
5. Stay inside the folders named in the step spec.
6. Files are the memory: read the step file; write to the file named for your role.
7. Unclear / blocked → write it under *Open questions* and stop; do not guess.
8. Be honest — list what you could not verify.

## Versioning

Version `0.1.0` lives in `Code/OpenKSP.Core/MetaInfo.txt`. Step 1 deliberately does **not** bump it.
