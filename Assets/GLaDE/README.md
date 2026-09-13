# GLaDE — VR statics lab

GLaDE turns textbook statics problems into things a student physically takes apart in VR:
isolate a body, replace everything it touched with a force, then step through the equilibrium
equations on a whiteboard that explains *why* each equation is the right one to write next.
Every attempt draws new numbers, so the method is what gets learned, not the answer.

## Running it

* **Editor, no headset:** open `Assets/GLaDE/Scenes/Hub.unity` and press Play. The XR Interaction
  Simulator spawns automatically (see `SimulatorBootstrap`) — press `Y` in the Game view for its
  key map; the mouse and keyboard drive the head and controllers.
* **Headset:** OpenXR is configured for Standalone and Android with the Meta Touch / Touch Pro / Touch
  Plus and Khronos simple controller profiles. Install the Android Build Support module for Quest builds.
* **Whiteboard shortcuts (desktop testing):** Space / → next step, ← back, H hint, N new problem.

Build order: `Hub` → `Problem_Truss` → `Problem_Beam`, then the legacy desktop menu scenes.

## Architecture (Assets/GLaDE)

| Folder | What lives there |
|---|---|
| `Scripts/Core` | Pure C#: `StaticsProblem` asset schema, expression evaluator, 2D equilibrium solver, `SolutionGenerator` (writes the teaching steps from the real numbers), `ProblemGenerator` (randomises within valid ranges). No Unity scene dependencies, easy to unit-test. |
| `Scripts/Problems` | Runtime: `ProblemManager` (phase loop), `StructureView` (3D model), `SectionPlane`, `ForceToken` / `ForceSocket` (free-body diagram building), `Whiteboard`, `ProblemHub`. |
| `Scripts/Editor` | `GLaDE` menu: authors the problem assets, materials, theme and builds every scene from code. |
| `Data/Problems` | The problem assets. **New problems are new assets, not new code.** |
| `Scenes` | Generated scenes. Re-run *GLaDE ▸ Build Everything* after changing the builder. |

### The loop a student goes through

1. **Isolate** — trusses: grab the section plane and cut through the members asked about (only a cut
   through the planned members is accepted; other cuts get specific feedback). Beams: lift the beam off
   its supports.
2. **Choose a side** (trusses) — grab the half to keep; the other half ghosts out. Keeping the "other"
   half is fine: the solution text is regenerated for that side.
3. **Build the FBD** — drop unlabeled force tokens on every cut member end and support reaction. A token
   takes its name and direction from where it lands, so the student decides *where* forces act.
4. **Solve** — the whiteboard reveals one step at a time. Every equation is built from the instance's
   geometry (moment arms, angles, signs), so it always matches the model in the room.

`Hint` gives phase-specific guidance; after a hint, `Show me how` performs the current phase and marks
the attempt as assisted.

## Authoring a new problem

1. Duplicate an asset in `Data/Problems` (or *Create ▸ GLaDE ▸ Statics Problem*).
2. Describe the geometry with expressions over your parameters: nodes (`x = "2*L"`), members, supports
   (pin / roller / fixed), concentrated loads (magnitude expression + angle) and distributed loads.
3. Give each parameter a min / max / step. The generator rejects draws that are degenerate, unstable,
   statically indeterminate, produce absurd force ratios, or make every target answer zero.
4. Write the solution plan as an ordered list of steps: `FreeBodyWhole`, `Reactions` (moment point),
   `Resultants`, `Section` (cut members, a node on the kept side, equations such as `M:D=KJ`, `Fy=KD`,
   `Fx=CD`), `Joint`, `FinalAnswer`. Optional `teachingNote` text is appended to the generated step.
5. Add the asset to `Data/Problem Library.asset`, set its `sceneName`, and either reuse a problem scene
   or build one with `GLaDESceneBuilder.BuildProblemScene`.

`ProblemAssetFactory.cs` shows both shipped problems written out in full.
