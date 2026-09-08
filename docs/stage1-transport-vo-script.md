# VO script — Stage 1 and Transport

*Written 5 September, branch `feat/Stage1-2_VO`. Replaces the Stage 1 and Transport
entries in `narration-scripts-v2.md`.*

---

## Two files, not one

| file | scene | scene length | VO window | current clip |
|---|---|---|---|---|
| `Segment1_VO.mp3` | `Stage1_StoryMode` | 43.0 s | 0.5 – 41.78 s | 41.28 s |
| `Segment2_Transport_VO.mp3` | `Transport_StoryMode` | 13.0 s | 0.5 – 10.3 s | 9.8 s |

They cannot be one file. `TourSceneSequencer` loads every stage with
`LoadSceneMode.Single`, which destroys the previous scene and every AudioSource in
it — a clip spanning the cut would be chopped mid-word. On top of that, **Skip
Intro** jumps Stage 1 → Shredding and never plays Transport at all, and **Next
Stage** can end Stage 1 at any moment. Per-scene audio survives both.

**Keep the new takes at or under the lengths above.** The timeline clip on
`S1_VO_Track` is authored at 41.28 s. A longer file is truncated; a shorter one
leaves the clip padded with silence. Either way the clip needs re-trimming in the
timeline, which is a scene edit.

---

## The rules these are written to

**No spoken numbers, ever.** Not one. The panel carries every value and updates
itself; audio does not. A custom order can ask for 50 kg or 20,000 t — that is
0 blades to 2,567 blades and 1 minute to 186 days. No recording covers that range,
so the narration states *relationships* and lets the panel state *values*.

**The narrator is a colleague on site**, not a prospectus. Shows you the thing,
lets the consequence land, never sells.

**Stage 1's job** is the conversion a planner cares about first: an order becomes a
physical quantity of blade. It is the only stage that presents no choice — it
presents the constant.

Pace: **~115 wpm**, matching the existing takes. Slower than conversation, which is
right for four cuts with pauses.

---

## Stage 1 — wind farm · 77 words

Cut points are the real ones from `Timelines/Stage1_Timeline.playable`.

### Shot 1 · `CAM_S1_01_WideAerial` · speak 0.5 – 12.0 s · 23 words

> Every order starts here, as a number of blades. Not tonnes on a page — actual
> blades, standing in a field, finished.

### Shot 2 · `CAM_S1_02_RedCluster` · speak 12.5 – 27.5 s · 29 words

> About sixty percent of a blade is glass fibre. The rest is cured resin, built to
> hold it forever. The fibre you sell is never the tonnage you take.

*The longest shot at 17 s, so it carries the conversion — why an order for 4,800 t
of fibre needs 6,962 t of blade.*

### Shot 3 · `CAM_S1_03_DismantlingCloseup` · speak 28.5 – 36.5 s · 15 words

> Cut them down. The count on the right is what your order costs in blades.

*Points at the panel rather than reciting it.*

### Shot 4 · `CAM_S1_04_TruckLeaving` · speak 37.0 – 41.5 s · 10 words

> How clean they come back out is a different decision.

*Separates the quantity decision from the quality decision, and hands off to
Transport.*

---

## Transport — 19 words

A 13 s pass-through with no chapter number. Nothing is decided here and the script
should not pretend otherwise.

### speak 0.5 – 10.3 s

> Cut on site, loaded, and hauled to the plant gate. Everything so far is haulage.
> The decisions start inside.

*Deliberately not the panel's wording. The panel says "Nothing is decided on the
road. The first real choice comes at the shredder." Saying the same sentence twice,
once in each channel, reads as a bug.*

---

## Subtitles

`SubtitleTrack.cs` is fully built and has **never had a cue file** — the
`[SubtitleTrack] No cue file assigned` warning in the console is that.

Cue files are written and ready to assign:

- `Assets/Narration/Stage1_Cues.txt`
- `Assets/Narration/Transport_Cues.txt`

Assign each to the `cueFile` field on the scene's `SubtitleTrack`. That is a scene
edit, so it belongs in the same pass as any timeline re-trim.

Subtitles matter more than usual here. Because the VO deliberately names no
values, the text channel is how a viewer follows the thread — it is part of the
argument, not an accessibility extra.

---

## What is NOT in these scripts, and why

- **No blade or turbine count.** Varies by order; the panel has it.
- **No days.** Varies by order and by grade; the panel has it.
- **No "the farm is the same every run."** True for the three presets, false the
  moment anyone uses Custom Order with a different tonnage.
- **No mention of grade.** Nothing has been decided yet at Stage 1. The grade
  argument belongs at Shredding, where the first real choice is made.
