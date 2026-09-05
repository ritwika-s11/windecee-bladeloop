# Claude Brief — Akshat

**Read `docs/BLADELOOP-PRODUCT-VISION.md` first.** This file is the build detail for your tasks.

> 🔴 **`docs/interface-contract.md` is binding.** `OrderContext`, `OrderSolver`, `TourRunner` and the
> `TourSplitWidth` constant are all implemented to it and merged. Anything you change in those
> signatures now breaks a live caller, so change the contract first and tell the group.

Branch off current `main`: `feature/order-spine`
Feature freeze **Wed 9 Sep** · sprint review **Fri 11 Sep**

> ## 🔄 Reassigned Wed 2 September — you now own the Custom Order screen
>
> **`OrderDashboardController.cs` and `OrderDashboard.unity` are yours**, handed over from Sharan,
> who moves to How It Works. He will not touch them again. Your job is to make that screen properly
> product-like — it works, but it is the screen a professor will actually *use*, and it should feel
> like software rather than a form.
>
> **Read it before you change it.** Two things in there are correct and must not regress:
>
> 1. **The feed slider is bounded by `OrderSolver.MaxFeed(particle)`.** This is the constraint that
>    stops a user finding a setting that beats all three presets on throughput *and* quality — the
>    exploit you found yourself. If that coupling breaks, the product's central claim breaks with it.
> 2. **Infeasible targets show `result.note` as a readable sentence**, not an error or a blank panel.
>
> **What's already done and shipped** (thank you — the dual screen landed clean):
> `TourRunner`, `OrderPanel`, the viewport split, `SplitVFov` with the 65° clamp, `BladeLoopTheme`.
>
> **Still open, in priority order:**
>
> 1. **Custom Order polish** — the new work. Ritwika will send you specifics; she is the product voice.
> 2. **Chapter navigation and *Skip to results*** — `JumpToChapter` is still a `TODO` in `TourRunner`.
>    Nobody is blocked on it, so it is cut-first if the week runs short.
> 3. **Explore mode** — ⚠️ **play it on current `main` before writing anything.** Your original
>    diagnosis was against `5bace06`, and Anirban's PR landed after that with `minPitch 5 / maxPitch
>    75` and `PauseFramePreserver` in all four stages. Both causes may already be fixed. If it still
>    sticks, send Anirban a repro — he wrote the current versions.
>
> **Do not touch:** the stage scenes or their timelines (Ritwika owns the whole tour from today),
> or `MainMenuController.cs`.
>
> One cheap follow-up when convenient: the palette now exists in three files —
> `BladeLoopTheme`, `MainMenuController` and `OrderDashboardController`. Pointing all three at
> `BladeLoopTheme` takes fifteen minutes and prevents drift the first time anyone tweaks the accent.

> ## ✅ Tasks 0, 1 and 2 are done — Ritwika wrote them on 31 Aug
>
> `OrderContext.cs`, `OrderSolver.cs` and `OrderSelfTest.cs` are on `main`. You had no time Monday
> and two people were blocked behind that one file, so she took it. Both are pure C# and were fully
> specified in `docs/interface-contract.md`, so it was implementing a spec, not designing one.
> Ownership moved to her in the Rule 2 table.
>
> **Your F1 constraint is in**, with k = 1106.1 as you corrected. `OrderSolver.MaxFeed(particleMm)`
> is public so Sharan binds his feed slider to it. Run **BladeLoop → Verify Order Model** to see
> everything checked against the canonical table — including a test that the exploit stays blocked.
>
> **Start at Task 3 (the dual screen).** That's the critical path now: Anirban's 14 canvases and
> Sharan's screens both assume it. Then Task 5, then the FOV fix, then chapter nav last.
>
> **`TourRunner.cs` is already on `main` as a stub**, and the home page calls it — so you're filling
> in behaviour, not designing an interface. `StartRun()` loads `FullPlantTour` today; `SplitVFov()`
> is done including the 65° clamp. The TODOs in the file mark exactly what's left: `Camera.rect`,
> the order panel, `SkipToResults()`, `JumpToChapter()`.
>
> Use **`OrderContext.TourSplitWidth`**, never a literal `0.72` — Anirban's overlay frames read the
> same constant.
>
> **Both compile clean and the self-test passes in the editor** — output recorded in
> `docs/interface-contract.md` §10, including the check that the 0.5 mm / 9,000 kg/h exploit you
> found is now capped at 4,967 kg/h.
>
> If anything in those two files is wrong or awkward for you, say so — you're the person most likely
> to spot a problem with them.

---

## Paste this to your Claude

> I'm working on BladeLoop, a Unity 6 (6000.4.7f1) URP WebGL project that visualises thermal
> co-processing of wind turbine blades. Read `docs/BLADELOOP-PRODUCT-VISION.md` in the repo root
> for product context, then `docs/handover-akshat.md` for my tasks, and
> `docs/plant-explorer-architecture.md` for how the existing code is structured.
>
> Project constraints:
> - **New Input System only** (`activeInputHandler: 1`). `OnMouseDown` and the legacy `Input`
>   class never fire. Use `Mouse.current` / `Keyboard.current`.
> - **Cinemachine 3.** A `CinemachineTrack` on a Timeline does nothing unless you call
>   `director.SetGenericBinding(track, brain)`.
> - `ProcessModel.cs` is read-only for me — Sharan owns it. Never change a formula there.
> - I must not open or modify any `.unity` scene file. Scenes have single owners and cannot be
>   git-merged.
> - Everything must still work when no order is active, exactly as today.
>
> Work task by task, in order. After each script change, check the Unity console for compile
> errors before continuing.

---

## ~~Task 1 — `OrderContext.cs`~~ ✅ DONE (Ritwika, 31 Aug)

*Kept below for reference — this is the spec she implemented to, and it's still the description of
what's on `main`. Skip to Task 3 for your actual work.*

`Assets/Scripts/OrderContext.cs`

A **static class**, not a MonoBehaviour — so it needs no scene object, no prefab, no inspector
wiring, survives every scene load, and works in WebGL. There is currently no state that survives a
scene load except `TourSceneSequencer` (which is `DontDestroyOnLoad`).

Required shape — **these names matter, three other people are coding against them**:

```csharp
public enum Grade { High, Mid, Low }

[System.Serializable]
public class Order
{
    public string customerName;     // "Nordkomposit GmbH"
    public string customerType;     // "Composite manufacturer"
    public Grade  targetGrade;
    public float  targetTonnes;     // tonnes of fibre requested
}

public static class OrderContext
{
    public static Order Active;                     // null = free play
    public static ProcessModel Model = new ProcessModel();
    public static bool HasOrder => Active != null;

    public static void SetOrder(Order o, ProcessModel m);
    public static void Clear();

    // Derived — used by the panel, the dashboard and the outcome report:
    public static float FibreKgH         { get; }   // Model.OutputSplit().GlassKgH
    public static float FeedTonnesNeeded { get; }   // targetTonnes / (GlassKgH / FeedKgH)
    public static float CampaignHours    { get; }   // targetTonnes * 1000 / GlassKgH
    public static Grade AchievedGrade    { get; }   // from purity + tensile
}
```

Grade thresholds as **public** named constants — `OrderContext` is the single source of truth for
these and nobody hardcodes them anywhere else. Updated 30 Aug from the CEE review:

```csharp
public const float HighPurity = 90f, HighTensile = 85f;
public const float MidPurity  = 78f, MidTensile  = 70f;
```

⚠️ These **must** stay below the metric ceilings in `ProcessModel` (93 % purity / 90 % tensile) or
the high-grade state becomes unreachable. See `docs/interface-contract.md` §2 for the full surface.

**Also put the three presets here** as static readonly data, so Ritwika's homepage and Sharan's
dashboard read the same definitions:

| Grade | Customer type | Order | Temp | Retention | Feed | Particle |
|---|---|---|---|---|---|---|
| High | Composite manufacturer | **4,800 t** | 600 °C | 35 min | 6,500 kg/h | 2 mm |
| Mid | Precast concrete producer | **4,100 t** | 580 °C | 35 min | 8,000 kg/h | 8 mm |
| Low | Cement works | **3,250 t** | 550 °C | 35 min | 8,800 kg/h | 16 mm |

Customer *names* are Ritwika's call — use placeholders.

⚠️ **The order tonnages are not arbitrary — do not round them.** They were chosen so all three
presets consume the same feedstock (~6,990 t ≈ 619 blades ≈ 206 turbines), which is what makes the
three runs comparable. Rounding them to 5,000 / 4,000 / 3,000 would break that.

Blade count uses **11.3 t per blade, 3 blades per turbine** — a sourced assumption from the CEE
team. Put it in `OrderContext` as a named constant next to the grade thresholds.

### Verify before pushing

Add a `[ContextMenu]` check that logs the three presets. They must produce:

| Preset | Efficiency | Purity | Tensile | Fibre out |
|---|---|---|---|---|
| High | 100 % | 93.0 % | 90.0 % | 4,482 kg/h |
| Mid | 76 % | 82.5 % | 76.5 % | 4,691 kg/h |
| Low | 50 % | 69.8 % | 58.3 % | 4,091 kg/h |

These are computed from the current `ProcessModel` and are correct. **Different numbers means
something is wrong — tell Ritwika before continuing.**

**Push and open a PR the moment this compiles and verifies. Do not bundle it with Task 2.**

---

## Task 2 — `OrderSolver.cs`

`Assets/Scripts/OrderSolver.cs`

Runs `ProcessModel` backwards: given a target grade, find settings that reach it.
`ProcessModel` is pure arithmetic with no Unity dependencies, so brute-force grid search runs in
well under a second.

Grid (~120k evaluations):

| Parameter | Range | Step |
|---|---|---|
| Temp | 400–700 °C | 10 °C |
| Retention | 30–45 min | 1 min |
| Feed | 4,000–9,000 kg/h | 250 kg/h |
| Particle | 1–20 mm | 1 mm |

```csharp
public static class OrderSolver
{
    public struct Result { public ProcessModel model; public bool feasible; public string note; }

    // Hit targetGrade with the HIGHEST fibre throughput.
    public static Result Solve(Grade targetGrade);

    // Hit targetGrade with the LOWEST temperature.
    public static Result SolveGentlest(Grade targetGrade);
}
```

### 🔴 The shredder capacity constraint — non-negotiable, this is your F1 fix

**Agreed 30 Aug.** Your unconstrained solver returns 600 °C / 35 min / 9,000 kg/h / 0.5 mm for
*every* target grade — 5,911 kg/h at 90.3 % purity, which qualifies as high grade and beats the
high preset by 32 %. Confirmed independently against the rescaled model. Without a fix, Custom Order
disproves our own pitch.

Apply your fix, with **one calibration correction**:

```csharp
// Finer shredding is slower shredding: particle size caps feed rate.
// k = 1106.1 (NOT 1100) — the exact fit through (2mm, 6500) and (16mm, 8800).
// At k=1100 the LOW preset (16mm, 8800) is infeasible by 13 kg/h and the solver
// would reject one of our own presets.
static float MaxFeed(float particleMm) =>
    Mathf.Clamp(6500f + 1106.1f * Mathf.Log(particleMm / 2f), 4000f, 9000f);
```

| Particle | Fmax | Preset | Feasible |
|---|---|---|---|
| 2 mm | 6,500 | 6,500 | ✓ exact |
| 8 mm | 8,033 | 8,000 | ✓ |
| 16 mm | 8,800 | 8,800 | ✓ exact |

Reject any grid point where `feed > MaxFeed(particle)`. The resulting frontier rises then falls,
peaking at **6 mm / 7,708 kg/h → 4,806 kg/h at 86.3 % purity** (mid grade), and 1 mm caps at
5,738 kg/h so the sub-2 mm exploit dies for free.

**Note:** the solver's best mid-grade answer (6 mm) will slightly beat our 8 mm mid preset
(4,806 vs 4,770). That's intended — the presets are illustrative customer orders, not solver output.

Tell Sharan: **the feed slider on Custom Order must be bounded by the particle-size slider.**

**Two things matter more than the search itself:**

1. **Maximise throughput within the grade — don't just return the optimum.** If the user asks for
   mid grade, the interesting answer is *the fastest way to make mid grade*, which is a higher
   feed rate than the design case. That's the trade-off the whole product turns on (§4 of the
   vision doc). Returning 600 °C / 2 mm for every request makes the feature pointless.
2. **Handle infeasibility honestly.** If nothing in the grid reaches the target, return
   `feasible = false` with a readable `note` — Sharan displays it. Never return a silently wrong
   answer.

Keep this file free of Unity dependencies so it stays fast and testable.

---

## Task 3 — The dual screen

The tour renders in the left ~72 % of the window; a persistent order panel occupies the right ~28 %.
See §5.2 of the vision doc for the layout.

**Approach — this is what keeps you out of everyone's scenes.** Do not edit stage scenes to add a
panel. Create a `DontDestroyOnLoad` object once when an order launches, and have it react to
`SceneManager.sceneLoaded`:

```csharp
void OnSceneLoaded(Scene s, LoadSceneMode m)
{
    var cam = Camera.main;
    if (cam != null && OrderContext.HasOrder)
        cam.rect = new Rect(0f, 0f, 0.72f, 1f);
}
```

One script, zero per-scene wiring, automatically applies to all four stages.

**Panel contents:** grade badge, customer type, the four parameter values, the five output bars
with percentages. **Nothing else** — keep it narrow and legible.

**Two things to watch:**

- **Reset the rect** to `new Rect(0,0,1,1)` when returning to the menu or when `HasOrder` is false,
  or free play renders cropped.
- `ExploreOrbitCamera` and `ExploreClickRaycaster` both use `Camera.main`.
  `Camera.ScreenPointToRay` does respect a viewport rect, so clicking should still work — but
  **test Explore mode inside the split viewport** and tell Anirban if it's off.

---

## Task 4 — Chapter navigation and *Skip to results*

`TourSceneSequencer.cs` already chains five scenes and advances when each `PlayableDirector`
finishes. Add:

- A four-chip chapter bar (`Farm · Shred · Kiln · Separate`) that jumps to a stage
- A **Skip to results** button that ends the tour and loads Sharan's Outcome Report scene

Jumping means loading the scene at that index and continuing the chain from there. The sequencer
already loads by name from `sceneSequence`, so this is mostly restructuring the coroutine to start
from an index rather than always from 0.

**Watch out:** the sequencer is `DontDestroyOnLoad` and carries the fade canvas with it. If you
restart the chain, don't spawn a second sequencer. Read `BackToMenuButton.cs` first — it already
has cleanup logic for exactly this.

**Coordinate with Ritwika**: she owns the subtitle system, which also uses Next/Back controls in
the same corner of the screen. Agree who renders what before you both build a button bar.

---

## Task 5 — Fix Explore mode (professor feedback, now assigned to you)

The outstanding note was: *"when stopping the tutorial in the rotary kiln, you can only rotate the
screen once, then it totally gets stuck. Also, it is not possible to click any part."*

**You own `PauseFramePreserver.cs`, `ExploreOrbitCamera.cs` and `ExploreClickRaycaster.cs`** — added
to the Rule 2 table for this.

> 🔴 **Play it on current `main` before you write a line.** Your diagnosis was verified against
> `5bace06`, but **Anirban's PR merged after that** and changed exactly these files. On `main` today:
>
> ```
> ExploreOrbitCamera.cs   minPitch = 5f, maxPitch = 75f   → 70° of travel, not 8.9°
>                         rotationDamping, zoomDamping, spinInertia all present
> PauseFramePreserver.cs  exists, and is in all four stage scenes
> ```
>
> `PauseFramePreserver` is the fix for the other half of your diagnosis — it seeds the orbit pivot
> along the camera's forward axis at true distance, so the frame doesn't jump on pause and the ray
> no longer lands on the floor. **Both root causes you identified appear to be addressed already.**
> If it still sticks, send Anirban the repro — he wrote the current versions. If it doesn't, that's a
> day back for the spec you owe him.

**The two scene-side changes are Anirban's**, under Rule 1. Confirmed on current `main`:

```
Stage3_StoryMode   22 ClickableParts, all inside the cutaway → 0 active while paused
Stage4_V2          ClickablePart 0   PartInfoPanel 0   ExploreClickRaycaster 0
```

Stage 4 — the money shot — has nothing clickable at all. And Anirban confirmed why Stage 3's 22 are
dead: **every one has an inactive ancestor.** Checking `m_IsActive` on the components' own
GameObjects reports 18 "active" and hides it — you have to walk the parent chain.

**Write Anirban a short spec by Wed 2 Sep** naming which objects need `ClickablePart`, what titles
and descriptions they carry, and where `PartInfoPanel` and `ExploreClickRaycaster` go. He applies it
in the scenes; his Task 5 is idle until it arrives. Don't open the scenes yourself.

Also verify Explore mode works **inside the 72 % split viewport** — `Camera.ScreenPointToRay` should
respect the viewport rect, but it needs testing rather than assuming.

---

## FOV compensation — do this before Anirban re-frames anything

Agreed from your F4. Unity holds vertical FOV fixed and derives horizontal from aspect, so a 72 %
viewport cuts 28 % of horizontal field from all ~40 shots, not just badly-framed ones:

```csharp
// Clamp at 65°: above that you get visible perspective stretch, and 30% more
// vertical view exposes the hill ring and ground plane that Anirban built to
// sit just outside frame. Stage 4's widest lens (66°) would otherwise land at 84°.
newVFov = Mathf.Min(
    2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f * Mathf.Deg2Rad) / 0.72f) * Mathf.Rad2Deg,
    65f);
```

Apply when the split is active, restore on exit. **Due Wed 2 Sep** — Anirban's Task 6 is idle without
it. The clamp bites on any shot originally above ~49°, which covers a fair number of Stage 3's 12 and
Stage 4's 14 lenses; those stay partially compensated and Anirban reviews them by hand.

---

## What you must not break

- Pause behaviour in `StoryModeController.TogglePause`: it sets `Time.timeScale = 0`, disables the
  `CinemachineBrain`, and pauses `AudioListener`. **All three are load-bearing** — stage animation
  runs on script clocks, not the timeline. Don't simplify it.
- `ProcessModel.cs` — read only.
- No `.unity` scene edits.
- Free play with no order must work exactly as today, full screen, no panel.

---

## Definition of done

- [x] ~~`OrderContext` + `OrderSolver`~~ — done by Ritwika 31 Aug, verified by the self-test
- [ ] Explore mode played on current `main` before any code written
- [x] ~~Solver returns a max-throughput answer per grade~~ — Ritwika, 31 Aug
- [x] **An order runs all four stages with the panel visible and correct throughout** — 3 Sep
- [x] **Camera rect resets cleanly on exit; free play unaffected** — 3 Sep
- [ ] Chapter skip and *Skip to results* both work
- [ ] Explore mode orbits freely (not stuck after one drag) and clicking works, in split viewport
- [ ] Spec for the Stage 3 / Stage 4 clickable-part changes handed to Anirban
- [x] **FOV compensation in before Anirban starts re-framing** — 3 Sep
- [x] Unity console: zero errors, and zero new warnings

---

## Task 3 — delivered 3 Sep

`OrderPanel.cs` (new) · `BladeLoopTheme.cs` (new) · `TourRunner.cs` (filled in).
**No `.unity` file touched**, so nothing collides with Anirban.

`OrderPanel` creates itself, marks itself `DontDestroyOnLoad` and rides
`SceneManager.sceneLoaded` through all five scenes. It sets `Camera.rect` from
`OrderContext.TourSplitWidth`, applies `TourRunner.SplitVFov`, and draws the panel into
the remaining 28 %.

**What the panel shows, per stage.** Settings appear only once a stage has decided
something — a temperature listed over a field of turbines is noise. Output appears only at
Separation, because before the plant has run there is no result to report.

| Stage | Settings shown | Lit | Output |
|---|---|---|---|
| Wind farm | — | — | no |
| In transit | — | — | no |
| Shredding | Particle | Particle | no |
| Rotary kiln | Particle, Temperature, Retention | Temperature, Retention | no |
| Separation | all four | Feed rate | **yes**, plus purity/tensile |

Panel copy comes from `ProcessModel` (`ParticleInfo()`, `TempInfo()`, `RetentionInfo()`) and
`OrderContext.EndUseFor()` — never written in `OrderPanel`. When the narration is rewritten
per grade, the panel follows for free. **Confirmed with Ritwika 3 Sep: the line changes once
per stage, not per narration beat** — more movement than that competes with the voiceover.

### Three things worth knowing

1. **FOV is read from the Brain, never from `Camera.fieldOfView`.** Reading the camera
   compounds — frame two compensates frame one's compensated value and the lens opens until
   it hits the 65° clamp. It reads `brain.ActiveVirtualCamera.State.Lens.FieldOfView`, the
   authored blended value, so compensation is applied exactly once. While paused the Brain is
   *disabled* (`StoryModeController` does this for `ExploreOrbitCamera`), so there is no
   authored lens to read and the FOV is left alone.

2. **The panel self-heals on exit.** `StoryModeController.BackToMenu()` — the Escape key —
   loads `MainMenu` directly, without restoring `Camera.rect` or knowing the panel exists.
   Rather than patch each exit and miss one, the panel watches what scene loaded: anything
   outside the tour chain restores the camera and destroys it.

3. **`Clear()` ordering.** `ReturnToMenu()` calls it — that is what feeds the home page's
   last-run line. `SkipToResults()` deliberately does **not**: `OutcomeReportController.Start()`
   reads `Active` and `Model`, and clearing first renders an empty report that looks like
   Sharan's bug.

### Open, and needs a decision

**`BladeLoopTheme.cs` duplicates the home page palette.** `MainMenuController`'s colours are
`private static`, and `handover-sharan.md` tells Sharan to hand-copy them — the order panel
is the third surface needing them. The new file is the single source they could all read, but
until Ritwika and Sharan adopt it there are two copies, which is worse than one. **Either
adopt it or say so and I will inline the values and delete it.**

---

## Two things that changed after you wrote your feedback

1. **The quality metrics were re-anchored** (30 Aug, from the CEE sourcing). Ceilings are now
   **93 % purity / 90 % tensile**, down from 99 / 100 — the old model claimed thermal recovery did
   zero damage to the fibre, which the literature contradicts. Curve *shapes* are unchanged; only
   the anchor moved. Your §4 recomputation was correct against the old values.
2. **Order sizes changed** to 4,800 / 4,100 / 3,250 t, chosen so all three presets consume the same
   ~6,990 t of feedstock (619 blades, 206 turbines). Don't round them — see Task 1.

Your F1, F2, F4 and F5 are all accepted and are in the docs. F3 is confirmed: runtime-built C#,
no prefab. F6 was already in Anirban's brief (Task 3, Change 1) — you had an older copy.
