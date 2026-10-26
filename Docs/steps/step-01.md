# Step 01 — Phase 1: Core Coordinates (per Docs/Phase1-Design.md v1.1)

**Status:** planned — execution pending.
**Basis:** `Docs/Phase1-Design.md` (review-approved v1.1). This file defines scope only; the design doc is the authoritative spec.

## Goal
Deliver the double-precision simulation core (`OpenKSP.Core`) plus the Unity-side bridge scaffolding, with the T1–T10 test suite green under `dotnet test`, zero compiler warnings under `-warnaserror`, and no UnityEngine references in Core.

## Folders to touch
- `Code/OpenKSP.Core/` — Math/, Celestial/, Orbit/ (design §4 layout: add Propagation/, World/, Time/ subfolders as the design lays out; existing files may be reworked to match the design)
- `Code/OpenKSP.Core.Tests/` — xUnit test project (currently empty), T1–T10
- `Assets/Plugins/OpenKSP/Runtime/` — Unity MonoBehaviours (FloatingOriginSystem, PhysicsProxy, SolarSystemStub) — C# 9, Unity 6.0.0f1 / URP 17.6 compatible, must not break the pure library
- `Assets/StreamingAssets/Planets/planets.json` — body data per design §6

## Tests that must pass
Design §8, T1–T10, run via `dotnet test Code/OpenKSP.Core.Tests/`.
Gate (see Docs/BugTestPlan.md): all green, twice in a row, zero warnings, hygiene greps clean, planets.json valid.

## Environment (this host, no Unity editor)
- `export PATH=/home/jonames/.dotnet:$PATH`
- No `OpenKSP.sln` exists yet; tests run via `dotnet test Code/OpenKSP.Core.Tests/` on the test project directly.
- C# 9 only (`LangVersion` is already 9 in the csproj).

## Out of scope (Phase 1)
No orbital UI, ship model, mod config parser, networking, multiplayer, keplerian-UI features beyond the T8 round-trip. Unity scene work is hand-done by the human on their editor box — we only write C# and JSON.

## Current state (as found)
- `D3.cs`: struct with fields but no operators/ToFloat/FromFloat — incomplete.
- `CBody.cs`: mixes a D3 + a DVector (duplicated), derives mass from mu with hard-coded G — design says mu-only.
- `CBodyLoader.cs`: no real JSON parsing; silent fallback to hard-coded bodies.
- `WorldState.cs`: partial struct, does not compile (CS1513).
- `OrbitPropagator.cs`: does not compile; `_bodies` never used; placeholder ComputeAcc.
- `OpenKSP.Core.Tests/`: empty — no .csproj.
- `Assets/Plugins/OpenKSP/Runtime/`: empty.
- Build currently FAILS.
