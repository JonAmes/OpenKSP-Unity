# step-01-plan — Phase 1 Core Coordinates

**NEXT: coder, task 1**

| # | Task | Files | Test | Owner | Status |
|---|------|-------|------|-------|--------|
| 1 | Math foundation `D3`, `DTime` (operators, ToFloat/FromFloat); test project scaffold; T1 | Code/OpenKSP.Core/Math/*, Code/OpenKSP.Core.Tests/* | T1 | coder | [x] |
| 2 | Bodies & data driver: `CBody`, `CBodyLoader` (real JSON parse per §6), `planets.json`; T2, T3 | Code/OpenKSP.Core/Celestial/*, Assets/StreamingAssets/Planets/planets.json | T2, T3 | coder | [x] |
| 3 | World state, gravity, propagation: `GravityField`, `OrbitPropagator` (RK4), `WorldState` (Step, TotalEnergy, snapshot/restore); T4, T5, T9 | Code/OpenKSP.Core/Orbit/*, Celestial/* | T4, T5, T9 | coder | [x] |
| 4 | Floating origin, time warp, kepler round-trip: `FloatingOrigin`, `TimeWarpController`, `KeplerianElements`; T6, T7, T8, T10 | Code/OpenKSP.Core/* | T6, T7, T8, T10 | coder | [x] |
| 5 | Unity bridge scaffolding: PhysicsProxy, FloatingOriginSystem, SolarSystemStub (C# 9, no per-frame allocs in Update) | Assets/Plugins/OpenKSP/Runtime/* | static review (no test here) | coder | [x] |
| 6 | Bug-tester gate per Docs/BugTestPlan.md | Code/** | T1–T10 ×2, build, greps | qa (bug tester) | [x] (Docs/steps/step-01-bugtest.md) |
| 7 | Cold review of final diff | docs/steps/step-01-review.md | — | reviewer | [x] (Docs/steps/step-01-review.md) |

Open questions: none blocking. `OpenKSP.sln` absence is accepted — tests invoked directly on the test csproj; if the coder needs an sln, they must report, not invent packages.
