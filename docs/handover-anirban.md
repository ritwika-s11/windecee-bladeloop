# Claude Brief — Anirban

**Read `docs/BLADELOOP-PRODUCT-VISION.md` first.** This file is the build detail for your tasks.

Branch off current `main`: `feature/parameter-visuals`
Visuals done by **Sun 7 Sep** · feature freeze **Tue 9 Sep**

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

## You are blocked until Tue 2 Sep

Akshat's `OrderContext.cs` — where you read parameters from — lands on `main` on **Tuesday 2 Sep**.
Until then, do **Task 1**, which has no dependency.

---

## Task 1 — Fix the overlays for the split screen *(start here, no dependency)*

Akshat is splitting the window: 3D tour in the **left 72 %**, order panel in the **right 28 %**.

Every UI overlay in the stage scenes is currently anchored to the **full screen**, so they will sit
on top of the panel.

| Element | Script | Currently |
|---|---|---|
| Subtitle box + Next/Back | `SubtitleTrack.cs` (Ritwika's) | bottom centre, full screen |
| Back to menu button | `BackToMenuButton.cs` | corner, full screen |
| Explore hint chip | `ExploreHintChip.cs` | full screen |
| Pause hint | `StoryModeController.pauseHintUI` | full screen |
| Part info panel (Stage 3) | `PartInfoPanel.cs` | full screen |

**How:** put each stage's overlay canvas under a parent `RectTransform` anchored `(0,0)` to
`(0.72, 1)`. Every child then stays inside the tour viewport automatically — no repositioning each
element by hand.

**Must not break the non-split case.** With no order the tour is full screen and overlays should
fill it as today. Make the parent's right anchor switchable off `OrderContext.HasOrder` in `Start()`.

⚠️ The Game view runs at roughly **2:1 aspect, not 16:9**, so the canvas is about **1016** reference
units tall, not 1080. Elements near y = ±540 will clip. This has bitten us before.

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

The *pause/orbit* half is Akshat's (he owns `PauseFramePreserver.cs` and `ExploreOrbitCamera.cs`).
**The scene half is yours**, because it's scene edits. Verified on current `main`:

```
Stage3_StoryMode   22 ClickableParts — all inside the cutaway, so 0 are active while paused
Stage4_V2          ClickablePart 0   PartInfoPanel 0   ExploreClickRaycaster 0
```

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
72 % cuts 28 % of horizontal field from **every** shot — roughly 40 of them — not just badly-framed
ones. Akshat is compensating in code with one line
(`newVFov = 2·atan(tan(vFov/2)/0.72)`), which restores the original horizontal framing for all of
them at once.

Once that lands, play each stage in split view and fix only the handful that still look wrong.

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

- [ ] Overlays stay inside the tour viewport when split, fill the screen when not
- [ ] Stage 2 granules visibly differ at 2 / 8 / 16 mm
- [ ] Stage 3 kiln visibly differs at 550 / 580 / 600 °C; rotation follows retention
- [ ] Stage 4 fibre and char streams visibly reflect the output split
- [ ] Stage 4 has clickable parts; Stage 3's are reachable while paused
- [ ] All three presets played end to end — each looks distinctly different
- [ ] Stage scenes still play standalone in the editor with no order set
- [ ] Unity console: zero errors
