# Claude Brief — Sharan

**Read `docs/BLADELOOP-PRODUCT-VISION.md` first.** This file is the build detail for your tasks.

> 🔴 **`docs/interface-contract.md` answers S1, S2, S3, S4 and S5.** It fixes the exact signatures
> for `OrderContext`, `OrderSolver` and `TourRunner`, states who owns which number, gives the
> canonical verification table, and defines the tour → Outcome Report handoff.
>
> **You do not have to wait for Tuesday.** Build Task 3 and Task 4 against those signatures now.
> The code won't compile until Akshat merges — that's expected. Get the layout and logic done, then
> pull on Tuesday and fix up. That should take minutes, not a rebuild.

Branch off current `main`: `feature/order-dashboard`
Start **today, Mon 31 Aug** · Custom Order screen **Fri 4 Sep** · feature freeze **Wed 9 Sep**

---

## The goal

**Build the screens where the user works with numbers instead of watching the plant.**

The Plant Explorer you built is not going away — it is being **promoted**. Its sliders, tanks and
quality metrics become the Custom Order screen, reached through the order flow instead of sitting
as a separate tab. Same components, better place in the product.

All three screens use the approach you already use: **UI built entirely in C# at runtime, no
prefabs, no Scene view layout.**

---

## Paste this to your Claude

> I'm working on BladeLoop, a Unity 6 (6000.4.7f1) URP WebGL project. Read
> `docs/BLADELOOP-PRODUCT-VISION.md` in the repo root for product context, then
> `docs/handover-sharan.md` for my tasks, then `docs/plant-explorer-architecture.md` for how the
> existing dashboard code is structured — the new screens follow the same pattern.
>
> Key facts:
> - **All dashboard UI is built in C# at runtime by the controller script.** There are no UI
>   prefabs and nothing is laid out in the Scene view. To change wording or layout you edit the C#
>   file, not the scene.
> - New Input System only. The legacy `Input` class never fires.
> - UI positions live in `RectTransform.anchoredPosition`, not `transform.position`.
> - `ProcessModel.cs` is mine but **read-only for this sprint** except for adding new methods.
>   Never change an existing formula — three other people depend on the current numbers.
> - I must not edit `OrderContext.cs`, `OrderSolver.cs`, `TourSceneSequencer.cs`,
>   `StoryModeController.cs`, `SubtitleTrack.cs`, or any stage scene.
>
> Work one task at a time and check the Unity console for compile errors after each change.

---

## Task 1 — Remove the miniature 3D kiln *(start here, no dependency)*

The small reactive kiln next to the dashboard is not part of the product any more.

In `PlantExplorer.unity`, delete the `ReactiveKiln` and `KilnBackdrop` GameObjects, then reflow the
dashboard to use the space they free up — the tanks and quality metrics can breathe.

Leave `KilnVisualizer.cs` on disk for now (deleting it mid-sprint causes needless compile churn);
just remove any references to it from the controller. It can be cleaned up after the freeze.

---

## Task 2 — Content for the "How it works" page *(also no dependency)*

Write the copy first as `docs/how-it-works-content.md`. You'll build the screen in Task 5.

Four sections:

**1. What this models.** Thermal co-processing of decommissioned wind turbine blades. Four inputs,
five output streams, a closed mass balance. Two or three plain-language paragraphs — a professor
should read it and know exactly what we claim to simulate.

**2. The four inputs and why they matter.** You already have this — it's what your ⓘ popups say.
Reuse `TempInfo()`, `RetentionInfo()`, `FeedInfo()`, `ParticleInfo()` from `ProcessModel.cs`.

**3. Where the numbers come from.** State plainly:

> Baseline output proportions (fibre 70 / oil 16 / syngas 8 / char 6 of the recovered stream) come
> from the CEE reference model. Deviation weights, loss behaviour and quality curves are our own
> model, calibrated to that baseline.

**4. Our assumptions, stated honestly.** The most important section in the app for our credibility.

| Tier | Purity | Tensile retention | End use |
|---|---|---|---|
| High | ≥ 90 % | ≥ 85 % | Composite manufacturing |
| Mid | ≥ 78 % | ≥ 70 % | Precast concrete, casting |
| Low | below | below | Cement kiln co-processing |

The tiers are **calibrated to published pyrolysis results** — sources and reasoning are in
`docs/CEE-deliverable.md` and `docs/grade-threshold-reasoning.md` (Anjani & Hari, 30 Aug). Cite
them on this page. Two facts worth naming, because they turn our tiers into real markets:

- **Regen Fiber** already sells shredded blade fibre into precast concrete — that's the mid tier
- **Cement co-processing** is the most commercially mature end-of-life route at scale — the low tier

> ⚠️ Two honesty requirements on this page, both non-negotiable:
>
> 1. **The threshold numbers are project assumptions.** No published grading standard for recovered
>    composite glass fibre exists (PAS 101 covers container cullet glass only). Say so. The words
>    "industry standard" must not appear anywhere.
> 2. **Define "purity" explicitly**, because the literature doesn't use it — it reports tensile and
>    modulus retention. Our definition: *the mass fraction of recovered material that is fibre,
>    rather than adhered char and resin residue.* State it in those words.

---

## Task 3 — Custom Order screen 🎯 main task

*Build it today against `docs/interface-contract.md`. The `OrderContext` skeleton lands **today**
so it compiles; `OrderSolver` follows **Tue 1 Sep**.*

New scene `Assets/Scenes/OrderDashboard.unity` + `Assets/Scripts/OrderDashboardController.cs`,
built the same way as Plant Explorer: an empty GameObject holding a controller that creates the
entire UI at runtime. Reuse your palette, fonts, spacing and light theme — it should feel like the
same product.

Layout is sketched in **§5.3 of the vision doc**. In order down the screen:

**The order form**

| Field | Control | Range |
|---|---|---|
| Customer name | text input | free text, default "Custom order" |
| Target grade | three-way selector | High / Mid / Low |
| Quantity | slider or number input | 1,000 – 10,000 tonnes of fibre, default 4,000 |

*(The three presets sit at 4,800 / 4,100 / 3,250 t, so this range brackets them with room either
side. A wider range makes the slider unusable.)*

**A big `[ SOLVE ]` button.** On press:

```csharp
var result = OrderSolver.Solve(selectedGrade);
if (!result.feasible) { /* show result.note, stop here */ }
OrderContext.SetOrder(newOrder, result.model);
```

**The solved plan** — reusing your existing components:

- **The four settings it found**, presented as *the answer*. This is the payoff: the user asked for
  an outcome and got a recipe.
- **The five output streams** — your tank bars, unchanged.
- **Purity and tensile**, with the achieved grade badge.
- **The campaign figures** from `OrderContext`: feedstock tonnes needed, running hours, days at 24/7.

**Two buttons:** `[ Watch this run → ]` (loads the tour — ask Akshat for the exact call, he owns
the sequencer) and `[ ← Menu ]`.

### Two things that make this good rather than adequate

1. **Let the user adjust after solving.** Show the four settings as *editable* sliders so they can
   nudge one and watch the grade badge move. This is your existing Plant Explorer live-update code
   almost unchanged — it turns a one-shot calculator into something you can play with.

   🔴 **One new rule on those sliders: the feed-rate slider must be bounded by the particle-size
   slider.** Finer shredding is slower shredding, so maximum feed depends on particle size:

   ```
   Fmax(P) = 6500 + 1106.1 · ln(P / 2)     clamped to [4,000 … 9,000] kg/h
   2 mm → 6,500      8 mm → 8,033      16 mm → 8,800
   ```

   Akshat exposes this from `OrderSolver` — **call his method, don't reimplement the formula.** When
   the user drags particle size down, the feed slider's maximum drops with it and the current value
   clamps. This is not cosmetic: without it, a user can set 0.5 mm at 9,000 kg/h and get better
   throughput *and* better quality than every preset, which disproves the whole product. See §4 of
   the vision doc.
2. **Handle infeasibility in plain language**, not as an error:
   *"No settings in the operating envelope reach high grade at 9,000 kg/h. Try a lower feed rate,
   or accept mid grade."*

### Verify against these

Computed from the current `ProcessModel` and correct. **Different numbers means something is wrong
— tell Ritwika before continuing.**

| Preset | Settings | Efficiency | Purity | Tensile | Fibre out |
|---|---|---|---|---|---|
| High | 600 °C / 35 min / 6,500 kg/h / 2 mm | 100 % | 93.0 % | 90.0 % | 4,482 kg/h |
| Mid | 580 °C / 35 min / 8,000 kg/h / 8 mm | 76 % | 82.5 % | 76.5 % | 4,691 kg/h |
| Low | 550 °C / 35 min / 8,800 kg/h / 16 mm | 50 % | 69.8 % | 58.3 % | 4,091 kg/h |

---

## Task 4 — Outcome Report screen

Shown when a tour finishes, or when the user hits *Skip to results*.
`Assets/Scenes/OutcomeReport.unity` + `Assets/Scripts/OutcomeReportController.cs`.
Layout sketched in **§5.4** of the vision doc.

It answers one question: **did we fill the order, and what did it cost in time and material?**

1. **The verdict**, large and unambiguous: *"Order filled — Mid grade"* or *"Target was High grade;
   this run achieved Mid grade."*
   **Missing the target is never a failure — it's a different customer.** If the run came in below
   target, name who *would* buy it. The words "fail" and "error" must not appear on this screen.
2. **The settings used** — four values, compact.
3. **What came out** — five tank bars, purity, tensile, grade badge.
4. **The campaign** — feedstock in, hours, days at 24/7, from `OrderContext`.
5. **The trade-off line.** This is what gets remarked on in the review. For a mid-grade run:

   > This run produces **4,691 kg/h** of fibre — more than the high-grade setting's 4,482 kg/h —
   > but at 88 % purity instead of 99 %. Higher throughput, lower value per tonne.

   **Compute it live** by comparing against the high-grade preset. Don't hardcode the sentence.
6. **Buttons:** `[ Try different settings ]` → Custom Order, `[ ← Menu ]`.

---

## Task 5 — How it works screen

Turn your Task 2 markdown into `Assets/Scenes/HowItWorks.unity` +
`Assets/Scripts/HowItWorksController.cs`. Plain, readable, scrollable, same palette. One
`[ ← Menu ]` button.

Do this **last** — it's the lowest-risk item and the easiest to finish quickly if time gets tight.

---

## If you need something new from ProcessModel

You own the file, but for this sprint: **add methods, never modify existing ones.** There is
already a clearly-marked additive section at the bottom — put new work there with a comment saying
what it's for.

If you think an existing formula is wrong, **message Ritwika — don't fix it yourself.** Anirban's
visuals and Akshat's solver are both calibrated against the current numbers; a silent change
desyncs all three.

---

## What you must not break

- The Plant Explorer screen keeps working while you build the new one. Build alongside, then
  switch over — don't refactor it out from under yourself mid-sprint.
- No edits to `OrderContext.cs`, `OrderSolver.cs`, `TourSceneSequencer.cs`,
  `StoryModeController.cs`, `SubtitleTrack.cs`, or any stage scene.
- No changes to existing formulas in `ProcessModel.cs`.
- Scene files can't be git-merged — only ever open `PlantExplorer.unity` and scenes you create.

---

## Definition of done

- [ ] Miniature 3D kiln removed, dashboard reflowed
- [ ] `docs/how-it-works-content.md` written, assumptions labelled as assumptions
- [ ] Custom Order: form → solve → settings + outputs + campaign figures → Watch this run
- [ ] Verified against the three preset number sets above
- [ ] Infeasible targets produce a readable explanation, not an error
- [ ] Outcome Report: verdict, settings, outputs, campaign, live trade-off line
- [ ] How it works screen readable and scrollable
- [ ] Unity console: zero errors
