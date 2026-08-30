# BladeLoop — Technical Reference

**Author:** Ritwika Sen · 30 August 2026
**Describes the codebase as it stands on current `main`.** This is what we build on top of.

Shared reference for everyone's Claude. For *what we're building*, read
`docs/BLADELOOP-PRODUCT-VISION.md`. Plant Explorer credit: Sharan built the settings dashboard
described in §3–§6.

---

## 1. Project facts

| | |
|---|---|
| Unity | 6000.4.7f1, URP |
| Target | WebGL (edited on macOS/Metal) |
| Input | **New Input System only** (`activeInputHandler: 1`) — `OnMouseDown` and the legacy `Input` class never fire |
| Camera | Cinemachine 3 — a `CinemachineTrack` does nothing unless `director.SetGenericBinding(track, brain)` is called |

---

## 2. Scenes and the tour chain

`TourSceneSequencer.cs` is `DontDestroyOnLoad` and chains five scenes back to back, advancing when
each scene's `PlayableDirector` finishes:

```
Stage1_StoryMode → Transport_StoryMode → Stage2_StoryMode → Stage3_StoryMode → Stage4_V2
     43 s                13 s                  32 s               84 s            86 s
```

≈ 4.5 minutes including fades and end-holds.

Other scenes: `MainMenu`, `PlantExplorer`, `SeparationExplorer`, `FullPlantTour`, `_Dashboard`,
`Stage4_StoryMode` (superseded by `Stage4_V2`), `Stage5_StoryMode` (**out of the build**).

**Scene files cannot be git-merged.** One owner per scene — see §8 of the vision doc.

---

## 3. `ProcessModel.cs` — the maths

Pure arithmetic, no Unity UI. This is the brain of the whole product.

**Four inputs:**

| Input | Range | Optimum | Represents |
|---|---|---|---|
| `TempC` | 400–700 °C | **600** | Kiln temperature (indirect, oxygen-free) |
| `RetentionMin` | 30–45 min | **35** | Time in the rotary kiln |
| `FeedKgH` | 4,000–9,000 kg/h | **6,500** | Shredded material fed in |
| `ParticleSizeMm` | 1–20 mm | **2** | Shredded feedstock size |

**How the numbers are computed:**

- Per-input deviations `DevTemp`, `DevRetention`, `DevFeed`, `DevParticle` (0 = perfect, 1 = worst)
- `OverallDeviation` = `Temp 0.30 + Retention 0.22 + Feed 0.16 + **Particle 0.32**`
- `EfficiencyPct` = `(1 − OverallDeviation) × 100`
- `SystemStatus` = Optimal (dev < 0.15) / Caution (< 0.45) / Critical
- `LossFraction` = 1.5 % baseline, driven 75 % by particle size and 25 % by overall deviation, capped at 10 %
- `OutputSplit()` returns kg/h and % for all five streams; losses come out of the feed first, the
  four products share the rest, **everything sums to `FeedKgH`**
- `FiberPurityPct`, `TensileRetentionPct` — quality metrics that fall as conditions worsen

> **Particle size carries the heaviest weight (0.32) — more than temperature.** 2 mm gives even
> heat penetration and complete decomposition. Bigger particles keep a cold core → incomplete
> decomposition → poorer fibre and more losses.

**Five output streams**, at optimum:

| Stream | Share | kg/h |
|---|---|---|
| Glass fibre | ~69 % | 4,482 |
| Oil | ~16 % | 1,024 |
| Syngas | ~8 % | 512 |
| Char dust | ~6 % | 384 |
| Losses | ~1.5 % | 98 |

Baseline proportions (fibre 70 / oil 16 / syngas 8 / char 6 of the recovered stream) come from the
CEE reference model. Oil takes mass older versions gave to syngas — that's why syngas is ~8 %, not
~24 %. Don't "fix" syngas without re-balancing oil.

**Live explanation strings** — `TempInfo()`, `RetentionInfo()`, `FeedInfo()`, `ParticleInfo()`, plus
`CauseEffect()`, `OutputConsequence()`, `QualityConsequence()`, `ExplainNow()` and per-stream
`GlassInfo()` / `OilInfo()` / `CharInfo()` / `LossInfo()` / `PurityInfo()` / `TensileInfo()`.
Reword there and the UI picks it up automatically.

> ⚠️ **Sprint rule:** `ProcessModel.cs` is now depended on by Akshat's solver, Anirban's visuals and
> the order presets. **Additive edits only — never change an existing formula.** If you think one is
> wrong, message Ritwika.

---

## 4. The dashboard is built in C#, not in the Scene view

**There are no UI prefabs.** Open `PlantExplorer.unity` and you will *not* see the dashboard laid
out. The only saved objects are the controller GameObject, `Main Camera`, the 3D kiln
(`ReactiveKiln` — **being removed**), `KilnBackdrop` (**being removed**) and a `PostProcessVolume`.

The Canvas, all UI, and even the EventSystem are created at runtime by
`PlantExplorerController.cs`.

**So: to change wording, colours or layout, edit the C# file.** Clicking around the scene won't
work — the objects don't exist until Play.

Every label is made with `MakeText(parent, "name", "text", size, colour, alignment)`; sliders with
`MakeSlider(...)`. The palette is at the top of the controller. Light theme is deliberate.

---

## 5. Where to edit what

| You want to… | Edit |
|---|---|
| Reword the live ⓘ popup text | `ProcessModel.cs` → `TempInfo()`, `RetentionInfo()`, `FeedInfo()`, `ParticleInfo()` |
| Change baseline output %s | `ProcessModel.cs` → `OutputSplit()` intercepts |
| Change loss baseline / cap | `ProcessModel.cs` → `BaseLossFrac`, `MaxLossFrac`, `LossFraction` |
| Change efficiency / deviation weights | `ProcessModel.cs` → `OverallDeviation` |
| Change a slider label, range, optimum | `PlantExplorerController.cs` → `MakeSlider(...)`; optima are `OptTemp` etc. in `ProcessModel.cs` |
| Change colours, fonts, spacing | `PlantExplorerController.cs` → palette + `Build*` methods |

---

## 6. Key scripts, by area

**Story / tour**
`TourSceneSequencer.cs` (chains the five scenes) · `StoryModeController.cs` (pause, Explore mode,
back to menu) · `BackToMenuButton.cs` · `CameraPullback.cs` · `SceneLoader.cs`

**Explore mode** (pause and orbit)
`ExploreOrbitCamera.cs` (drives `Camera.main` directly while paused) · `ExploreClickRaycaster.cs`
(New Input System replacement for `OnMouseDown`) · `ExploreHintChip.cs` · `PartInfoPanel.cs` ·
`ClickablePart.cs` · `PauseFramePreserver.cs`

**Dashboard**
`ProcessModel.cs` · `PlantExplorerController.cs` · `KilnVisualizer.cs` (miniature kiln — **being
removed**) · `PlantModel.cs` · `SeparationController.cs`

**Stage visuals**
Stage 1: `WindFarmBladeAnimator.cs`, `ShearsAnimator.cs`, `BladeRotor.cs`, `CraneAnimator.cs`
Transport: `TruckDriving.cs`, `TruckDumpAnimator.cs`, `WheelRoller.cs`, `TransportBeats.cs`
Stage 2: `ConveyorBeltScroller.cs`, `Stage2CraneFeeder.cs`
Stage 3: `TemperatureRampAnimator.cs`, `KilnRotator.cs`, `BurnerFlicker.cs`,
`AirlockDoorCycle.cs`, `AirlockFlowController.cs`, `TowerStatusPanel.cs`
Stage 4: `KilnRotator.cs`, `CutawayToggler.cs`, `CutawayTimelineTrigger.cs`

> ⚠️ **`KilnVisualizer.cs` is only in `PlantExplorer`, not in Stage 3.** Its `SetHeat()` /
> `SetRotation()` methods belong to the miniature kiln being removed. Stage 3's equivalent is
> `TemperatureRampAnimator.cs`, which currently hardcodes `tempEnd = 620f`.

---

## 7. Useful object names

**Stage 2** — `S2_OutputPile_0` … `S2_OutputPile_19` (20 granule objects), framed by camera
`CAM_S2_04_OutputGranules`.

**Stage 4** — particle systems `EL_PS_FibreToBox`, `EL_PS_CharToDrum_0`, `EL_PS_CharToDrum_1`;
containers `EL_Fib_Box`, `EL_Char_Drum_0/1`.

---

## 8. Audio

All in `Assets/Audio/`:

| File | Length | Stage |
|---|---|---|
| `Segment1_VO.mp3` | 41.3 s | Stage 1 |
| `Segment2_Transport_VO.mp3` | 9.8 s | Transport |
| `Stage2_Shredder_VO.mp3` | 29.7 s | Stage 2 |
| `Stage3_Airlock_VO.mp3` | 27.1 s | Stage 3, airlock |
| `Stage3_Kiln_VO.mp3` | 50.2 s | Stage 3, kiln |
| `Stage4_V2_VO.mp3` | 74.8 s | Stage 4 |
| `Stage4_Hood_VO.mp3` | 55.7 s | superseded, unused |

`Stage4_V2_VO` is split into 14 timeline clips with `clipIn` offsets. **All of these are being
re-recorded** — see the vision doc §7.

---

## 9. Gotchas that have cost us time before

- **UI positions live in `RectTransform.anchoredPosition`**, not `transform.position`. Setting
  `transform.position` looks like it works and then reverts.
- **`GameObject.Find` ignores inactive objects.** Use
  `FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)`.
- **Scale multiplies under scaled parents.** Parent to a unit-scale holder instead.
- **The Game view runs at roughly 2:1, not 16:9** — the canvas is about 1016 reference units tall,
  not 1080. Elements near y = ±540 clip.
- **Pause is three things, all load-bearing.** `StoryModeController.TogglePause` sets
  `Time.timeScale = 0`, disables the `CinemachineBrain`, and pauses `AudioListener`. Stage animation
  runs on script clocks, not the timeline, so freezing the director alone is not enough. Don't
  simplify it.
- **Never accept VS Code's "Discard Changes" on a material file** — it permanently deletes untracked
  files. We lost `M4V2_Part_Char.mat` that way.
- **Particle materials need a texture** or they render as hard squares. Use `Default-Particle.psd`.
- **GitHub Pages cannot serve Git LFS files.** The WebGL build must not route through LFS.
- **MCP-for-Unity:** if the Claude ↔ Unity bridge disconnects, fully quit and reopen Claude Desktop
  including the tray icon. The Unity server is usually fine; it's the client that needs reconnecting.
