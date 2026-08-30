# CEE Brief — Answers (Anjani & Hari)

For: Ritwika · From: Anjani Lohith Kosana, Hari Krishna Kondam · 30 Aug 2026

---

## 1. Grade thresholds

**Approach: sound, keep it.** Grading recovered fibre by quality and routing different
grades to different end markets is exactly how the industry already talks about this
material — there's no single global spec, but every source we found (cement
co-processing literature, mechanical-recycling-into-concrete studies, pyrolysis fibre
studies) treats "high-fidelity fibre for structural reuse" vs. "degraded material for
filler/fuel" as the real-world split. So the *shape* of your tiering is right.

**The specific numbers (≥95/≥95, ≥85/≥80): no published standard found.**
We looked for something like PAS 101 (which exists for container cullet glass, UK) but
for *recovered composite glass fibre* specifically — nothing comparable exists. Purity
and tensile-retention thresholds for tiering recyclate are not standardized anywhere we
could find. Two research groups independently label their own pyrolysis output as
"high quality" fibre at retention values in the 72–93% range depending on process, with
no agreed cutoff. **Keep your numbers, but label them explicitly as project
assumptions** — that's the honest and defensible answer, and it's consistent with what
the actual literature does (every paper defines its own bar).

One adjustment worth considering: your mid-tier boundary (≥85% purity / ≥80% tensile)
sits close to real reported pyrolysis results — recovered fibre has been measured at
76% tensile / 88% modulus retention for wind-blade epoxy composite specifically (see
data point below), and separately at 72–93% tensile retention across other pyrolysis
studies. That range straddles your low/mid line, which is reasonable — it means your
mid tier represents genuinely achievable pyrolysis output, not a fictional middle
ground.

- Source (the 76%/88% data point you already had): a two-step pyrolysis study
  (425°C pyrolysis + 475°C oxidation) on wind-blade epoxy GFRP reported clean white
  fibres retaining 76% tensile strength and 88% modulus vs. virgin fibre.
- Broader range: pyrolysis-recovered fibres reported elsewhere at up to ~90-93% tensile
  retention under optimal conditions, and as low as ~72% for aged real-world blade
  waste processed by simpler methods — so your tier spread is realistic, not
  cherry-picked.

## 2. Average blade mass

**Recommended value: ~11–12 tonnes per blade.**

- Assumes: a blade from the ~2 MW class turbine, which is representative of the
  generation now actually being decommissioned in Europe (installed roughly
  2000s–2010s, now reaching 20-year end-of-life / repowering age). A commercial 2 MW
  blade (LM Wind Power's 56.8 m design) weighs 11.3 t.
- Range to cite alongside it: industry rule-of-thumb figures put blade mass at
  roughly 10–14 tonnes per MW of turbine rating, so a 1.5 MW blade lands nearer 5-8 t
  and a 3 MW blade nearer 12-15 t. 11.3 t for a 2 MW-class blade is a reasonable
  single defensible figure sitting in the middle of that range.
- Source: LM Wind Power product spec (LM 56.8 P, 2 MW class, 11.3 t); cross-checked
  against the widely-cited 10 t/MW rule of thumb and Liu & Barlow (2017) mass-per-MW
  estimates used in DOE/NREL blade-waste inventory work.

State this in the app as: *"Assumes an average 2 MW-class blade at ~11 tonnes — actual
blade mass varies significantly by turbine size and generation."*

## 3. End use per tier

- **High grade** — Fibre clean and strong enough to go back into new composite
  parts: reinforcement in new panels, structural laminates, or other manufactured
  composite products, essentially standing in for virgin glass fibre.
- **Mid grade** — Not clean enough for structural reuse, but strong enough to serve
  as a reinforcing filler mixed into precast concrete or cast products — several
  real recycling operations (e.g. Iowa-based Regen Fiber) already sell shredded blade
  fibre into precisely this market, for slabs, pavement and precast panels.
- **Low grade** — Coarse, mixed material that's co-processed in cement kilns: the
  glass content substitutes for raw silica/sand in the cement mix while the organic
  resin content burns as kiln fuel, replacing coal. This is currently the most
  commercially mature end-of-life route for blade material at scale.

## 4. Sanity check

**26.5% char at 16 mm / 550°C — plausible, not too high.** At coarser particle size
and lower temperature, heat and pyrolysis gases can't penetrate the material as
completely in the same retention time, so more of the resin carbonises in place
instead of fully devolatilising into oil/syngas. A char fraction rising into the
mid-20s% under a deliberately under-driven (large particle, lower temp) case is
consistent with what the incomplete-pyrolysis literature describes — this is a
real, well-documented failure mode, not a modeling artifact.

**Loss stream: 1.5% at optimum, capped at 10% — reasonable.** Fugitive dust losses
past a baghouse filter, moisture flashing off, and residue staying stuck to
equipment surfaces easily account for 1-2% under well-controlled conditions, and a
worse-case ceiling around 10% for a badly-run, high-throughput, coarse-particle case
is a sensible outer bound — real particulate-handling systems commonly lose
single-digit percentages even before accounting for a "everything going wrong"
scenario. Nothing here jumps out as physically wrong.

---

### Sources
- CompositesWorld — cement co-processing of decommissioned wind blades
- ScienceDirect — "Co-processing of end-of-life wind turbine blades in portland
  cement production"
- PACA Web — "Wind Turbine Blade Recycling for the Concrete Industry" (Regen Fiber)
- ResearchGate — two-step pyrolysis (425°C/475°C) wind-blade GFRP fibre recovery,
  76% tensile / 88% modulus retention
- OSTI — "Recycling of Commercial E-glass Reinforced Thermoset Composites via Two
  Temperature Step Pyrolysis" (wind blade + SMC feedstock)
- CompositesWorld — "Defining the landscape for wind blades at end of service life"
  (10-14 t/MW mass estimates)
- LM Wind Power — LM 56.8 P blade spec (2 MW class, 11.3 t)
- letsrecycle.com — PAS 101 (confirms no equivalent standard exists for recovered
  composite glass fibre grading)
