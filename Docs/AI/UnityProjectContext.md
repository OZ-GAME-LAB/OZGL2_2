# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- Project root: `D:/Documents/GitHub/OZGL2_2`
- Last analyzed: 2026-09-08 (local HUD/building/unit UI work; latest evidence in `Docs/UI/UnitInfoUiValidation.md`)
- Last analyzed commit: `e472459fc88d5d8970cce00734c1834d504273bc`
- Current feature branch: `feature/ui-common-hud`

## Confirmed Environment

- Unity version: 6000.3.23f1 (09d2ecc7fb28)
- Render pipeline: Universal Render Pipeline 17.3.0 with a 2D Renderer asset
- Input system: Unity Input System 1.20.0
- Target platforms: Unknown; no team target-platform documentation is present

## Important Packages And Frameworks

| Area | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Runtime UI | uGUI 2.0.0 + TMP; local common HUD implementation and isolated demo | Confirmed | `Assets/Scripts/UI`, `Assets/Prefabs/UI/MvpCommonHud.prefab` |
| 2D rendering | URP 17.3.0 and 2D Renderer assets are configured | Confirmed | `Packages/manifest.json`, `ProjectSettings/QualitySettings.asset`, `Assets/Settings/Renderer2D.asset` |
| Input | Input System 1.20.0 is active | Confirmed | `Packages/manifest.json`, `ProjectSettings/ProjectSettings.asset` |
| Testing | Unity Test Framework 1.6.0 is installed; no first-party test assembly exists | Confirmed | `Packages/manifest.json`, `Assets/` |
| Networking | No first-party networking implementation was found | Confirmed | `Packages/manifest.json`, `Assets/` |

## Directory Structure

| Path | Purpose | Confidence | Evidence |
| --- | --- | --- | --- |
| `Assets/Scripts/UI` | Runtime UI code | Confirmed | Team folder convention and repository structure |
| `Assets/Prefabs/UI` | Reusable UI prefabs | Confirmed | Team folder convention and repository structure |
| `Assets/Scenes` | Bootstrap, main, UI, and test scenes | Confirmed | Repository structure |
| `Assets/Data` | Units, buildings, waves, economy, artifacts, and events data | Confirmed | Repository structure |

## Assembly Boundaries

- No first-party `.asmdef` or `.asmref` exists at the analyzed commit. Local UI scripts now exist.
- New scripts currently compile into Unity's default runtime assembly unless the team defines assembly boundaries.

## Scenes And Startup Flow

- Build scenes: `Assets/Scenes/SampleScene.unity`
- Likely startup scene: `Assets/Scenes/SampleScene.unity`
- Scene loading flow: Unknown; the scene currently contains only the default camera and 2D global light.
- UI-only demo: `Assets/Scenes/Test/MvpHudTest.unity`. Not added to shared Build Settings.
- Building info demo: `Assets/Scenes/Test/MvpBuildingInfoTest.unity`; display-only snapshots, no real building/economy logic. See `Docs/UI/BuildingInfoUiSpec.md` and `Docs/UI/BuildingInfoUiValidation.md`.
- Unit info demo: `Assets/Scenes/Test/MvpUnitInfoTest.unity`; instance-keyed selection/health snapshots, no real unit/combat logic. Not added to shared Build Settings.

## Architecture

| Pattern | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Runtime architecture | Core teammate branch has `Game.Core.GameFlowController`; not integrated into this branch | Confirmed | `origin/feature/core-game-test` at `17f404f` |
| UI architecture | `Game.UI.GameUIController` displays authoritative values and emits intent events; demo-only data is isolated | Confirmed | `Assets/Scripts/UI`, `Docs/UI/MvpCommonHudSpec.md` |
| Folder organization | Feature/system folders are prepared under `Assets/Scripts`, `Assets/Data`, and `Assets/Prefabs` | Confirmed | Repository structure |

## Coding Conventions

- Member order: public constants/events/properties, serialized fields, dependencies/cache, runtime state, lifecycle, public methods, handlers, internal methods.
- Serialized fields: `[SerializeField] private`.
- Naming: private fields use `_camelCase`; methods/events use `PascalCase`; parameters and locals use `camelCase`.
- Public contracts: `Can...`, `Get...`, `Try...`, and `Request...` follow the team's documented behavior rules.
- Existing UI namespace: `Game.UI`; inspected remote core namespace: `Game.Core`. Retain these; no folder migration.

## Testing And Validation

- EditMode tests: none found.
- PlayMode tests: none found.
- CI/build validation: none found.
- Current validation surface: repository inspection, Unity 6000.3.23f1 batch compilation, `Game/UI/Validate MVP HUD`, Editor Play Mode and screenshots. No Unity-specific MCP provider is configured.

## Available Unity Tooling

| Capability | Status | Evidence |
| --- | --- | --- |
| Unity project and Editor logs | available | Local filesystem and running Editor |
| Unity Editor UI control | available | Windows computer-use surface |
| Unity-specific MCP provider | unavailable | No provider package/config or callable Unity MCP tools found |

## Important Constraints

- Work on a feature branch and open a PR when it compiles, runs, preserves scenes/prefabs, and has a clear description.
- PR target is documented as `TutorialMap`, but that branch does not currently exist on the remote.
- Unity assets and their `.meta` files must be committed together.
- Do not modify shared scenes or prefabs until ownership and integration flow are agreed.
- `ProjectSettings/ProjectSettings.asset` contains a credential-like platform setting; do not reproduce its value in documentation or output.

## Unknowns And Confidence

- Current common HUD uses uGUI/TMP. Do not introduce a second UI framework for this feature.
- Actual core/economy linkage remains a separate integration task; the sample is not production gameplay.
- Building selection/info presentation: 113 Editor checks and actual mouse Play smoke passed (four categories, refresh, close/clear). Building action UI: 150 Editor checks, plus 113 info and 98 HUD regression checks, and actual mouse Play request/response smoke passed. See `Docs/UI/BuildingActionUiSpec.md` and `BuildingActionUiValidation.md`. Test scene: `Assets/Scenes/Test/MvpBuildingActionTest.unity`; no actual building/economy changes. Contracts remain UI-side proposals, not finalized team APIs.
- Unit info UI: 157 Editor checks plus 361 existing UI regression checks passed; actual Play selection, health/zero, stale callbacks, removal, close/reselect, wheel scrolling and reading-position retention passed. `UnitInfoData` / `UnitInfoPanel` are UI-side contracts; use spawn-lifetime unique IDs, not unit type IDs. Actual unit selection/stats/death and building-panel switching remain unconnected. See `Docs/UI/UnitInfoUiSpec.md` and `Docs/UI/UnitInfoUiValidation.md`.
- MVP loss condition conflicts across documents and must be resolved before result UI integration.
- `TutorialMap` branch creation and the exact feature-branch base/PR flow are unresolved.

## Source Files Inspected

- `README.md`
- `ProjectSettings/ProjectVersion.txt`
- `ProjectSettings/EditorBuildSettings.asset`
- `ProjectSettings/ProjectSettings.asset`
- `ProjectSettings/GraphicsSettings.asset`
- `ProjectSettings/QualitySettings.asset`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `Assets/Scenes/SampleScene.unity`

<!-- unity-onboarding:generated:end -->
