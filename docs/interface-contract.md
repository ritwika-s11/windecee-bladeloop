# BladeLoop — Interface Contract

**Author:** Ritwika Sen · 31 August 2026
**Binding on:** Akshat (implements) and Sharan (calls). Anirban reads §2 only.

This file exists so Sharan can build his screens **before** Akshat's code is merged. Both of you
build against the signatures below. Akshat implements to them; Sharan calls them.

> **If either of you needs a signature changed, message the group and change this file first.**
> Do not change it silently on your branch — the other person is already coding against it.

---

## 1. Who owns which number

Sharan asked this directly (S1). The rule is:

| Kind of number | Comes from | Why |
|---|---|---|
| **Per-hour and quality** — output streams, efficiency, purity, tensile | `ProcessModel` (i.e. `result.model` / `OrderContext.Model`) | These depend only on the four settings, not on the order |
| **Per-order and campaign** — feedstock tonnes, blades, turbines, running hours, days | `OrderContext` | These need `targetTonnes`, which lives on the `Order`, not the model |

So: **anything with "per hour" or "%" in it, call the model. Anything with "for this order" in it,
call OrderContext.** Never recompute a campaign figure yourself.

---

## 2. `OrderContext.cs` — owner: Akshat

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
    // ---- state ----
    public static Order        Active;      // null = no order (free play / editor)
    public static ProcessModel Model;       // never null; defaults to the design case
    public static bool         HasOrder => Active != null;

    public static void SetOrder(Order o, ProcessModel m);
    public static void Clear();

    // ---- grade tiers (single source of truth; do not hardcode these anywhere else) ----
    public const float HighPurity = 90f, HighTensile = 85f;
    public const float MidPurity  = 78f, MidTensile  = 70f;

    public static Grade GradeOf(float purityPct, float tensilePct);
    public static Grade AchievedGrade { get; }      // GradeOf(Model.FiberPurityPct, Model.TensileRetentionPct)
    public static bool  MeetsTarget   { get; }      // AchievedGrade is at least Active.targetGrade

    // ---- campaign figures for the active order ----
    // All return 0 when HasOrder is false. Never throw.
    public static float FibreKgH         { get; }   // Model.OutputSplit().GlassKgH
    public static float FeedTonnesNeeded { get; }   // targetTonnes / (GlassKgH / FeedKgH)
    public static float CampaignHours    { get; }   // targetTonnes * 1000 / GlassKgH
    public static float CampaignDays     { get; }   // CampaignHours / 24
    public static int   BladesNeeded     { get; }   // FeedTonnesNeeded / BladeMassTonnes
    public static int   TurbinesNeeded   { get; }   // BladesNeeded / BladesPerTurbine

    // ---- sourced assumptions (CEE team, 30 Aug) ----
    public const float BladeMassTonnes  = 11.3f;    // 2 MW-class blade, LM 56.8 P
    public const int   BladesPerTurbine = 3;

    // ---- the three presets ----
    public static readonly Preset[] Presets;        // High, Mid, Low in that order
    public struct Preset
    {
        public Order        order;
        public ProcessModel model;
        public string       endUse;     // "Composite manufacturing" etc.
    }
}
```

**Preset values — do not round these.** They are chosen so all three consume the same feedstock
(~6,990 t ≈ 619 blades ≈ 206 turbines), which is what makes the three runs comparable.

| Grade | Customer type | Order | Temp | Retention | Feed | Particle |
|---|---|---|---|---|---|---|
| High | Composite manufacturer | 4,800 t | 600 °C | 35 min | 6,500 kg/h | 2 mm |
| Mid | Precast concrete producer | 4,100 t | 580 °C | 35 min | 8,000 kg/h | 8 mm |
| Low | Cement works | 3,250 t | 550 °C | 35 min | 8,800 kg/h | 16 mm |

Customer *names* are Ritwika's — use placeholders until she supplies them.

---

## 3. `OrderSolver.cs` — owner: Akshat

```csharp
public static class OrderSolver
{
    public struct Result
    {
        public ProcessModel model;      // the settings found; null if !feasible
        public bool         feasible;
        public string       note;       // plain-language reason when !feasible; may be empty otherwise
    }

    // Highest fibre throughput that still reaches targetGrade.
    public static Result Solve(Grade targetGrade);

    // Lowest kiln temperature that still reaches targetGrade.
    public static Result SolveGentlest(Grade targetGrade);

    // Maximum feed rate permitted at a given particle size.
    // Finer shredding is slower shredding. Sharan binds the feed slider's
    // upper limit to this. Do NOT reimplement the formula anywhere else.
    public static float MaxFeed(float particleMm);
}
```

`Solve` takes **grade only, not quantity.** Quantity doesn't change which settings are best — it only
changes how long the campaign runs, which `OrderContext` derives.

### `MaxFeed` — the shredder capacity constraint

```csharp
Mathf.Clamp(6500f + 1106.1f * Mathf.Log(particleMm / 2f), 4000f, 9000f)
```

**k = 1106.1, not 1100.** At 1100 the low preset is infeasible by 13 kg/h and the solver would
reject one of our own presets.

| Particle | MaxFeed | Preset | Feasible |
|---|---|---|---|
| 2 mm | 6,500 | 6,500 | ✓ exact |
| 8 mm | 8,033 | 8,000 | ✓ |
| 16 mm | 8,800 | 8,800 | ✓ exact |

Why this exists: without it, `600 °C / 35 min / 9,000 kg/h / 0.5 mm` returns 5,911 kg/h at high
grade — beating every preset on throughput *and* quality, which disproves the product's own thesis.
See §4 of the vision doc.

**When `feasible` is false**, `note` must be readable by a non-engineer, e.g.
*"No settings in the operating envelope reach high grade at that feed rate. Try a coarser target or
accept mid grade."* Sharan displays `note` verbatim.

---

## 4. Starting a run — owner: Akshat *(answers S4)*

```csharp
public static class TourRunner
{
    // Begins the four-stage chain for OrderContext.Active, creates the
    // persistent order panel, and applies the 72% viewport split.
    // No-ops safely if OrderContext.HasOrder is false.
    public static void StartRun();

    // Ends the run immediately and loads the Outcome Report.
    public static void SkipToResults();

    // Jumps to a stage (0 = Farm, 1 = Shred, 2 = Kiln, 3 = Separate)
    // and continues the chain from there.
    public static void JumpToChapter(int index);
}
```

**Sharan's "Watch this run →" button is exactly:**

```csharp
OrderContext.SetOrder(order, result.model);
TourRunner.StartRun();
```

**Ritwika's preset order cards are exactly:**

```csharp
var p = OrderContext.Presets[i];
OrderContext.SetOrder(p.order, p.model);
TourRunner.StartRun();
```

---

## 5. Ending a run — owner: Akshat *(answers S5)*

**Decided: the sequencer loads the scene; Sharan's scene only reads state.**

`TourSceneSequencer` already ends the chain and loads `MainMenu`. It now loads
**`OutcomeReport`** instead, whenever `OrderContext.HasOrder` is true. With no order it still goes to
`MainMenu`, unchanged.

`OutcomeReportController.Start()` reads `OrderContext` and renders. It receives no parameters, waits
for no callback, and subscribes to no event. **Neither person edits the other's file.**

Both exit routes — the chain finishing naturally, and `SkipToResults()` — land in the same scene the
same way.

---

## 6. Scene names

Exact strings. Every one must be ticked in Build Settings.

| Scene | Owner | Purpose |
|---|---|---|
| `MainMenu` | Ritwika | Order homepage |
| `OrderDashboard` | Sharan | Custom Order |
| `OutcomeReport` | Sharan | End-of-run report |
| `HowItWorks` | Sharan | Model and assumptions |
| `Stage1_StoryMode` · `Transport_StoryMode` · `Stage2_StoryMode` · `Stage3_StoryMode` · `Stage4_V2` | Anirban | The tour |

`PlantExplorer` stays in the project while Sharan builds `OrderDashboard` alongside it, and comes out
of Build Settings once the switchover is done.

---

## 7. Behaviour with no order

Per Rule 6, everything must no-op rather than crash when `OrderContext.HasOrder` is false:

- `OrderContext.Model` is **never null** — it defaults to the design case (600 / 35 / 6,500 / 2 mm)
- All campaign figures return `0`
- `TourRunner.StartRun()` returns without doing anything
- No viewport split, no order panel
- Every stage scene stays independently playable in the editor

This is an internal safety property, not a user-facing mode.

---

## 8. Verification table — the canonical one *(answers S3)*

Both of you test against **this table and no other**. It is computed from `ProcessModel.cs` as it
stands on `main` after the 30 Aug re-anchor.

| Preset | Settings | Efficiency | Purity | Tensile | Fibre out |
|---|---|---|---|---|---|
| High | 600 / 35 / 6,500 / 2 mm | 100 % | 93.0 % | 90.0 % | 4,482 kg/h |
| Mid | 580 / 35 / 8,000 / 8 mm | 76 % | 82.5 % | 76.5 % | 4,691 kg/h |
| Low | 550 / 35 / 8,800 / 16 mm | 50 % | 69.8 % | 58.3 % | 4,091 kg/h |

Campaign figures:

| Preset | Order | Feedstock | Blades | Turbines | Days |
|---|---|---|---|---|---|
| High | 4,800 t | 6,962 t | 616 | 205 | 44.6 |
| Mid | 4,100 t | 6,992 t | 619 | 206 | 36.4 |
| Low | 3,250 t | 6,991 t | 619 | 206 | 33.1 |

**Note for Sharan:** the fibre-out figures are *unchanged* from your original brief — the capacity
constraint does not move any preset feed rate, because all three already sit on the curve. Purity and
tensile **did** change, from the CEE re-anchor on 30 Aug. If you see 93.0 / 82.5 / 69.8, that is
correct; the old 99.0 / 88.0 / 74.5 is what's stale.

Constrained solver output, for reference:

| Target | Best settings | Fibre out |
|---|---|---|
| High | 600 / 35 / 7,150 / 3.6 mm | 4,725 kg/h |
| Mid | 600 / 35 / 7,750 / 6.2 mm | 4,808 kg/h |
| Low | *same as mid* | 4,808 kg/h |

The solver's mid answer slightly beats the mid preset. That's expected — presets are illustrative
customer orders, not solver output.

---

## 9. Build order

| When | Who | What |
|---|---|---|
| Now | Sharan | Build the screens against these signatures. They won't compile until Tuesday — that's fine, get the layout and logic done. |
| Now | Akshat | Implement to these signatures exactly. |
| Tue 2 Sep | Akshat | `OrderContext` + `OrderSolver` merged to `main`. |
| Tue 2 Sep | Sharan | Pull, compile, fix up. Should be minutes, not a rebuild. |

If Akshat finds a signature here that's wrong or awkward, **say so before Tuesday** — changing it
after Sharan has built against it costs a day.
