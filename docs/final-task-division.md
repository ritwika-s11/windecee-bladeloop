# Final task division — to submission, 25 September

**Written:** 16 September 2026 · **Submission:** 25 September 2026 · **Nine days**

Last sprint is over. The professor asked for two things: every remaining bug fixed, and an
answer to *"why would anyone buy recycled glass fibre instead of virgin?"*. Both are covered
below, plus the report and submission work.

Everything here has been checked against the code on `main` as of today. Where a previous
diagnosis turned out to be wrong, this file says so — please read your section rather than
working from what was said in the review.

---

## Who has what

| # | Task | Owner | Notes |
|---|---|---|---|
| 1 | Mid/Low solver collapse | **Akshat** | Measure first. Touches `OrderSolver.cs` |
| 2 | Charts in the outcome report | **Akshat** | `OutcomeReport.cs`, standalone HTML |
| 3 | Space / Explore mode — Transport | **Sharan** | Scene has none of the components |
| 4 | Space / Explore mode — Stage 3 entry | **Sharan** | Not "unset" — see the handover below |
| 5 | World-space label typography | **Anirban** | His item from the testing round |
| 6 | Windows executable — build and test | **Anirban** | Only Windows machine on the team |
| 7 | Tensile retention vs literature | **Hari** | 90% claimed vs 30–45% published |
| 8 | Flowcharts for the report | **Hari** | Ritwika needs these to write |
| 9 | Project report | **Ritwika** | |
| 10 | Final presentation | **Ritwika** | |
| 11 | Submission, 25 Sep | **Ritwika** | |

---

## 1 · Mid/Low solver collapse — Akshat

**Found by Hari.** In Custom Order, solving for **Precast concrete (Mid)** and for
**Cement works (Low)** with the same material gives an identical plan: 600 °C · 35 min ·
7,715 kg/h · 6 mm, 62.3% fibre, 86.2% purity, same grade badge, same nudge copy.

### The diagnosis in Hari's bug report is wrong — do not fix what it says

His report proposes Cause A: "buyer isn't passed into the solver in the *I have material*
flow." That is not what happens. The code path is:

```csharp
// OrderDashboardController.ComputeFrontier()
frontier = mode == Mode.Constraint
         ? OrderSolver.FrontierWhere(m => true, shredMm)   // grade NOT used
         : OrderSolver.SolveFrontier(grade);               // grade IS used
```

Modes are `Buyer`, `Supply`, `Constraint`. "I have material" (600 blades) is **Supply**,
which takes the `SolveFrontier(grade)` branch — so the buyer *does* reach the solver.

**The real cause is that Mid and Low solve to the same answer.** Solving each preset's
target grade gives:

```
HIGH   66.1% fibre   90.1 purity
MID    62.3% fibre   86.2 purity
LOW    62.3% fibre   86.2 purity   ← identical to MID
```

62.3 / 86.2 are exactly the numbers in Hari's report. He compared Mid against Low and found
the collision without recognising it as one. Comparing either against High would have shown
a difference and nothing would have been filed.

### What to do

1. **Measure before changing anything.** The figures above are from `OrderSolver.Solve()`.
   Custom Order uses `SolveFrontier()`. Confirm the frontier collapses the same way before
   touching the objective function — it may not.
2. If it does collapse, the question is *why relaxing the quality bar buys no extra
   throughput*. Expect the cap to be `MaxFeed(particleMm)` or the search grid resolution,
   not the objective.
3. Behaviour that would be correct: a cement works has almost no quality bar, so the solver
   should be free to run **faster and coarser** and get more tonnes through the same plant.
   Right now it does not.

`OrderSolver.cs` and `OrderContext.cs` are Ritwika's files — agree the change with her
before pushing. (Hari's doc says these are yours; that ownership table is out of date.)

---

## 2 · Charts in the outcome report — Akshat

`Assets/Scripts/OutcomeReport.cs` builds the downloadable run report as **one
self-contained HTML file** — no external CSS, no images, no web fonts, because it has to
open from a USB stick on a machine with no network.

Constraints that must survive:

- **No external chart library.** Inline SVG or CSS bars only. The existing output bars are
  already CSS `div`s with a width percentage — the same approach extends to anything else.
- **The palette is mirrored as hex string constants** at the top of the file
  (`CBone`, `COxide`, `StreamCols[]`, …) because the file cannot reference `BladeLoopTheme`.
  This is the one place in the project that does not follow the theme automatically. If you
  add colours, add them there and note them.
- **The `@media print` block must keep working** — the report is dark on screen and flips
  to paper when printed.

Worth charting, in rough order of value: the four output streams against the design case
(the report currently shows actual only), and settings-vs-design as a deviation bar per
parameter. Both sets of numbers are already computed — `ProcessModel.OutputSplit()` and
`OrderContext.Reference`.

---

## 3 & 4 · Space / Explore mode — Sharan

Your Claude could not resolve this last time. The reason, most likely, is that the
diagnosis it was given — *"Stage 3's orbit entry point is unset"* — **is false**. Both
fields are wired. Here is what is actually true, read off the scene files today.

### Component inventory across the five tour scenes

| scene | StoryModeController | ExploreOrbitCamera | ExploreClickRaycaster | ExploreHintChip | PauseFramePreserver |
|---|---|---|---|---|---|
| Stage1_StoryMode | yes | yes | yes | yes | yes |
| **Transport_StoryMode** | **no** | **no** | **no** | **no** | **no** |
| Stage2_StoryMode | yes | yes | **no** | yes | yes |
| Stage3_StoryMode | yes | yes | yes | yes | yes |
| Stage4_V2 | yes | yes | **no** | yes | yes |

### Task 3 — Transport does nothing

Transport has **none** of the five components. Space does nothing because there is no
`StoryModeController` to toggle, and there is no orbit rig to hand the camera to.

Fix: add `StoryModeController`, `ExploreOrbitCamera` (plus an empty transform as its
target), `ExploreHintChip` and `PauseFramePreserver`, copying the values from
`Stage2_StoryMode` — that scene is the closest match in scale. Transport has no
`ClickablePart`s, so `ExploreClickRaycaster` is not needed; `OrderPanel` already checks for
working click targets and drops the "click any part" sentence by itself.

### Task 4 — Stage 3 jumps to a bad angle

`ExploreOrbitCamera` in Stage 3 is fully wired:

```
controller : StoryController          (set)
target     : FreeOrbit_Target  at (-5.00, 3.66, 0.00)
startDistance 13 · minDistance 1.5 · maxDistance 26
```

Stage 4, which behaves acceptably, is the same design with `V2_OrbitTarget` at
(-9.00, 3.60, 0.00) and `startDistance 17`.

The jump comes from `InitFromCamera()`:

```csharp
Vector3 offset = cam.position - target.position;
distance = Mathf.Clamp(Mathf.Max(offset.magnitude, startDistance), minDistance, maxDistance);
...
cam.transform.LookAt(target.position);
```

Two separate causes, and both need fixing:

1. **`Max(offset.magnitude, startDistance)` pulls the camera outward.** Pause on a close
   shot 5 m from the target and you are yanked to 13 m. The inherited yaw and pitch are
   correct — only the distance is overridden. Dropping the `Max` so the entry distance is
   the camera's real distance removes the zoom-out entirely.
2. **The pivot is a single fixed point for the whole stage.** Every shot re-aims at
   `FreeOrbit_Target`, so pausing on any shot not framed near (-5, 3.66, 0) swings the
   camera across the scene. That is the "arbitrary position with a bad angle."

The better fix for 2 is to pivot on **what the current shot was actually looking at**: read
`CinemachineBrain.ActiveVirtualCamera.LookAt` when the pause begins and use that transform
as the pivot, falling back to `target` when it is null. That is about ten lines in
`InitFromCamera`, it needs no scene edits, and it makes every stage behave the same way
without hand-tuning a target per scene.

Stage 2 and Stage 4 have no `ExploreClickRaycaster`, so "click a part" does nothing there.
That is consistent today because `OrderPanel.SceneHasWorkingClickTargets()` hides the
sentence — but it is worth knowing before someone reports it as a sixth bug.

---

## 5 · World-space label typography — Anirban

Your own item from the testing round. It overlaps the Stage 4 polishing Ritwika did, so
check with her before starting so you are not both in the same labels.

## 6 · Windows executable — Anirban

You have the only Windows machine. Build the player and run a full pass:

- All five stages, both from the worked examples and from Custom Order.
- The run report at the end, and the **Save report** button. On Windows it writes an
  `.html` to the Desktop and opens it in the browser — that path has been tested in the
  editor but not in a built player.
- Both the tour ending and the **RUN REPORT** shortcut on the Custom Order screen.

Report the Unity version you built with and anything that differs from the Mac editor.

---

## 7 · Tensile retention vs literature — Hari

This is the question the professor's "why buy recycled fibre?" leads to, and we should have
an answer before he asks.

`ProcessModel` reports **tensile retention of 90% at high grade** (600 °C, 35 min), 82.5%
at mid, 69.8% at low. The published figures for glass fibre recovered by pyrolysis at
450–600 °C are a **55–70% strength loss** — i.e. roughly **30–45% retention**, not 90%.

We are not assuming the model is wrong; you own the equations and there may be a good
reason. Possibilities worth stating explicitly in the report:

- The model may describe fibre that is **remelted into new fibre**, which does restore
  properties, rather than fibre reused directly. That is a real industrial route and would
  justify the number — but it changes what "reclaimed glass fibre" means on our screens.
- Or the retention figure may be relative to something other than virgin strength.

What we need from you: **one paragraph stating which basis the number is on, with a
citation.** If it should change, tell Ritwika early — it ripples through the grade
thresholds, the three presets and the Stage 4 visuals, and that is not a change to make in
the last few days.

Reference points:

- Mechanical properties of thermally-treated and recycled glass fibres — <https://www.sciencedirect.com/science/article/abs/pii/S1359836811000217>
- Mechanism controlling glass fibre strength loss during thermal recycling — <https://www.sciencedirect.com/science/article/abs/pii/S1359835X15002055>
- Glass fibres produced with recovered glass fibres from blades (Beauson, 2025) — <https://4spepublications.onlinelibrary.wiley.com/doi/10.1002/pc.70006>

### The economics answer, for the report

The professor asked why anyone buys recycled fibre rather than virgin. The honest answer is
that **on price and performance alone, mostly they would not** — and that is the interesting
part, not a weakness:

- Virgin E-glass is about **$1.30/kg**. A 5,000 t/yr pyrolysis plant needs to sell recovered
  fibre at roughly **80% of virgin** just to break even.
- The recovered fibre is also weaker, per the strength figures above.
- **The business case is on the disposal side, not the sales side.** Germany banned
  landfilling composites in **2024**; the European wind industry's self-imposed landfill ban
  took effect **1 January 2026**. Disposing of a non-carbon blade costs **€200–1,400 per
  tonne**.
- So the plant's primary revenue is the **gate fee** for taking the blade. Fibre, oil, gas
  and char are secondary recovery that offsets processing cost.

This is what our three buyers already encode: cement works takes almost anything, precast
takes filler, composite makers need the good material and rarely get it. It is a **disposal
hierarchy**, not a sales funnel. The report should say so plainly.

Sources: techno-economic assessment of a 7,000 t/yr blade pyrolysis plant —
<https://www.sciencedirect.com/science/article/abs/pii/S2589014X26000976> · environmental
and economic assessment of blade recycling approaches —
<https://pmc.ncbi.nlm.nih.gov/articles/PMC11770760/> · WindEurope on blade disposal —
<https://windeurope.org/news/where-do-wind-turbine-blades-go-when-they-are-decommissioned/>

## 8 · Flowcharts for the report — Hari

Ritwika needs these to write. Agree the list with her first so you are not drawing diagrams
the report does not use.

---

## One clarification that came up — retention time vs campaign length

Hari asked why the kiln appears to produce large tonnages in 35 minutes while an order takes
44 days. The two numbers measure different things and both are correct.

**35 minutes is residence time, not a batch cycle.** It is how long a given particle stays
inside the drum. The kiln is continuous — material enters and leaves at the same time.

```
holdup in the drum = 6,500 kg/h × 0.583 h = 3,792 kg
so in any 35 minutes the kiln passes 3.79 t of feed → 2.61 t of fibre
```

The campaign follows from the hourly rate, not from the residence time:

| preset | feed | fibre out | order | campaign |
|---|---|---|---|---|
| HIGH | 6,500 kg/h | 4,482 kg/h | 4,800 t | 1,071 h = **44.6 days** |
| MID | 8,000 kg/h | 4,691 kg/h | 4,100 t | 874 h = **36.4 days** |
| LOW | 8,800 kg/h | 4,091 kg/h | 3,250 t | 794 h = **33.1 days** |

Note that **mid grade out-produces high grade in fibre per hour** (4,691 vs 4,482) despite a
lower fibre percentage, because it runs 8,000 kg/h instead of 6,500. That trade-off is the
thing the app exists to show.

**One caveat to state in the report:** `CampaignDays = CampaignHours / 24` assumes
continuous running with no downtime. At a realistic 85% availability the high-grade order is
**52.5 days**, not 44.6. On a single 8-hour shift it is 134 days.

---

## Sequencing

Tasks 1, 3, 4, 5 are code and should land first — Ritwika needs a stable build to record the
final demo and to write against. Task 6 depends on those being merged. Tasks 7 and 8 gate
the report, so Hari's two items are on the critical path and should not wait.

Please push small and push often rather than one large branch each at the end. Unity scene
files cannot be merged, so if you need to edit a scene, say so first.
