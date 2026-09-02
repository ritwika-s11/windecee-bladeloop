# BladeLoop — What We're Building

**Single source of truth. If anything else disagrees with this doc, this doc wins.**

Author: Ritwika Sen (DE Lead) · 30 August 2026
Sprint review: **11 September** · Submission: **25 September**
We start from **current `main`**.

Read this whole doc before you write any code. Then read your own brief in `docs/`.
Come back with feedback — this gets finalised before work starts.

---

## 1. The problem

The feedback was, in effect: *"nice video, but anyone can make a nice video."*

That is fair about what we have today. Right now the app is:

```
   MAIN MENU
      ├── Full Plant Tour  ──►  4.5 minutes of narration. You watch. Nothing you do matters.
      └── Plant Explorer   ──►  Sliders and numbers. Changes nothing you saw in the tour.
```

**Two halves that never meet.** The tour doesn't know the sliders exist. The sliders don't know
the tour exists. So the tour is a video, and the dashboard is a calculator, and neither one is a
product.

---

## 2. The vision

> **BladeLoop turns a customer's order for recovered glass fibre into plant settings,
> then shows you the plant running at those settings and what actually came out.**

The user is no longer an audience. They are running a recycling plant against a real job.

```
   ┌──────────┐        ┌──────────┐        ┌──────────┐
   │  ORDER   │  ────► │   PLAN   │  ────► │  PROVE   │
   └──────────┘        └──────────┘        └──────────┘
   A customer needs     What settings       Watch the plant run
   X tonnes of fibre    deliver that?       at those settings, and
   at Y grade                               read what came out
```

Everything we build for the rest of this project fits into one of those three boxes.

**We are not starting over.** We already have the four stage scenes, the narration, the process
model and the dashboard. What's missing is that they don't share anything. We're connecting them.

---

## 3. The idea that makes it work

**There is no wrong parameter set. There is only a different customer.**

This is the answer to the obvious challenge — *"if you already know the best settings, why let
anyone change them?"*

Run the plant badly and you don't get failure. You get coarser, less pure fibre — and there is a
real buyer for that. Recovered material is sold by grade, not accepted or rejected.

```
                 fibre purity ▲
                              │
   HIGH GRADE   ≥90% / ≥85%   │  ██████   ──►  composite manufacturing
                              │                 (new panels, structural parts)
   MID GRADE    ≥78% / ≥70%   │  ████     ──►  precast concrete, casting
                              │                 (reinforcement filler)
   LOW GRADE    below that    │  ██       ──►  cement works
                              │                 (co-processed as fuel + raw material)
                              └──────────────────────────────────────►
                                                    tensile retention
```

**These tiers are anchored to real published results** (Anjani and Hari, 30 Aug — see
`docs/CEE-deliverable.md` and `docs/grade-threshold-reasoning.md`):

- Standard single-step pyrolysis of real wind-blade waste achieves **~72–76 %** tensile retention
- Optimised two-step pyrolysis reaches **~90–93 %**
- A published two-step study on wind-blade epoxy GFRP reports **76 % tensile / 88 % modulus** —
  our mid-grade preset reproduces this to within half a percent

**Both tiers below the top describe markets that already exist.** Regen Fiber sells shredded blade
fibre into precast concrete today; cement co-processing is currently the most commercially mature
end-of-life route for blade material at scale. We didn't invent these customers.

> ⚠️ **The threshold *numbers* remain our project assumptions.** No published grading standard for
> recovered composite glass fibre exists — the closest analogue, PAS 101, covers container cullet
> glass only. The tiers are calibrated to demonstrated performance, not derived from a standard.
> **Nobody writes "industry standard"** in the app, a slide, or the report.
>
> Also: **"purity" is our own definition** — the literature reports tensile and modulus retention,
> not purity %. We define it as *the mass fraction of recovered material that is fibre rather than
> adhered char and resin residue*, and we say so on the How it works page.

---

## 4. The result that proves it isn't a video

These come from running our own `ProcessModel.cs`. Not estimates.

| | Temp | Retention | Feed | Particle | Efficiency | Purity | Tensile | **Fibre out** |
|---|---|---|---|---|---|---|---|---|
| **High grade** | 600 °C | 35 min | 6,500 kg/h | 2 mm | 100 % | 93.0 % | 90.0 % | **4,482 kg/h** |
| **Mid grade** | 580 °C | 35 min | 8,000 kg/h | 8 mm | 76 % | 82.5 % | 76.5 % | **4,691 kg/h** |
| **Low grade** | 550 °C | 35 min | 8,800 kg/h | 16 mm | 50 % | 69.8 % | 58.3 % | **4,091 kg/h** |

**Look at the last column.**

```
   fibre produced per hour
   HIGH  ████████████████████░  4,482 kg/h   at 99% purity
   MID   █████████████████████  4,691 kg/h   at 88% purity   ◄── MORE fibre, lower value
   LOW   ██████████████████░░░  4,091 kg/h   at 74% purity
```

The mid-grade run makes **more** fibre per hour than the "perfect" run, because you push more
material through — but it's worth less per tonne. And low grade makes less than either, because
losses climb faster than throughput.

That trade-off falls out of our own model. It is the single best thing we can show, and there is
no way to fake it with a video.

### The three presets process the same wind farm

The order sizes are chosen so that **all three consume the same feedstock — about 7,000 tonnes,
619 blades, 206 turbines.** Same input. Three different products.

| Preset | Order | Feedstock | Blades | Turbines | Days | Grade achieved |
|---|---|---|---|---|---|---|
| **High** | 4,800 t | 6,962 t | 616 | 205 | **44.6** | 93.0 % / 90.0 % |
| **Mid** | 4,100 t | 6,992 t | 619 | 206 | **36.4** | 82.5 % / 76.5 % |
| **Low** | 3,250 t | 6,991 t | 619 | 206 | **33.1** | 69.8 % / 58.3 % |

Holding the input constant is what makes the comparison land. A professor can click all three and
see that the *same wind farm* becomes 4,800 t of structural-grade fibre in 45 days, or 3,250 t of
cement feedstock in 33 days.

### Two ways to read the trade-off — both true, and they point opposite ways

This is the most interesting thing in the model, and it's worth understanding before you present it:

| | Winner | Why |
|---|---|---|
| **Per hour** (plant capacity) | **Mid grade** — 4,691 kg/h vs 4,482 | You push more material through |
| **Per blade** (material yield) | **High grade** — 4,800 t vs 4,100 from the same farm | Less lost to char and dust |

Over a full year of continuous running, the per-hour effect wins: mid grade yields **41,096 t/yr**
against high grade's **39,260 t/yr**. But from any *fixed* pile of blades, high grade extracts more.

So the honest framing is: **run for quality and you waste less of each blade but tie up the plant
longer; run for throughput and you turn the plant over faster but throw more away.** That is a real
capacity-versus-yield decision, and it is not something you can fake with a video.

### The shredder capacity constraint — the rule that makes all of this hold

*Added 30 Aug after Akshat ran the inverse solver against the model. Read this bit carefully; the
whole product rests on it.*

Without one extra rule, the trade-off above is an illusion. Akshat searched the parameter space and
found a single setting — **600 °C · 35 min · 9,000 kg/h · 0.5 mm** — that returns **5,911 kg/h at
90.3 % purity and 90 % tensile.** That qualifies as *high grade*, beats our high-grade preset by
32 %, and is the answer the solver gives for **every** target grade.

If a professor opened Custom Order and the solver answered honestly, it would hand back that setting
and *"there is no wrong parameter set, only a different customer"* would be disproved by our own
tool. That is a worse position than we are in today.

**Why the model allows it.** Two gaps:

1. `DevParticle = max(0, P − 2) / 18` — so 0.5 mm, 1 mm and 2 mm score **identically.** The app calls
   2 mm the optimum while the arithmetic gives finer grinding away for free.
2. Feed rate is nearly free. Fibre output scales linearly with feed, but feed's only penalty carries
   weight 0.16. Going 6,500 → 9,000 kg/h buys +38 % material for about 3 points of purity.

**The fix, in one sentence: finer shredding is slower shredding, so particle size caps feed rate.**

```
Fmax(P) = 6500 + 1106.1 · ln(P / 2)        clamped to [4,000 … 9,000] kg/h

    2 mm  →  6,500 kg/h        8 mm  →  8,033 kg/h        16 mm  →  8,800 kg/h
```

The three preset feed rates were chosen by hand before anyone derived this, and **all three land on
that curve** — 2 mm and 16 mm exactly, 8 mm within 0.4 %. The intuition was already in the product;
it just was never written into the maths. `FeedInfo()` and `CauseEffect()` have been describing this
coupling in prose all along.

With the constraint applied, the frontier becomes real:

```
   particle    max feed     fibre out    purity
    1 mm        5,733        3,896        92.2      finer costs you
    2 mm        6,500        4,482        93.0
    4 mm        7,267        4,755        89.5
    6 mm        7,715        4,810        86.2   ◄── PEAK, and it's mid grade
    8 mm        8,033        4,774        83.2
   16 mm        8,800        4,278        71.5
   20 mm        9,000        3,921        65.8
```

**Rise, then fall.** Mid grade genuinely out-produces high grade, low grade loses to both, and no
solver can game its way around it. It also kills the sub-2 mm exploit for nothing, because 1 mm caps
feed at 5,738 kg/h.

> **This is one constraint inside `OrderSolver`. It changes no existing `ProcessModel` formula**, so
> the additive-only rule in §8 still holds. It does mean the feed slider on the Custom Order screen
> must be bounded by the particle-size slider.

One consequence to be aware of: the solver's best mid-grade answer is 6 mm / 7,715 kg/h (4,810),
marginally better than our 8 mm mid preset (4,774 at that cap, 4,691 as configured). The presets stay as they are — they are
illustrative customer orders, not solver output — so a user who runs the preset and then opens
Custom Order will be told they could do slightly better. That reads as the tool being useful.

### What the constrained solver actually returns — and how to talk about low grade

| Target | Best settings | Fibre out | Achieved |
|---|---|---|---|
| High | 600 °C · 35 min · 7,150 kg/h · 3.6 mm | 4,725 kg/h | 90.1 % / 86.8 % |
| Mid | 600 °C · 35 min · 7,715 kg/h · 6.0 mm | **4,810 kg/h** | 86.2 % / 82.0 % |
| Low | *same as mid* | 4,810 kg/h | 86.2 % / 82.0 % |

Mid beats high on throughput — the thesis holds, causally. But note the third row: **asked for low
grade, the solver still recommends the mid-grade settings**, because running coarser than ~6 mm
costs more than it gains.

That is a true and useful result, and it changes how we should frame the low tier:

> **Low grade is not something you choose. It is where you land.**
> Contaminated or oversized feedstock, a shredder at its limit, an under-fired kiln, a deadline that
> forces throughput — these produce low-grade output. The cement works exists to buy it when that
> happens. Nobody deliberately degrades their product; they degrade it by accident, or by
> constraint, and the value of a grade-tiered market is that the material still has somewhere to go.

Word the low-grade order card and its narration accordingly — *"when the plant can't do better,
there's still a buyer"*, not *"a customer who wants worse fibre."*

---

## 5. What the user sees

> ## ✅ Built 1 Sep — what's actually on `main`
>
> The home page is no longer the wireframe below. It is a **full-bleed dusk wind farm** —
> real geometry from `Stage1-WindFarm.fbx`, eleven turbines with blades turning, camera
> drifting on three out-of-phase sine waves, linear fog hiding the terrain edge — with the
> UI floating over it on a two-band scrim.
>
> **Three changes from the plan, all deliberate:**
>
> 1. **The tiles show the four settings**, in mono. The presets exist to teach someone what
>    to type into Custom Order, so the settings are the content — not the tonnage.
> 2. **Each tile carries a live stacked output bar** from `OutputSplit()`, same five streams
>    and colours as Plant Explorer. High grade's char block is 5.9 %, low grade's is 26.5 % —
>    over four times wider. The product's whole argument, readable in half a second.
> 3. **No invented company names.** Buyer type plus a sourced end-use line (§3).
>
> Typography is **IBM Plex Sans + Mono** (SIL OFL). The TMP assets live in
> `Assets/Resources/Fonts/` *specifically* so `Resources.Load` resolves them in a player
> build — move them somewhere tidier and the fonts vanish from the Windows executable while
> still working in the editor. `Assets/Fonts/OFL.txt` must ship with any distribution.
>
> **Still to do:** de-template the palette (it is currently generic dark-dashboard blue
> rather than anything drawn from the materials), and an operational status strip. Both are
> polish — the tour is the higher priority.

### 5.1 Homepage — the original plan (superseded by the note above)

The current menu (stage buttons, Full Plant Tour, Plant Explorer) **goes away**. In its place:

```
┌═══════════════════════════════════════════════════════════════════┐
║                          B L A D E L O O P                        ║
║              Recovering glass fibre from wind turbine blades      ║
╠═══════════════════════════════════════════════════════════════════╣
║                                                                   ║
║        There is no wrong setting — only a different buyer.        ║
║                                                                   ║
║   ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐    ║
║   │   HIGH GRADE    │ │   MID GRADE     │ │   LOW GRADE     │    ║
║   │  Composite      │ │  Precast        │ │  Cement         │    ║
║   │  manufacturer   │ │  concrete       │ │  works          │    ║
║   │                 │ │                 │ │                 │    ║
║   │ Clean enough to │ │ Not structural, │ │ Glass replaces  │    ║
║   │ go back into    │ │ but sold today  │ │ sand, resin     │    ║
║   │ new structural  │ │ as reinforcing  │ │ replaces coal.  │    ║
║   │ parts.          │ │ filler.         │ │ There is always │    ║
║   │                 │ │                 │ │ a buyer.        │    ║
║   │    4,800 t      │ │    4,100 t      │ │    3,250 t      │    ║
║   │  ≥90% purity    │ │  ≥78% purity    │ │  any grade      │    ║
║   │                 │ │                 │ │                 │    ║
║   │ ~206 turbines   │ │ ~206 turbines   │ │ ~206 turbines   │    ║
║   │    45 days      │ │    36 days      │ │    33 days      │    ║
║   │                 │ │                 │ │                 │    ║
║   │  [ Run order ]  │ │  [ Run order ]  │ │  [ Run order ]  │    ║
║   └─────────────────┘ └─────────────────┘ └─────────────────┘    ║
║                                                                   ║
║          ┌───────────────────────────────────────────┐            ║
║          │   CUSTOM ORDER — set your own target →    │            ║
║          └───────────────────────────────────────────┘            ║
║                                                                   ║
║                      · How it works ·                             ║
╚═══════════════════════════════════════════════════════════════════╝
```

The three cards deliberately show **the same turbine count**. That is the point: one decommissioned
wind farm, three customers, three very different outcomes.

**No invented company names.** An earlier draft gave each card a fictional German firm; the buyer
*type* already carries the argument, and a name needed a "these are fictional" disclaimer while
adding nothing. What each card carries instead is a **true, sourced line about who actually buys
that grade** — precast concrete really does take shredded blade fibre today, and cement
co-processing really is the most commercially mature route at scale (both from
`docs/CEE-deliverable.md` §3).

That change also puts the thesis on the landing page. The obvious challenge — *"why would anyone
run the plant badly?"* — is answered before anyone asks it.

**Three things, one of them clearly primary.** No stage buttons. No standalone "Full Plant Tour"
button — the tour is what an order *plays*. No separate "Plant Explorer" button — its sliders and
tanks become the Custom Order screen.

Same content as today. Better reason to click it.

### 5.2 Running an order — the dual screen

This is the heart of the product. **Parameters and consequences in the same frame, at the same
time.** One screenshot proves the whole argument.

```
┌──────────────────────────────────────────────┬──────────────────────────┐
│                                              │  ▓▓ HIGH GRADE ▓▓        │
│                                              │  Composite manufacturer  │
│                                              │  ────────────────────────│
│                                              │  SETTINGS                │
│           [  3D plant, playing  ]            │   600 °C     35 min      │
│                                              │   6,500 kg/h   2 mm      │
│                                              │  ────────────────────────│
│                                              │  OUTPUT                  │
│                                              │   fibre  ████████  69%   │
│  "Glass fibre is the heavier fraction.       │   oil    ██        16%   │
│   It drops out first."                       │   syngas █          8%   │
│         [ ◀ Back ]   [ Next ▶ ]              │   char   █          6%   │
├──────────────────────────────────────────────┤   loss             1.5%  │
│  Farm · Shred · Kiln · Separate     [Skip →] │                          │
└──────────────────────────────────────────────┴──────────────────────────┘
        left ~72% — the tour                      right ~28% — live panel
```

The tour renders into the left 72 % of the window. The panel on the right updates live and
persists across all four stages.

**Nobody sits through five minutes.** So: a chapter bar to jump between stages, and a
**Skip to results** button. Someone who wants the story watches it. Someone who wants the answer
clicks twice. Even at 20 seconds in, the panel already tells you what's happening.

### Plant Explorer is transitional — do not invest in it

**It stays for now.** Integration is in progress and nothing should get lost while the pieces are
being connected. It is on the home page and in Build Settings, and it works.

**It goes away in the final version**, and the reason is worth understanding: once every run ends
in an outcome report showing statistics alongside what you watched, and Custom Order shows those
same statistics tailored to your order, a separate free-play dashboard has nothing left to do. Its
job — "move sliders, watch numbers respond" — is absorbed by the two screens that have a reason for
the numbers to exist.

**What this means in practice:**

- **Sharan:** don't spend time polishing Plant Explorer. Build Custom Order and the Outcome Report
  in the home page's palette from the start, so they don't need restyling later.
- **Nobody deletes it** until both replacement screens are working end to end.
- Its components are not wasted — the sliders, tanks and quality metrics are exactly what Custom
  Order needs. It is being promoted, not thrown away.

### 5.3 Custom order — this is what Plant Explorer becomes

Plant Explorer's sliders, tanks and quality metrics don't disappear. They **become** this screen,
reached through the order flow instead of sitting as a separate tab.

```
┌───────────────────────────────────────────────────────────────┐
│  CUSTOM ORDER                                     [ ← Menu ]  │
│  ───────────────────────────────────────────────────────────  │
│  Customer   [ Custom order            ]                       │
│  Grade      ( ) High   (•) Mid   ( ) Low                      │
│  Quantity   [────────●─────────]   4,100 tonnes               │
│                                                               │
│                     [   S O L V E   ]                         │
│  ───────────────────────────────────────────────────────────  │
│  THESE SETTINGS DELIVER IT                                    │
│    Temperature  ●──────────  580 °C                           │
│    Retention    ────●──────   35 min      ← adjustable, live  │
│    Feed rate    ───────●───  8,000 kg/h                       │
│    Particle     ──●───────      8 mm                          │
│  ───────────────────────────────────────────────────────────  │
│  YOU WOULD GET            │  THIS ORDER TAKES                 │
│   fibre  ██████  58.6%    │   6,992 t of blade material in    │
│   oil    ██      14.3%    │   619 blades · ~206 turbines      │
│   syngas █        7.4%    │   874 h · 36.4 days at 24/7       │
│   char   ██      15.5%    │                                   │
│   loss            4.1%    │   ▓▓ MID GRADE ▓▓                 │
│   purity 82.5%  tensile 76.5%                                 │
│  ───────────────────────────────────────────────────────────  │
│                              [ Watch this run → ]             │
└───────────────────────────────────────────────────────────────┘
```

Solve first, then let the user nudge the sliders and watch the grade badge move. That turns a
one-shot calculator into something you can play with — and it's the Plant Explorer behaviour we
already have, put to a purpose.

### 5.4 Outcome report — the end of a run

```
┌───────────────────────────────────────────────────────────────┐
│                    ORDER FILLED — MID GRADE                   │
│  ───────────────────────────────────────────────────────────  │
│  Settings used:  580 °C · 35 min · 8,000 kg/h · 8 mm          │
│                                                               │
│  You produced 4,691 kg/h of fibre at 88% purity.              │
│  That is MORE fibre per hour than the high-grade setting      │
│  (4,482 kg/h) — but at lower value per tonne.                 │
│                                                               │
│  Goes to: precast concrete and casting.                       │
│  ───────────────────────────────────────────────────────────  │
│  [ Try different settings ]              [ ← Menu ]           │
└───────────────────────────────────────────────────────────────┘
```

**Missing the target is never a failure — it's a different customer.** The words "fail" and
"error" never appear on this screen.

### 5.5 How it works

A plain, scrollable page: what we model, where the numbers come from, and — most importantly —
our assumptions stated openly and labelled as assumptions. This page is what protects us in the
defence.

---

## 6. What each stage does now

An earlier version of this plan cut Stages 1 and 2. That was wrong — **the order reaches into
every stage**, and Stage 2 holds the most influential setting in the entire model.

```
   STAGE 1          STAGE 2          STAGE 3          STAGE 4
   Wind farm   ─►   Shredding   ─►   Kiln       ─►   Separation
   ────────         ────────         ────────         ──────────
   How much         What size        How hot,         What you
   material         to shred to      how long         actually got
   this order
   needs
                    ▲
                    │
        weight 0.32 in the model — MORE than temperature.
        This is where the biggest decision is made.
```

**Transport is a pass-through.** `Transport_StoryMode` sits between Stages 1 and 2 and stays in the
chain — the order panel remains docked and the viewport split applies, but it gets **no chapter chip**.
It's a 13-second transition, not a stage.

| Stage | The order makes it answer | What visibly changes |
|---|---|---|
| **1 — Wind farm** | How much blade material this order needs | (opening beat) |
| **2 — Shredding** | What size to shred to | Granule size in the output pile — fine sand vs chunky flakes |
| **3 — Kiln** | Temperature and retention | Kiln glow brightness/colour, rotation speed |
| **4 — Separation** | What came out, at what grade | Fibre stream vs char stream — the money shot |

On a low-grade run the char drums should be visibly busy while the fibre box fills slowly. That
one contrast makes the trade-off undeniable without a single number on screen.

### Stage 1 gets real numbers

Anjani and Hari sourced an average blade mass: **11.3 t**, for a 2 MW-class blade (LM 56.8 P) —
representative of the generation now being decommissioned in Europe. Cross-checked against the
10–14 t/MW rule of thumb.

The app must state this as an assumption: *"Assumes an average 2 MW-class blade at ~11 tonnes —
actual blade mass varies significantly by turbine size and generation."*

At 11.3 t/blade and 3 blades per turbine, the three presets each draw on:

| Preset | Order | Feedstock | Blades | Turbines |
|---|---|---|---|---|
| High | 4,800 t | 6,962 t | 616 | 205 |
| Mid | 4,100 t | 6,992 t | 619 | 206 |
| Low | 3,250 t | 6,991 t | 619 | 206 |

**Order sizes were chosen to make these match.** A ~200-turbine decommissioning programme is a
believable scale, and holding it constant across all three presets is what makes them comparable.
Stage 1 can therefore open every run with the same line — *"this order needs about 620 blades from
roughly 206 turbines"* — and the difference shows up downstream, where it belongs.

---

## 7. Who does what

Each person also has a technical brief in `docs/` to give their Claude.

### Ritwika — homepage, narration, subtitles, integration

0. ✅ **`OrderContext.cs` + `OrderSolver.cs`** — written and merged 31 Aug, taken off Akshat so
   Anirban and Sharan weren't queued behind him. Plus `OrderSelfTest.cs`
   (**BladeLoop → Verify Order Model**).
0b. ✅ **Home page rebuilt** (1 Sep) — see §5.1. Also shipped: `TourRunner.cs` stub,
   `OrderContext.TourSplitWidth`, `HomeStageDrift.cs`, and IBM Plex fonts.
1. Rebuild `MainMenu.unity` as the order homepage (§5.1)
2. Rewrite the narration scripts for all four stages — removing every hard number, since
   parameters now vary per order
3. Re-record the voiceover
4. Subtitles: `SubtitleTrack.cs`, the cue files and the timings. **Anirban adds the `SubtitleCanvas`
   and component to Stages 1–3** — they're his scenes under Rule 1, and he's rebuilding every overlay
   canvas in them during his Task 1 anyway. One pass instead of two, and no merge conflict.
   (On `main` today, subtitles exist in `Stage4_V2` only, with one cue file.)
5. Review and merge every PR; keep `main` green
6. Produce the WebGL build for the review

### Akshat — the shared spine (`docs/handover-akshat.md`)

> ✅ **`OrderContext.cs` and `OrderSolver.cs` are done and on `main`** — Ritwika took them on 31 Aug
> so the Unity-side work wasn't queued behind them. Both are pure C# and implemented exactly to
> `docs/interface-contract.md`. Run **BladeLoop → Verify Order Model** in the editor to see them
> checked against the canonical numbers. **Nobody is blocked on you any more.**
3. The dual screen — viewport split plus the persistent order panel across all four stages.
   Includes **FOV compensation**: Unity fixes vertical FOV and derives horizontal, so a 72 %
   viewport silently cuts 28 % of horizontal field from all ~40 shots. One line of code
   (`newVFov = 2·atan(tan(vFov/2)/0.72)`) restores the framing — do this **before** anyone
   re-frames a shot by hand.
4. Chapter navigation and *Skip to results*
5. **Fix Explore mode getting stuck** (professor feedback). Owns `PauseFramePreserver.cs`,
   `ExploreOrbitCamera.cs`, `ExploreClickRaycaster.cs`. Writes the scene-side spec for Anirban by
   **Wed 2 Sep**.

   ⚠️ **Play it on current `main` before writing any code.** Both root causes in the original
   diagnosis — the pause pivot hitting the floor, and pitch clamped to ~9° of travel — were fixed by
   Anirban's PR, which merged *after* that review. Current `main` has `minPitch 5 / maxPitch 75`
   (70° of travel), damping and spin inertia, and `PauseFramePreserver` in all four stage scenes. If
   it still sticks, send Anirban the repro; he wrote those versions.

*Code only, no scenes — so he never blocks anyone on a file lock. The order panel is a
`DontDestroyOnLoad` canvas built at runtime in C#, the same pattern the dashboard already uses.
There is no prefab to position in the Scene view, and `Camera.rect` is set from code.*

### Anirban — making parameters visible (`docs/handover-anirban.md`)

1. 🔴 **Wrap all 14 overlay canvases** so they stay inside the tour viewport when the screen splits.
   Not five — fourteen, and **every one is Screen Space – Overlay, which ignores `Camera.rect`
   entirely.** Akshat's split narrows the 3D render and leaves all of them covering the full screen
   on top of the order panel. This task is what makes the dual screen work, not tidying-up.
   Also adds `SubtitleCanvas` + component to Stages 1–3 in the same pass (see Ritwika's item 4).
2. Stage 2 — granule size responds to particle size
3. Stage 3 — kiln glow and rotation respond to temperature and retention
4. Stage 4 — fibre and char streams respond to the output split
5. **Add clickable parts to Stage 4 and fix the Stage 3 ones** (professor feedback, previously
   unassigned). `Stage4_V2` currently has **zero** `ClickablePart`, `PartInfoPanel` and
   `ExploreClickRaycaster` components — nothing in our money-shot stage can be clicked. Stage 3's
   22 clickable parts are all inside the cutaway, so none are active while paused. Akshat gives you
   a spec for both.
6. Fix any camera shots that still crop badly at 72 % width — **after** Akshat's FOV compensation
   lands (Wed 2 Sep), which fixes most of them automatically

*Owns all five stage scenes. Nobody else opens them.*

**Priority, if the week runs short.** Task 1 happens Monday by necessity and is not cuttable — without
it every screenshot of the dual screen has overlays smeared across the panel. After that:
**4 → 2 → 3 → 5 → 6 → 7.** Cut from the bottom: Task 7 first, then Task 6, then Task 3's Change 4
(feed → airlock flow, the least visible), then Stage 4's optional cues.

### Sharan — the settings and results screens (`docs/handover-sharan.md`)

1. **Remove the miniature 3D kiln** from the dashboard — it's not part of the product any more
2. Rebuild Plant Explorer as the **Custom Order** screen (§5.3)
3. Build the **Outcome Report** screen (§5.4)
4. Write and build the **How it works** page (§5.5)

### Anjani & Hari — the numbers behind the claims (`docs/handover-cee-orders.md`)

No Unity. ✅ **Delivered 30 August** — see `docs/CEE-deliverable.md` and
`docs/grade-threshold-reasoning.md`. Their sourcing is what drove the model re-anchor in §3 and §4.
Nothing outstanding from them.

---

## 8. How we work together

We are four people on four branches in one Unity project. Unity is unusually hostile to this.

### Rule 1 — `.unity` scene files cannot be merged. Ever.

Git will produce a "merged" scene file that Unity then cannot open. **One owner per scene.**
Need a change in someone else's scene? Message them. Don't open it.

| Scene | Owner |
|---|---|
| `MainMenu.unity` | Ritwika |
| `Stage1_StoryMode`, `Transport_StoryMode`, `Stage2_StoryMode`, `Stage3_StoryMode`, `Stage4_V2` | Anirban |
| `PlantExplorer.unity` and any new dashboard scene | Sharan |
| *(none — code only)* | Akshat |

### Rule 2 — one owner per script

| File | Owner |
|---|---|
| `ProcessModel.cs` | Sharan — **additive only**, never change an existing formula |
| `PlantExplorerController.cs` | Sharan |
| `TourSceneSequencer.cs`, `StoryModeController.cs`, `PauseFramePreserver.cs`, `ExploreOrbitCamera.cs`, `ExploreClickRaycaster.cs`, `TourRunner.cs` | Akshat |
| `OrderContext.cs`, `OrderSolver.cs`, `OrderSelfTest.cs` | Ritwika — **moved from Akshat 31 Aug**, already written and merged |
| `KilnRotator.cs`, `TemperatureRampAnimator.cs`, `AirlockFlowController.cs`, stage animators | Anirban |
| `SceneLoader.cs`, `SubtitleTrack.cs` | Ritwika |

Need a change in a file you don't own? Ask. Don't edit it "just quickly".

### Rule 3 — rebase on main every morning

```bash
git fetch origin
git rebase origin/main
```

A branch that has drifted for four days will not merge in the time we have.

### Rule 4 — small PRs, merged fast

Push at the end of every session, even if incomplete. Open a PR the moment a piece works.
**Do not save up a week of work for one big PR.**

### Rule 5 — the project must compile before you push

Zero errors in the Unity console. A branch that doesn't compile blocks everyone who rebases.

### Rule 6 — every feature must no-op without an order

Guard every change with "is there an active order?", so that with no order the code falls back to
today's behaviour. This is an **internal code-guard rule, not a user-facing promise** — once the
homepage changes, there is no route in the shipped build to a no-order state, so "free play" is
something we rely on for safe development and testing, not a mode we ship.

Why it still matters: it keeps every stage scene independently playable in the editor, which is how
Anirban works, and it means a null `OrderContext` can never crash the build.

---

## 9. Timeline

*Day names corrected 31 Aug — the previous version had them all a day out.*

| Date | Milestone |
|---|---|
| ~~Sun 30 Aug~~ | ✅ CEE numbers delivered. Doc reviews in from Akshat, Sharan, Anirban. |
| **Mon 31 Aug — today** | ✅ **`OrderContext` + `OrderSolver` written and merged** (Ritwika, taken off Akshat). Anirban and Sharan unblocked. Akshat starts on the dual screen. |
| **Tue 1 Sep** | Dual screen and viewport split taking shape. |
| **Wed 2 Sep** | Akshat's Explore spec to Anirban. FOV compensation merged. Narration scripts drafted. |
| **Fri 4 Sep** | Dual screen working on one stage. Custom Order screen functional. Homepage laid out. |
| **Mon 7 Sep** | All four stages responding to parameters. Voiceover re-recorded. |
| **Wed 9 Sep** | 🔴 **Feature freeze.** All branches merged. Bug fixes only after this. |
| **Thu 10 Sep** | WebGL build. Full run-through. Rehearse the demo. |
| **Fri 11 Sep** | **Sprint review.** |
| 12–24 Sep | Report, plus anything the review asks for. |
| **Fri 25 Sep** | **Submission.** |

The 9 September freeze is not negotiable. Not merged by then means it doesn't ship.

### Why this moved

Anirban's Task 1 and Sharan's Task 3 both read `OrderContext`, and Akshat had no time on Monday —
so two people would have lost a day waiting on one file. Both files are pure C# with no Unity
dependencies and were fully specified in the interface contract, so writing them was implementing a
spec rather than designing one. Akshat keeps the Unity-side work that genuinely needs him: the dual
screen, the viewport split, Explore mode and chapter navigation.

---

## 10. What we are deliberately not building

Named here so nobody spends a day on them:

- ❌ Inventory or stock tracking
- ❌ Cost or revenue in euros
- ❌ Stage 5 (energy recovery) — the scene exists but stays out of the build
- ❌ The miniature 3D kiln in the dashboard — being removed
- ❌ Re-cutting the narration to be shorter — we solve that with skip navigation
- ❌ Any new 3D modelling

---

## 11. Answering the professors

**"Why let the user change parameters if you already know the optimum?"**

> Because the optimum is only optimal for one customer. A composite manufacturer needs high-purity
> fibre and will pay for it. A cement works takes coarse material at a much lower spec. Our model
> shows that running for the concrete market actually produces *more* fibre per hour than running
> for the composite market — it's just worth less per tonne. The user is choosing a business
> position, and the plant settings follow from it.

**"Is this still just a video?"**

> Open the custom order screen and type a target we've never used. The settings are solved live and
> the plant runs at them. There is no recorded version of that.

**"Where do your numbers come from?"**

> The baseline output proportions come from the CEE reference model. The deviation weights, loss
> behaviour and quality curves are ours, calibrated to that baseline. Our grade thresholds are
> stated project assumptions and labelled as such in the app — we haven't found published
> standards for them and we don't claim any.

---

## Feedback

Read this, sit with your Claude, and come back with anything that's unclear, wrong, or that you
think won't work. Better to change the plan tomorrow than on 10 September.

Questions to Ritwika.
