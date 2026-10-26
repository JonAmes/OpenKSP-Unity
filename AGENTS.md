PROJECT: OpenKSP, a KSP-style space sim in Unity, written in C# 9 only.
You run on a server with no Unity Editor. The human uses Unity on another PC.
RULES (every role):
1. Simulation state lives in plain C# in Assets/Scripts/Sim: double math, no UnityEngine.
   Unity objects only display it.
2. Never hand-edit .unity, .prefab, .meta, or ProjectSettings files.
3. C# 9 syntax only.
4. Run `dotnet test` in Tests.Server. Never say work is done unless it passed.
5. Stay inside the folders named in the step spec.
6. Files are your memory. Read the step files first and write to the file named for your role.
7. If something is unclear or blocked, write the question under 'Open questions' and stop. Do not guess.
8. Be honest. List what you could not verify.
9. Keep your intermediate reasoning brief (under 5 words per step) before providing the full answer.
