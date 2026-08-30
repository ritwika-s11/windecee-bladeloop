# Narration Scripts v2 — for the Order → Plan → Prove build

**Owner:** Ritwika · **Draft:** 30 August 2026 · **Record by:** Sunday 7 September

Replaces the current voiceover for all four stages plus Transport.
Read `docs/BLADELOOP-PRODUCT-VISION.md` for why.

---

## The one rule that drives this whole rewrite

**No concrete numbers in the voiceover.**

The current scripts say things like *"at six hundred degrees"*, *"it drops out first: seventy
percent"*, *"sixteen percent"*. Every one of those is now wrong for two of the three presets. If a
user runs the low-grade order and the narrator says "six hundred degrees" while the panel says
550 °C, the app contradicts itself in front of the examiner.

So the split is:

| | says |
|---|---|
| **The voiceover** | what the equipment *does* and *why* — mechanism, cause and effect |
| **The order panel** | what the numbers *are* right now — always live, always correct |

This is also better narration. "Hotter feeds break down more completely" is a more useful sentence
than "six hundred degrees", and it stays true at every setting.

### Practical substitutions

| Instead of | Say |
|---|---|
| "at six hundred degrees" | "at the set temperature" / "at working heat" |
| "seventy percent" | "the largest share" / "the heaviest fraction, and most of the mass" |
| "sixteen percent" | "a smaller liquid fraction" |
| "shredded to two millimetres" | "shredded to the size this order calls for" |
| "eight percent, and it goes back to fire the kiln" | "the rest is gas, and it goes back to fire the kiln" |

Comparatives and superlatives are safe. Absolute values are not.

---

## The second change: name the order

Each stage should open by connecting what you're seeing to why you're seeing it. One short clause
is enough — this is what turns a tour into a job.

| Stage | Opening idea |
|---|---|
| 1 — Wind farm | this is where the material for this order comes from |
| 2 — Shredding | the size we shred to is set by the grade this order needs |
| 3 — Kiln | the heat and the time are what decide the quality |
| 4 — Separation | this is where you find out what you actually got |

---

## Timing constraints — do not break these

Anirban has been told not to move any camera shots, because the VO is being re-recorded to the
*existing* timings. So the new scripts must fit the current timelines:

| Stage | Scene | Timeline | Speech budget | ≈ words @ 2.3 w/s |
|---|---|---|---|---|
| 1 | `Stage1_StoryMode` | 43 s | ~36 s | **~83** |
| — | `Transport_StoryMode` | 13 s | ~10 s | **~23** |
| 2 | `Stage2_StoryMode` | 32 s | ~27 s | **~62** |
| 3 | `Stage3_StoryMode` | 84 s | ~72 s | **~165** |
| 4 | `Stage4_V2` | 86 s | ~72 s | **~165** |

Pacing target ~2.3 words/sec with a **1 second pause at every camera change**. Audio starts at
**1.0 s**, not 0.0 s, so the establishing shot lands before the first line.

---

## Stage 4 — full rewritten script ✅ shot-aligned, ready to record

This one is exact: it's written against the real 14-shot cut list in
`docs/stage4-v2-vo-script.md`, same shots, same budgets, numbers removed.

| # | Shot | Cut at | Budget | Line |
|---|---|---|---|---|
| 00 | Establish | 0.0 s | 1.0–4.5 s | Everything leaving the kiln arrives as one stream. |
| 01 | HoodTwoPaths | 5.4 s | 6.4–13.0 s | At working heat the hood splits it: solids fall, vapour rises. Nothing burns — there's no oxygen. |
| 02 | FibersDrop | 13.6 s | 14.6–20.0 s | What falls is glass fibre and carbon char, still glowing, still bound together. |
| 03 | ConveyorRun | 20.4 s | 21.4–26.4 s | A water-jacketed screw cools them under nitrogen, from kiln heat down to handling temperature. |
| 04 | Airlock | 26.8 s | 27.8–31.2 s | A rotary valve lets them out without letting air in. |
| 04b | ElutriatorFeed | 31.6 s | 32.6–36.8 s | Now air does the sorting. The mixture is lifted into a classifier. |
| 04c | FibreToBox | 37.2 s | 38.2–43.0 s | Glass fibre is the heavier fraction. It drops out first — and it's the product this order is for. |
| 04d | CharToDrums | 43.4 s | 44.4–49.2 s | The lighter char is carried on and drummed off. Watch how much: the coarser the feed, the more of it there is. |
| 05 | VaporHighRoad | 49.6 s | 50.6–53.2 s | Meanwhile, the vapour takes the high road. |
| 06 | HeatExchanger | 53.6 s | 54.6–59.4 s | First a heat exchanger, which gives its heat to the kiln's burner air. |
| 07 | Cyclone | 59.8 s | 60.8–66.8 s | Then a cyclone spins the last of that dust back out of the gas. |
| 08 | Condenser | 67.2 s | 68.2–72.0 s | Cooled in the condenser, the heavy fractions rain out. |
| 09 | OilAndSyngas | 72.4 s | 73.4–78.4 s | Liquid settles as pyrolysis oil. What stays gas is syngas. |
| 10 | ClosingWide | 78.8 s | 79.8–85.6 s | And the syngas goes back to fire the kiln. The plant heats itself. |

**Changes from v1:** four numbers removed (600 °C, 50 °C, 70 %, 6 %, 16 %, 8 %); shot 04c now
names the order; shot 04d actively directs attention to the char pile, which is the visual Anirban
is making respond to particle size. That line does real work — it tells the viewer where to look
for the difference between presets.

### Plain text for the TTS tool

Insert **1 second of silence** at each line break.

```
Everything leaving the kiln arrives as one stream.

At working heat the hood splits it: solids fall, vapour rises. Nothing burns — there's no oxygen.

What falls is glass fibre and carbon char, still glowing, still bound together.

A water-jacketed screw cools them under nitrogen, from kiln heat down to handling temperature.

A rotary valve lets them out without letting air in.

Now air does the sorting. The mixture is lifted into a classifier.

Glass fibre is the heavier fraction. It drops out first — and it's the product this order is for.

The lighter char is carried on and drummed off. Watch how much: the coarser the feed, the more of it there is.

Meanwhile, the vapour takes the high road.

First a heat exchanger, which gives its heat to the kiln's burner air.

Then a cyclone spins the last of that dust back out of the gas.

Cooled in the condenser, the heavy fractions rain out.

Liquid settles as pyrolysis oil. What stays gas is syngas.

And the syngas goes back to fire the kiln. The plant heats itself.
```

---

## Stages 1–3 — drafts ⚠️ need aligning to your shot cuts

There's no written cut list for these the way there is for Stage 4, so these are drafts written to
the total word budget, not to individual shots. **Play each stage, note where the camera cuts, and
redistribute the lines.** The word counts are right; the line breaks may not be.

### Stage 1 — Wind farm · ~83 words

```
These blades have reached the end of their service life.

They are not waste. They are roughly sixty percent glass fibre by mass — a material
worth recovering, if you can get it out cleanly.

That "cleanly" is the whole problem, and it's what this plant is built to solve.

Somewhere, a customer needs recovered fibre at a particular grade. That order is
what decides how much material we take, and how we treat it.

This is where it starts.
```

*Note: "roughly sixty percent glass fibre by mass" is a property of blades, not of our process,
so it's safe to keep. Anjani and Hari can confirm the figure.*

### Transport · ~23 words

```
Cut on site, moved by road, and delivered to the plant gate.

From here on, every decision is about grade.
```

### Stage 2 — Shredding · ~62 words

```
Before anything can be heated, it has to be broken down.

Shears and shredders reduce the blade sections to a feedstock of a chosen size.

That size is not a detail. It's the single most influential setting in the whole
plant. Finer material heats evenly and breaks down completely. Coarser material
keeps a cold core — and a cold core means char instead of clean fibre.

Look at the output. That size was chosen by this order.
```

*This is the most important rewrite in the set. Stage 2 was previously a transitional beat; it now
carries the argument, because particle size is the heaviest-weighted input in the model.*

### Stage 3 — Kiln · ~165 words

Currently two audio files (`Stage3_Airlock_VO` ~27 s, `Stage3_Kiln_VO` ~50 s). Keep that split.

**Airlock section (~27 s, ~62 words):**

```
The feedstock enters through an airlock.

Two doors, never open at once. Between them, the air is purged and replaced with nitrogen.

This matters more than it looks. If oxygen reaches the material at temperature, it burns,
and burning destroys the fibre we came for. Everything past this point happens without air.
```

**Kiln section (~50 s, ~103 words):**

```
Inside the rotary kiln, the material is heated indirectly — no flame ever touches it.

The resin holding the composite together breaks down and leaves as vapour. The glass fibre
does not. It stays behind, and it stays whole, if the conditions are right.

Two settings decide that. How hot, and how long.

Too cool, or too quick, and the resin doesn't fully decompose — carbon stays on the fibre
and the product comes out darker and weaker. Hotter and slower gives a cleaner separation,
but the plant handles less material per hour.

That trade — throughput against quality — is the decision this order has already made.
```

---

## Recording checklist

- [ ] Stage 4 script recorded (exact, ready now)
- [ ] Stages 1–3 line breaks aligned to actual camera cuts before recording
- [ ] Every line re-read for stray absolute numbers
- [ ] One-second silence between lines, so cue timings can be derived by silence detection
- [ ] Same voice and pacing as the current files
- [ ] Files replaced in `Assets/Audio/` under the existing names, so nothing has to be re-linked
- [ ] Akshat notified — he derives subtitle cue files from the new audio

---

## Which files this replaces

| Current file | Length | Replaced by |
|---|---|---|
| `Segment1_VO` | 41.3 s | Stage 1 above |
| `Segment2_Transport_VO` | 9.8 s | Transport above |
| `Stage2_Shredder_VO` | 29.7 s | Stage 2 above |
| `Stage3_Airlock_VO` | 27.1 s | Stage 3, airlock section |
| `Stage3_Kiln_VO` | 50.2 s | Stage 3, kiln section |
| `Stage4_V2_VO` | 74.8 s | Stage 4 above |

`Stage4_Hood_VO` (55.7 s) is already superseded and unused — ignore it.

**Keep the current files** until the new ones are in and verified. Rename rather than delete.
