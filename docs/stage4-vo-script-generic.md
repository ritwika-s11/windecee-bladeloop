# Stage 4 — Voice-over script (generic)

**Supersedes `stage4-v2-vo-script.md`, which must not be recorded.** That version quotes
70 % fibre, 6 % char, 16 % oil, 8 % syngas and "six hundred degrees". Those are design-case
values. `ProcessModel.OutputSplit()` moves them with the planner's settings:

```
glass  = 70 − 26·D − 12·DevParticle   clamped 30..72
char   =  6 + 24·D + 12·DevParticle   clamped  6..42
oil    = 16 −  6·D                    clamped  4..17
syngas =  8 −  2·D                    clamped  3..9
```

On a poorly-set run the old narration claims 70 % fibre while the panel beside it reads 30 %,
and 6 % char against an actual 42 %. Stage 4 is the most order-dependent stage in the tour,
because it is where the outputs land — so it is the one stage whose script may never carry a
number.

**Scene:** `Stage4_V2.unity` · **Timeline:** `Stage4_V2_Timeline`
**New length:** ~65 s (from 86.00 s) · **9 shots** (from 14)

---

## The division of labour

The panel quantifies; the narration explains. The viewer can already read the fibre and char
percentages off `Stage4OrderBinding` — saying them aloud adds nothing and can only ever
contradict them. What the narration adds is *why the numbers came out that way*, and *where
everything goes afterwards*, neither of which the panel can say.

That is what makes Stage 4 the payoff of the planner story: this is where "see what you made"
actually happens.

---

## Timed script

163 words · ~60.5 s of speech at 2.4 words/sec · 0.4 s between lines.

| # | Shot | Line |
|---|---|---|
| 1 | Establish | Everything the kiln made leaves together — still hot, still mixed. |
| 2 | HoodTwoPaths | The first sorting needs no machine. Solids are too heavy to rise, so the vapour climbs away without them. |
| 3 | ConveyorRun | The solids ride out on a cooled screw under nitrogen — air here would undo the kiln's work. |
| 4 | ElutriatorFeed | Then air does the sorting: fibre is dense and drops, char is light enough to carry off. |
| 5 | FibreToBox | The fibre is what the order was for — cleaned of resin, weighed, boxed. |
| 6 | CharToDrums | Char is the tell: the more of it, the more resin the kiln never finished. |
| 7 | Condenser | The vapour cools as it travels, until the heavy part rains out as oil. |
| 8 | OilAndSyngas | None of it is thrown away. The gas returns to fire the kiln; the oil is sold on as fuel. |
| 9 | ClosingWide | The char goes to a cement works: the carbon burns, the glass becomes clinker. How you set the plant decided every share. |

Line 6 earns its place by handing the planner a reading skill: char rising *is* the visible
signal that the kiln under-converted. True at every setpoint, which is why it can be spoken.

Lines 8–9 are the new material — the fate of the three by-products. They also close the loop
back to the project's own title: cement kiln **co-processing** is the disposal route for the
char, and it is the reason nothing leaves the plant as waste.

---

## Where the by-product claims come from

| Claim | Basis |
|---|---|
| Syngas returns to fire the kiln | Syngas is used as fuel to power the pyrolysis process itself |
| Oil sold on as fuel / feedstock | Pyrolysis oil is used for energy production and as industrial feedstock; the tar fraction is rich in phenolics |
| Char to a cement works | In cement co-processing the carbonaceous fraction substitutes for fuel and the inert glass is incorporated into clinker as a raw material |
| "Nothing is thrown away" | Pyrolysis + cement kiln co-processing is currently the highest-potential commercial route for end-of-life blades |

Sources are listed at the foot of this file. Note the script says the char's **glass becomes
clinker**, not that it is recycled as fibre — the char fraction's glass is contaminated and
goes in as raw meal, which is a different claim from the clean fibre in shot 5.

---

## Plain text (for the TTS tool)

Insert a **0.4 second silence** at each line break. Do not pad the ends.

```
Everything the kiln made leaves together — still hot, still mixed.

The first sorting needs no machine. Solids are too heavy to rise, so the vapour climbs away without them.

The solids ride out on a cooled screw under nitrogen — air here would undo the kiln's work.

Then air does the sorting: fibre is dense and drops, char is light enough to carry off.

The fibre is what the order was for — cleaned of resin, weighed, boxed.

Char is the tell: the more of it, the more resin the kiln never finished.

The vapour cools as it travels, until the heavy part rains out as oil.

None of it is thrown away. The gas returns to fire the kiln; the oil is sold on as fuel.

The char goes to a cement works: the carbon burns, the glass becomes clinker. How you set the plant decided every share.
```

### If 65 s is too long

Drop lines **3** and **7**. They are the two pure-plumbing lines — they describe hardware that
behaves identically at every setpoint, so they teach the planner nothing. Removing them takes
the script to ~48 s and 7 shots, and the narration still flows: shot 4's "then air does the
sorting" follows shot 2's split without a gap in logic.

Do **not** drop 8 or 9 to save time. They are the answer to "what happens to the rest of it",
which is the question the stage exists to answer.

---

## Shot plan

Five shots are cut: `02_FibersDrop`, `04_Airlock`, `05_VaporHighRoad`, `06_HeatExchanger`,
`07_Cyclone`. Their cameras stay in the scene for free play and the plant explorer — only
their Timeline clips go.

| Shot | Start | Dur | End |
|---|---|---|---|
| S4V2_00_Establish | 0.0 | 5.2 | 5.2 |
| S4V2_01_HoodTwoPaths | 5.2 | 8.3 | 13.5 |
| S4V2_03_ConveyorRun | 13.5 | 7.9 | 21.4 |
| S4V2_04b_ElutriatorFeed | 21.4 | 7.9 | 29.3 |
| S4V2_04c_FibreToBox | 29.3 | 6.2 | 35.5 |
| S4V2_04d_CharToDrums | 35.5 | 7.1 | 42.6 |
| S4V2_08_Condenser | 42.6 | 6.6 | 49.2 |
| S4V2_09_OilAndSyngas | 49.2 | 8.7 | 57.9 |
| S4V2_10_ClosingWide | 57.9 | 7.0 | 64.9 |

These are estimates from the word counts. Actual starts get measured off the recording with
ffmpeg silence detection, as in Stages 1–3 — the table is a target, not a commitment.

---

## Known traps for the retime

1. **`CutawayTimelineTrigger.showAtTime = 48.8`** on one instance. Inside a 65 s timeline that
   still fires, but it now lands on a different shot than it was authored against, so it must
   be re-checked. The other instance is at 5.4. At the 48 s variant it would fall off the end
   entirely — the same failure as Stage 1's truck and Stage 3's temperature ramp.
2. **`TourSceneSequencer.sceneDurations`** in `FullPlantTour` still lists `86` for Stage 4.
   Inert while the scene has a `PlayableDirector`, but it should be corrected.
3. **Animation clips truncate, not compress**, when shortened. Anything whose duration drops
   needs `timeScale` set, or the move ends mid-travel.
4. `Stage4_Hood_VO.mp3` is referenced by nothing and can be deleted.

---

## Sources

- [End-of-life wind turbine blades as a resource: a comparative study of pyrolysis and combustion](https://www.sciencedirect.com/science/article/abs/pii/S0165237025003614)
- [Recovery of styrene-rich oil and glass fibres from end-of-life wind turbine blades using pyrolysis](https://www.sciencedirect.com/science/article/abs/pii/S0165237023002449)
- [Thermal decomposition behaviour of retired wind turbine blades: kinetics and pyrolysis product distribution](https://www.sciencedirect.com/science/article/abs/pii/S0301479725014914)
- [Upcycling of decommissioned wind turbine blades through pyrolysis](https://www.sciencedirect.com/science/article/abs/pii/S0959652622038641)
