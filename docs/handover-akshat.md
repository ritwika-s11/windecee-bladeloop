# Claude Brief — Akshat

**Read `docs/BLADELOOP-PRODUCT-VISION.md` first.** This file is the build detail for your tasks.

Branch off current `main`: `feature/order-spine`
Task 1 due **Tue 2 Sep** · everything else by **Tue 9 Sep** (feature freeze)

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

## Task 1 — `OrderContext.cs` 🔴 BLOCKING, due Tue 2 Sep

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

Grade thresholds as named constants, so there is exactly one place to change them when the CEE
team returns sourced numbers:

```csharp
const float HighPurity = 95f, HighTensile = 95f;
const float MidPurity  = 85f, MidTensile  = 80f;
```

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
to the Rule 2 table for this. You had this working on `feature/prof-feedback-akshat` @ `d412afc`
(root cause: the pause pivot ray hit the floor 26.41 m away instead of the kiln, and pitch limits
widened by only ±5°, leaving 8.9° of travel — that clamp is what reads as "totally gets stuck").
Redo it from current `main`.

**The two scene-side changes are Anirban's**, under Rule 1. Confirmed on current `main`:

```
Stage3_StoryMode   22 ClickableParts, all inside the cutaway → 0 active while paused
Stage4_V2          ClickablePart 0   PartInfoPanel 0   ExploreClickRaycaster 0
```

Stage 4 — the money shot — has nothing clickable at all. **Write Anirban a short spec** naming which
objects need `ClickablePart`, what titles and descriptions they carry, and where `PartInfoPanel` and
`ExploreClickRaycaster` go. He applies it in the scenes. Don't open the scenes yourself.

Also verify Explore mode works **inside the 72 % split viewport** — `Camera.ScreenPointToRay` should
respect the viewport rect, but it needs testing rather than assuming.

---

## FOV compensation — do this before Anirban re-frames anything

Agreed from your F4. Unity holds vertical FOV fixed and derives horizontal from aspect, so a 72 %
viewport cuts 28 % of horizontal field from all ~40 shots, not just badly-framed ones:

```csharp
newVFov = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f * Mathf.Deg2Rad) / 0.72f) * Mathf.Rad2Deg;
```

Apply when the split is active, restore on exit. Anirban has been told to wait for this and only fix
the handful that still look wrong afterwards.

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

- [ ] `OrderContext` merged to `main` by Tue 2 Sep, verified against the preset table
- [ ] Solver returns a max-throughput answer per grade and reports infeasibility honestly
- [ ] An order runs all four stages with the panel visible and correct throughout
- [ ] Camera rect resets cleanly on exit; free play unaffected
- [ ] Chapter skip and *Skip to results* both work
- [ ] Explore mode orbits freely (not stuck after one drag) and clicking works, in split viewport
- [ ] Spec for the Stage 3 / Stage 4 clickable-part changes handed to Anirban
- [ ] FOV compensation in before Anirban starts re-framing
- [ ] Unity console: zero errors

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
