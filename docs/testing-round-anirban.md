# Testing round — Anirban

Played all five stages against `main` (after pulling), composite-manufacturer and cement-works
presets, plus free play. Seven findings. **Three I fixed, four are yours or Sharan's** and I have
left them alone.

---

## Fixed

### 1. Floating yellow object in Transport — `Beacon`

A yellow marker hovering just ahead of the truck cab, visible for the whole drive.

`TR_BladeTruck/LoadMarkers/Beacon` at world (−38.45, 2.78, 0). The truck's own bounds are
x −49.5 … −38.34, y 0.05 … 3.53 — so it sat just past the nose, in mid-air. It was a load-placement
helper of mine that should never have shipped. **Removed; scene saved.** 93-line diff, nothing else
in `Transport_StoryMode.unity` touched.

### 2. `KilnOrderBinding` was overwriting your new kiln heat

Already pushed as `2993def`, but worth stating plainly since it explains a lot of the Stage 3
behaviour:

`KilnOrderBinding` runs at `[DefaultExecutionOrder(60)]`, i.e. **after** `TemperatureRampAnimator.Start`,
and it was writing `tempEnd`, `hotColor`, `hotIntensity`, `coolIntensity` and `shellHeatStrength` —
every field your `ApplyOrder()` had just set. It also carried `rampStartTime = 12` / `rampEndTime = 28`,
hard-coded when Stage 3 ran 84 s. After the retime to 33.6 s the kiln is on screen from 0–19 s, so
those numbers held the drum cold through the whole stage, including the 10.2 s line about how hot you
run it.

Temperature and colour now belong entirely to `TemperatureRampAnimator`. `KilnOrderBinding` keeps only
retention and feed rate. Your 9.5 / 18.5 ramp times survive.

> Same class of fault still live in `AirlockDoorCycle`: its phase boundaries are hard-coded at
> t = 3.0, 4.0 and 4.6 s rather than fractions of `cycleLength`. It survives the current retime but
> will break on the next one.

### 3. The Stage 3 kiln reads blue-grey, not hot

This is the one Anirban flagged, and it is not the temperature binding failing — the binding is
working. **Two materials are authored wrong** and between them they cover almost the whole kiln:

| material | base colour | metallic | maps |
|---|---|---|---|
| `S3_Mat_ShroudSteel` | (0.506, 0.517, **0.547**) | **0.78** | none at all |
| `S3_Mat_KilnShell` | (0.520, 0.525, **0.535**) | **0.76** | normal map only |

Both fail the same two ways:

1. **The base colours are blue-biased** — `b > g > r` in both. Steel is warm; painted plant cladding
   warmer still. They are cold before a single light touches them.
2. **metallic ≈ 0.77 with no mask makes both near-mirrors**, and what they have to reflect is
   `Sky_Stage1_RealHDRI` plus fog at (0.71, 0.78, 0.87). A mirror in a dark hall under a blue sky
   returns blue. Worse — the hotter the kiln gets, the harder that cold reflection fights the emission
   your ramp is adding. The material was cancelling the one thing the stage is about.

Neither has a base map, so the kiln is also ~9 m of flat untextured colour. That is why its rotation
is invisible: no surface feature to rotate. Giving the shell a texture makes `KilnRotator` legible for
the first time, which matters because rotation is how retention time reads.

**Fix: `Assets/Scripts/KilnShellGrade.cs`.** Attaches a generated base map and metallic/smoothness mask
and warms the tint. Constraints kept:

- **No scene edit.** Spawns via `RuntimeInitializeOnLoadMethod` and finds targets by material name —
  the `TourControls` / `OrderPanel` pattern. `Stage3_StoryMode.unity` is byte-identical to HEAD.
- **No shared asset edit.** Uses `Renderer.material` (per-renderer instance). Both `.mat` files verified
  content-identical to HEAD. A `MaterialPropertyBlock` could not be used: it cannot enable
  `_METALLICSPECGLOSSMAP`, which is a material keyword, and without that keyword URP ignores the mask
  and keeps the uniform 0.77 metallic — which is the bug.
- **Emission untouched.** `TemperatureRampAnimator` owns `_EmissionColor`. This writes a disjoint set
  of properties on the same instance, so the two cannot collide. I am not repeating finding 2.

> **One judgement call for you.** This runs with *and* without an order, which is an exception to
> "guard every change with `if (OrderContext.HasOrder)`". That rule exists so order-driven *behaviour*
> cannot leak into free play; this is not behaviour, it is two materials authored wrong that look wrong
> identically in both modes. Guarding it would leave free play blue and the tour steel, which is worse
> than either. `onlyWithOrder` on the component flips it to the letter of the rule if you disagree.

---

## Yours — I have not touched these

### 4. Shot 11 `vCam_S3_11_GlassFibersClose` has no subject

Anirban's screenshot of "a bad camera angle that shows nothing" is this shot. It is **4.6 s** of the
back end of the drum and a wall.

- camera (−5.6, 3.4, 4.4), look target (−6.9, 1.8, 0), **4.9 m** out
- within 2.2 m of that target: `S3_Shroud_EndRing_1`, `S3_Zone_3_Ring`, four burner bodies, three
  `FlameCore`s. **No glass fibre of any kind.**

The reason is that **every glass-fibre, char and gas object in Stage 3 is `active = false`** — all of
`S3_Inside_GlassFiber_*`, `FiberClump_*`, `FiberPile_Main`, `S3_OutputGlass_*`, `S3_OutputChar_*`,
`S3_OutputGas_*`, `S3_Cutaway_GlassFibers`, `Interior_GlassFiberStreaks`. Nothing turns them on:
`Stage3_Timeline` has **no activation track at all** (1 Cinemachine, 2 audio, 4 animation), and no
script `SetActive`s them.

And they should stay off. I enabled them temporarily and re-rendered: they fill the entire frame with a
flat beige blob. The geometry is broken — objects meant to be centimetre-scale fibres report renderer
bounds of 20–40 m. Someone disabled them for a good reason.

So the shot is orphaned: it was framed on a subject that was later switched off. **Your call** — cut it,
or re-aim it. If re-aiming, the burner row at (−6.9, 0.9, 0) is real, lit, and actually on; the other
honest option is giving those 4.6 s to shot 10 `CutawayInterior`, which currently gets 5.3 s.

I have not moved the camera or the look target, per *"Ritwika owns re-cutting; if a shot needs to move,
that is her call, not yours."*

> Related, low priority: `TemperatureRampAnimator.HeatPrefixes` includes `S3_Cutaway_FeedBed`,
> `S3_Cutaway_RefractoryLining`, `S3_Cutaway_InnerGlowSurface` and `Interior_RefractoryGlow`, and the
> sweep uses `FindObjectsInactive.Include` — so it is scaling emission on objects that never turn on.
> Harmless, just wasted work.

### 5. Stage 4 VO vs labels

Still open from the last PR. Narration says "six percent" carbon char; a cement-works run produces 27%
and the label now says so. Same for 70 / 16 / 8. Those figures were only ever right for the composite
run — they were already wrong for the other two, it just wasn't visible while the labels were wrong in
the same way. Cheapest correct fix is dropping the numbers from the Stage 4 VO so one recording serves
all three grades.

---

## Sharan's — Space behaves differently in every stage

Anirban hit this twice and it reads as three separate bugs to a player.

| scene | Space does |
|---|---|
| Transport | **nothing** — no explore rig present at all |
| Stage 3 | jumps to an arbitrary orbit position with a bad angle |
| Stage 4 | works as expected |

Transport has none of the three explore components. Stage 3 has them but the orbit entry point is
unset, so it lands wherever the rig's default is. Diagnosed only — Explore mode is Sharan's item and I
would rather not duplicate someone's work twice in one week.

---

## Still to do on my side

- World-space label typography (Anirban's fourth screenshot). Overlaps your "Stage 4 — polishing",
  so tell me if you would rather own it.
- Three end-to-end preset runs for the Definition of Done. `KilnShellGrade` has been verified by
  render, **not yet in a live play session** — edit-mode renders do not tick animators, and that has
  produced two false readings for me this week.
