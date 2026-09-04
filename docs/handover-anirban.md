# Claude Brief — Anirban

**Read `docs/BLADELOOP-PRODUCT-VISION.md` first.** This file is the build detail for your tasks.

Branch off current `main`: `feature/parameter-visuals`
Start **today, Mon 31 Aug** · visuals done **Mon 7 Sep** · feature freeze **Wed 9 Sep**

---

## The goal

**Make it obvious, by looking at the screen, that the user's settings changed the plant.**

When you're done, someone watching from across the room should be able to tell a high-grade run
from a low-grade run without reading a number.

You own all five stage scenes. Nobody else opens them.

---

## Paste this to your Claude

> I'm working on BladeLoop, a Unity 6 (6000.4.7f1) URP WebGL project that visualises thermal
> co-processing of wind turbine blades. Read `docs/BLADELOOP-PRODUCT-VISION.md` in the repo root
> for product context, then `docs/handover-anirban.md` for my tasks.
>
> Project constraints:
> - **New Input System only** (`activeInputHandler: 1`). `OnMouseDown` and the legacy `Input`
>   class never fire. Use `Mouse.current` / `Keyboard.current`.
> - UI positions live in `RectTransform.anchoredPosition`, **not** `transform.position`. Setting
>   `transform.position` on a UI element looks like it works and then reverts.
> - `GameObject.Find` ignores inactive objects. Use
>   `FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)`.
> - Watch for scale multiplication — parenting under a scaled parent multiplies scale. Parent to a
>   unit-scale holder instead.
> - I own the stage scenes only. Do not edit `ProcessModel.cs`, `PlantExplorerController.cs`,
>   `OrderContext.cs`, `OrderSolver.cs`, `TourSceneSequencer.cs`, `StoryModeController.cs` or
>   `SubtitleTrack.cs` — other people own those.
>
> Work one task at a time and check the Unity console for compile errors after each change.

---

## Schedule

✅ **`OrderContext.cs` is on `main` now** — complete, not a stub. Ritwika wrote it so you weren't
queued behind Akshat. Pull and wire `OrderContext.HasOrder` and `OrderContext.Model` directly.

⚠️ **The split width now lives in one place: `OrderContext.TourSplitWidth` (0.72).** Your
`TourViewportFrame.splitWidth` is a serialised field, so the 0.72 is baked into all 14 canvases —
left alone for now rather than editing your file mid-sprint. **Next time you touch
`TourViewportFrame`, default it from `OrderContext.TourSplitWidth`.** Akshat's `Camera.rect` reads
the constant; if the two ever drift, your overlays sit slightly off the edge of the 3D view and
nothing on screen explains why.

⚠️ **Two things in `MainMenu` that touch your world — read before you pull.**
>
> The home page now instantiates the wind farm from `Stage1-WindFarm.fbx` as a `HomeStage`
> object, keeping only `WF_Turbine_*` and `WF_Terrain`. **Your Stage 1 scene is untouched** —
> nothing was reparented or moved, and the FBX itself is unchanged.
>
> The terrain looked like bright daytime grass against a night sky, so it is dimmed — but
> **at runtime, via a `MaterialPropertyBlock` in `MainMenuController.DimHomeTerrain()`, not by
> editing the material.** An earlier attempt edited `WF_Mat_Terrain.mat` directly, which was
> wrong twice: that material is shared with Stage 1 through the FBX, and multiplying a Color
> scales alpha too, so it also went 42 % transparent. Both were reverted. **If you ever want to
> tint shared geometry for one scene, use a property block** — it cannot leak into the asset.

✅ **You can finally test your Task 1.** `TourRunner.StartRun()` now exists, so an order can be set
and a run started — which is what flips `HasOrder` and makes your frames engage. Until Akshat lands
`Camera.rect`, a run shows overlays confined left with the 3D still full width. **That looks wrong
and is correct** — it is your 14 frames working.

Start **Task 1** today. It's the largest single item in your list and it's Monday's work regardless.

**Priority if the week runs short:** Task 1 is not cuttable — without it every screenshot of the dual
screen has overlays smeared across the order panel. After that your proposed order stands:
**4 → 2 → 3 → 5 → 6 → 7.** Cut from the bottom: Task 7 first, then Task 6, then Task 3's Change 4
(feed → airlock flow, least visible), then Stage 4's optional cues.

**Your two dependencies:** Akshat's Explore spec and his FOV compensation, both due **Wed 2 Sep**.

---

## Task 1 — Wrap all 14 overlay canvases 🔴 the dual screen depends on this

Akshat splits the window from code via `Camera.rect`: 3D tour in the **left 72 %**, order panel in
the **right 28 %**.

> ⚠️ **All 14 canvases are Screen Space – Overlay, and a Screen Space – Overlay canvas ignores
> `Camera.rect` completely.** It renders straight to the framebuffer, bypassing every camera. So
> Akshat's split narrows the 3D render and leaves **all 14 overlays covering the full screen, on top
> of the order panel.** Not one of them moves on its own. This task is what makes the dual screen
> work — it isn't tidying-up.

The 14, verified on `main`:

| Scene | Canvases |
|---|---|
| `Stage1_StoryMode` | `Stage1_UICanvas`, `ExploreHintCanvas`, `BackToMenuCanvas` |
| `Transport_StoryMode` | `BackToMenuCanvas` |
| `Stage2_StoryMode` | `Stage2_UICanvas`, `ExploreHintCanvas`, `BackToMenuCanvas` |
| `Stage3_StoryMode` | `Stage3_UICanvas`, `ExploreHintCanvas`, `BackToMenuCanvas` |
| `Stage4_V2` | `Stage4_UICanvas`, `SubtitleCanvas`, `ExploreHintCanvas`, `BackToMenuCanvas` |

Components affected: `SubtitleTrack.cs` (Ritwika's), `BackToMenuButton.cs`, `ExploreHintChip.cs`,
`StoryModeController.pauseHintUI`, `PartInfoPanel.cs` (Stage 3).

**How:** put each canvas's content under a parent `RectTransform` anchored `(0,0)` to `(0.72, 1)`.
Every child then stays inside the tour viewport automatically — no repositioning by hand.

**Must not break the non-split case.** With no order the tour is full screen and overlays fill it as
today. Switch the parent's right anchor off `OrderContext.HasOrder` in `Start()` — the real
`OrderContext` is on `main` now, so wire it directly rather than stubbing.

### Three things to fold into the same pass

**1. Add `SubtitleCanvas` + `SubtitleTrack` to Stages 1, 2 and 3.** On `main` today, subtitles exist
in `Stage4_V2` only (one canvas, one component, one cue file). Ritwika owns `SubtitleTrack.cs`, the
cue files and the timings; **the scene-side wiring is yours**, because they're your scenes under
Rule 1. You're rebuilding every overlay canvas in those scenes this week anyway — cheap in one pass,
merge-conflict-prone in two.

**2. Normalise `Stage2_UICanvas`.** It has `m_MatchWidthOrHeight: 0`; the other 13 are `0.5`. At ~2:1
that's a 58-unit difference in canvas height (960 vs 1018), which is why Stage 2's overlays have
needed hand-nudging. Set it to 0.5 and re-check Stage 2's positions.

**3. Transport is a pass-through.** Panel stays docked, viewport split applies, but **no chapter
chip** — it's a 13-second transition, not a stage. One canvas to wrap.

⚠️ The Game view runs at roughly **2:1 aspect, not 16:9**, so at match 0.5 the canvas is about
**1018** reference units tall, not 1080 — half-height is ~509 and anything at y = ±540 is off-screen.
That's the Stage 3 kiln-heading bug.

---

## Task 2 — Stage 2: make particle size visible 🎯 highest impact

**Your most important task.** Particle size carries weight **0.32** in the model — more than
temperature — and Stage 2 is where it's set.

The scene already contains **20 granule objects named `S2_OutputPile_0` … `S2_OutputPile_19`**, and
a camera `CAM_S2_04_OutputGranules` that frames them. Everything you need exists.

New script: `Assets/Scripts/ShredOutputSizer.cs`

| `ParticleSizeMm` | Granules should look like |
|---|---|
| **2 mm** (high) | small, uniform, dense — coarse sand. Many, tightly packed. |
| **8 mm** (mid) | visibly chunkier, more irregular, gaps appearing |
| **16 mm** (low) | clearly chunky flakes, obviously coarse, loose pile |

Implementation:

- Scale each granule by roughly `particleSizeMm / 2f`, but **vary each ±25 % randomly** so it looks
  like shredded material rather than 20 identical cubes. Seed the randomness so it's stable between
  runs.
- At larger sizes, **hide some granules** — bigger pieces, fewer of them — so pile volume stays
  believable instead of exploding.
- Add random rotation per granule. Coarse shred is angular; fine shred reads smooth.
- If `OrderContext.HasOrder` is false, change nothing.

If time allows: make the conveyor/output stream carry the same size material, so the change reads
earlier in the stage rather than only at the pile.

---

## Task 3 — Stage 3: make temperature and retention visible

Stage 3 uses **`TemperatureRampAnimator.cs`**, which ramps from `tempStart = 25 °C` to a
**hardcoded `tempEnd = 620 °C`**, driving the kiln shell colour, burner ring, burner nozzles and a
`TextMeshPro` temperature label.

> ⚠️ `KilnVisualizer.cs` with its tidy `SetHeat()` / `SetRotation()` methods is **not in Stage 3** —
> it belongs to the dashboard's miniature kiln, which is being removed from the product entirely.
> Don't go looking for it here. `TemperatureRampAnimator` is the Stage 3 equivalent.

**Change 1 — drive target temperature from the order.** In `Start()`, if `OrderContext.HasOrder`,
set `tempEnd = OrderContext.Model.TempC`. The existing colour lerp and label then follow
automatically. That's most of the task.

**Change 2 — widen the visual range** so 550 and 600 look genuinely different. The current colours
are tuned around 620 °C and the difference would be too subtle.

| Temperature | Kiln should read as |
|---|---|
| **600 °C** | bright, even orange — clearly at working heat |
| **580 °C** | noticeably duller, more red than orange |
| **550 °C** | visibly under-fired — dim, dark red, "this isn't hot enough" |

Judge this by eye.

**Change 3 — retention drives rotation.** `KilnRotator.cs` has a public `rpm` (default 1.5).
Longer retention = slower rotation:

```csharp
rpm = 1.5f * (35f / OrderContext.Model.RetentionMin);
```

35 min → 1.5 rpm · 45 min → ~1.17 rpm · 30 min → 1.75 rpm. Keep it subtle — supporting cue, not
headline.

**Change 4 — feed rate drives the charge flow.** `AirlockFlowController.cs` and
`AirlockDoorCycle.cs` control how material enters. Higher feed rate = more material, moving faster.
Your judgement on how; keep it clearly readable.

---

## Task 4 — Stage 4: make the output split visible

`Stage4_V2.unity` is where the user sees what they got. Three particle systems already exist:

```
EL_PS_FibreToBox      → fibre stream into the fibre box
EL_PS_CharToDrum_0    → char stream into drum 0
EL_PS_CharToDrum_1    → char stream into drum 1
```

Drive their **emission rates** from the output split:

```csharp
var split = OrderContext.Model.OutputSplit();
// split.GlassPct ≈ 47% (low grade) … 69% (high grade)
// split.CharPct  ≈  6% (high grade) … 27% (low grade)
```

| Run | Fibre stream | Char streams |
|---|---|---|
| **High** | strong, steady, clean | barely a trickle |
| **Mid** | still strong | clearly visible char flow |
| **Low** | thinner | heavy — obviously the dominant product |

**This is the money shot of the whole app.** On a low-grade run the char drums should be visibly
busy while the fibre box fills slowly.

Supporting cues if time allows:

- **Fibre colour by purity.** `OrderContext.Model.FiberPurityPct` runs ~74 % to ~99 %. High purity =
  clean off-white; low purity = greyer, dirtier. Tint the fibre particle material.
- **Fill levels** on `EL_Fib_Box` and the char drums matching the split. Nice if cheap, skip if not.

---

## Task 5 — Explore mode: make Stage 4 clickable, fix Stage 3

Outstanding professor feedback, previously unassigned: *"when stopping the tutorial in the rotary
kiln, you can only rotate the screen once, then it totally gets stuck. Also, it is not possible to
click any part."*

The *pause/orbit* half is Akshat's (he owns `PauseFramePreserver.cs` and `ExploreOrbitCamera.cs`) —
and he's been told to **play it on current `main` before writing code**, since your PR already
addressed both root causes in his original diagnosis. If it still sticks he'll send you the repro.

**The scene half is yours.** Verified on current `main`:

```
Stage3_StoryMode   22 ClickableParts — every one has an INACTIVE ANCESTOR, so 0 reachable at runtime
Stage4_V2          ClickablePart 0   PartInfoPanel 0   ExploreClickRaycaster 0
```

Ten of Stage 3's sit under `S3_GasBurners`, the rest under `Tower_TopHopper`,
`S3_StationaryShroud`, `S3_StationaryInletHood` and six other disabled parents. **Checking
`m_IsActive` on each component's own GameObject reports 18 "active" and hides the problem
completely — you have to walk the parent chain.** Worth remembering when you verify the fix.

**Stage 4 has nothing clickable at all** — and Stage 4 is the stage the whole product builds toward.

**Akshat is writing you a spec** naming which objects need `ClickablePart`, what title and
description each carries, and where `PartInfoPanel` and `ExploreClickRaycaster` go. Wait for it,
then apply it. Good candidates in Stage 4 are the elutriator, the fibre box, the char drums, the
cyclone, the condenser and the heat exchanger.

For Stage 3, the fix is making some clickable parts reachable when the cutaway is closed — not
moving the 22 that already exist.

---

## Task 6 — Camera framing under the split

⚠️ **Wait for Akshat before touching a single shot.**

Unity holds *vertical* FOV fixed and derives horizontal from aspect, so narrowing the viewport to
72 % cuts 28 % of horizontal field from **every** shot — 37 of them — not just badly-framed ones.
Akshat compensates in code with `newVFov = 2·atan(tan(vFov/2)/0.72)`, restoring the original
horizontal framing for all of them at once. Due **Wed 2 Sep**.

**Your clamp request is accepted:** the compensated value is capped at **65° vertical**, so nothing
inflates into visible perspective stretch or exposes the hill ring and ground plane you built to sit
just outside frame.

⚠️ **One correction to your estimate.** Work the clamp backwards: it bites on any shot whose original
vertical FOV exceeds **~49°**. Your ranges are Stage 3 at 36–55° and Stage 4 at 42–66°, so a good
share of Stage 4's 14 lenses and some of Stage 3's 12 will be only *partially* compensated — more
than the 3–4 you estimated. Not all will look wrong, but budget a review pass across both stages
rather than four fixes.

Once the compensation lands, play each stage in split view and fix what still looks wrong.

**Do not restructure the timelines** — framing only.

---

## Task 7 — Stage 1 blade count *(low priority, only if 1–6 are done)*

Once the order is known we know its feedstock tonnage (`OrderContext.FeedTonnesNeeded`). Reflecting
that in the wind farm's turbine count would be a nice opening beat. **Talk to Ritwika before
starting** — the blade-mass figure is still coming from the CEE team.

---

## What you must not break

- **Free play must be unchanged.** Guard every change with `if (OrderContext.HasOrder)`.
- **Don't restructure the timelines.** The voiceover is being re-recorded to the existing shot
  timings. Move a shot and the narration desyncs.
- **Don't edit other people's files** (listed in the Claude prompt above).
- **Materials:** never accept VS Code's "Discard Changes" on a material file — it permanently
  deletes untracked files. We lost `M4V2_Part_Char.mat` that way once already.

---

## Definition of done

- [x] Overlays stay inside the tour viewport when split, fill the screen when not
      *(Task 1 done — `TourViewportFrame.cs` on all 17 canvases across the 5 scenes; verified in
      play mode: frame width ratio 1.000 with no order, 0.720 with a preset applied, and back to
      1.000 on `OrderContext.Clear()`. Subtitle canvases added to Stages 1–3 — cue files still
      Ritwika's; their `CanvasGroup` alpha is set to 0 because `SubtitleTrack.Update()` returns
      early while `cues.Count == 0` and so never drives alpha, which would otherwise leave an empty
      bar on screen. It self-corrects the moment a cue file is assigned. `Stage2_UICanvas` match
      normalised 0 → 0.5.)*
- [x] Stage 2 granules visibly differ at 2 / 8 / 16 mm
      *(Task 2 done — `ShredOutputSizer.cs`. Real granule meshes ride the output conveyor, sized
      and counted from the order: 130 chips at 6 cm for 2 mm, 30 at 8 mm, 20 chips at 11 cm for
      16 mm. The heap alone could never work — it is 6–11 cm of material on a plant-scale set, so
      from any story camera it is a smudge; the belt can be filmed from 1.5 m, which is the only
      distance where the sizes are distinguishable. Heap kept as the secondary beat.*
      *Two deviations, both deliberate: (1) not `particleSizeMm / 2` — that is 8× linear at 16 mm,
      512× the volume per piece, and buries the conveyor; the authored pile is treated as the 8 mm
      midpoint with count moving against size, so volume grows ~1.4×. (2) the pile is built even
      with no order, because all 20 authored granules float 3–35 cm above the apron and were never
      ground-seated (their renderers had been disabled since the scene was built); "change nothing"
      taken literally would show hovering cubes in free play.*
      *Three pre-existing bugs fixed on the way: all 20 `S2_OutputPile_*` renderers were disabled;
      `S2_04_OutputGranules` pointed at empty sky for its full 17 s; and the crane blade was 3.20 m
      against a 2.22 m hopper mouth, released at y 5.00 when the funnel bottom is 5.53 — blade now
      2.00 m, landing at y 6.20.)*
- [ ] Stage 3 kiln visibly differs at 550 / 580 / 600 °C; rotation follows retention
- [ ] Stage 4 fibre and char streams visibly reflect the output split
- [ ] Stage 4 has clickable parts; Stage 3's are reachable while paused
- [ ] All three presets played end to end — each looks distinctly different
- [ ] Stage scenes still play standalone in the editor with no order set
- [ ] Unity console: zero errors
