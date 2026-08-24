# Stage 4 (V2) — Voice-over script

**Scene:** `Stage4_V2.unity` · **Timeline:** `Stage4_V2_Timeline` · **Length:** 86.00 s · **14 shots**

Written to the *current* camera cuts — no camera or timing changes required.
Pacing target ~2.3 words/sec, with a **1 s pause at every camera change** (13 pauses).
Total speech ≈ 72 s of the 86 s timeline (~85%), against ~92–96% in Stages 1–3.

Audio should **start at 1.0 s**, not 0.0 s, so the establishing shot lands before the first line.

---

## Timed script

| # | Shot | Cut at | Speech budget | Line |
|---|---|---|---|---|
| 00 | Establish | 0.0 s | 1.0–4.5 s | Everything leaving the kiln arrives as one stream. |
| 01 | HoodTwoPaths | 5.4 s | 6.4–13.0 s | At six hundred degrees the hood splits it: solids fall, vapour rises. Nothing burns — no oxygen. |
| 02 | FibersDrop | 13.6 s | 14.6–20.0 s | What falls is glass fibre and carbon char, still glowing, still bound together. |
| 03 | ConveyorRun | 20.4 s | 21.4–26.4 s | A water-jacketed screw cools them from six hundred degrees to fifty, under nitrogen. |
| 04 | Airlock | 26.8 s | 27.8–31.2 s | A rotary valve lets them out without letting air in. |
| 04b | ElutriatorFeed | 31.6 s | 32.6–36.8 s | Now air does the sorting. The mixture is lifted into a classifier. |
| 04c | FibreToBox | 37.2 s | 38.2–43.0 s | Glass fibre is the heavier fraction. It drops out first: seventy percent. |
| 04d | CharToDrums | 43.4 s | 44.4–49.2 s | The lighter char is carried on and drummed off — six percent. |
| 05 | VaporHighRoad | 49.6 s | 50.6–53.2 s | Meanwhile, the vapour takes the high road. |
| 06 | HeatExchanger | 53.6 s | 54.6–59.4 s | First a heat exchanger, which gives its heat to the kiln's burner air. |
| 07 | Cyclone | 59.8 s | 60.8–66.8 s | Then a cyclone spins the last of that dust back out of the gas. |
| 08 | Condenser | 67.2 s | 68.2–72.0 s | Cooled to forty-five degrees, the heavy fractions rain out. |
| 09 | OilAndSyngas | 72.4 s | 73.4–78.4 s | Liquid settles as pyrolysis oil — sixteen percent. What stays gas is syngas. |
| 10 | ClosingWide | 78.8 s | 79.8–85.6 s | Eight percent, and it goes back to fire the kiln. The plant heats itself. |

---

## Plain text (for the TTS tool)

Insert a **1 second silence** at each line break.

```
Everything leaving the kiln arrives as one stream.

At six hundred degrees the hood splits it: solids fall, vapour rises. Nothing burns — no oxygen.

What falls is glass fibre and carbon char, still glowing, still bound together.

A water-jacketed screw cools them from six hundred degrees to fifty, under nitrogen.

A rotary valve lets them out without letting air in.

Now air does the sorting. The mixture is lifted into a classifier.

Glass fibre is the heavier fraction. It drops out first: seventy percent.

The lighter char is carried on and drummed off — six percent.

Meanwhile, the vapour takes the high road.

First a heat exchanger, which gives its heat to the kiln's burner air.

Then a cyclone spins the last of that dust back out of the gas.

Cooled to forty-five degrees, the heavy fractions rain out.

Liquid settles as pyrolysis oil — sixteen percent. What stays gas is syngas.

Eight percent, and it goes back to fire the kiln. The plant heats itself.
```

**165 words.** If the render comes out longer than ~85 s, the shots to trim are 01 and 07 — they have the most slack.

---

## Every figure, and where it comes from

| Claim | Source |
|---|---|
| 600 °C hood | `L_Hood` — "DISCHARGE HOOD · 600 °C" |
| no oxygen / nothing burns | anoxic pyrolysis; hood N₂ purge +50 to +100 Pa (spec) |
| 600 → 50 °C, nitrogen | `L_Conveyor` — "WATER-JACKETED SCREW / N2 PURGED · 600→50 °C" |
| rotary valve, no air in | `L_Airlock` — "ROTARY AIRLOCK VALVE" |
| air classifier sorts by weight | elutriation; glass terminal velocity 0.0368 m/s vs char 0.0032 m/s, fluidising 0.015 m/s (spec) |
| glass fibre 70% | `L_Fiber` — "RECLAIMED GLASS FIBRE · 70%" |
| carbon char 6% | `L_Char` — "CARBON CHAR · 6%" |
| heat exchanger preheats burner air | `L_HX` |
| cyclone removes char dust | `L_Cyclone` + diagram "Fine Char Dust" |
| 45 °C condenser | `L_Condenser` — "RAIN CHAMBER · 45 °C" |
| pyrolysis oil 16% | `L_Oil` |
| syngas 8% → kiln burners | `L_Syngas` |

Char is quoted **once** (shot 04d). Shot 07 says "the last of that dust" rather than a second
percentage, because the cyclone recovers the same char stream — quoting 6% twice would read as
double-counting.

---

## What happens after you record it

1. Drop the MP3 into `Assets/Audio/` (suggested name `Stage4_V2_VO.mp3`, so the old
   `Stage4_Hood_VO.mp3` stays untouched for the archived Stage 4).
2. I swap the clip on the existing `S4V2_VO` audio track — same track, same binding — and set the
   clip start to 1.0 s.
3. I verify against the shot table above and report any line that overruns its cut.

Nothing in the scene changes until the file exists. The old `Stage4_Hood_VO.mp3` is not deleted.
