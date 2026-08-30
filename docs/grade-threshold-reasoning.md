# Grade Threshold Reality Check — Reasoning & Sources

For: BladeLoop CEE task · Prepared by Anjani Lohith Kosana, Hari Krishna Kondam
Purpose: back-up reasoning for defending Section 3 (grade thresholds) under questioning

---

## The question being answered

Section 3 of the product vision doc sets three grade tiers using two metrics — fibre
purity (%) and tensile retention (%):

| Tier | Fibre purity | Tensile retention |
|---|---|---|
| High | ≥ 95% | ≥ 95% |
| Mid | ≥ 85% | ≥ 80% |
| Low | below that | below that |

These are stated in the doc as **project assumptions, not published standards** — because
no published grading standard for recovered composite glass fibre exists (we checked;
the closest analogue, PAS 101, only covers container cullet glass, not composite-derived
fibre). This document evaluates how close each tier sits to what real recovery processes
actually achieve, so the numbers can be defended honestly rather than by claiming a
standard that doesn't exist.

---

## What real processes actually achieve (tensile retention)

| Process | Feedstock | Tensile retention achieved |
|---|---|---|
| Standard single-step pyrolysis | Real wind-blade waste (semi-wet method) | ~72% |
| Two-step pyrolysis (425°C pyrolysis + 475°C oxidation) | Wind-blade epoxy GFRP | 76% tensile / 88% modulus |
| Single high-temp pyrolysis step (baseline) | Wind blade + automotive SMC | Baseline case, improved on by multi-step process |
| Multi-step pyrolysis (optimized) | Same study, two-temperature-step | Up to 19% improvement in tensile strength over single-step baseline |
| Various pyrolysis studies, best-case optimized conditions | Mixed GFRP sources | Up to ~90–93% under optimal conditions |
| Molten-salt-assisted pyrolysis | Specialized process, not standard thermal recovery | Reported to approach near-virgin performance — but this is a distinct, non-standard technique |

**Real-world spread: roughly 72–93%**, with ordinary single-step pyrolysis clustering in
the low-to-mid 70s, and only carefully optimized multi-step or chemically-assisted
processes reaching the low 90s.

---

## Tier-by-tier assessment

### Low grade (below 80–85%)
**Close to real-world.** This is where most straightforward, single-step pyrolysis
output actually lands (~72–76% in the studies above). Defensible without qualification.

### Mid grade (≥85% purity / ≥80% tensile retention)
**Close to real-world, achievable with process control.** The 80% tensile floor sits
right around where better-controlled two-step processes land — our own sourced data
point (76% tensile / 88% modulus, two-step pyrolysis on wind-blade epoxy) falls just
under this line, which is reassuring: it shows the mid tier represents a real,
demonstrated process outcome, not a hypothetical one.

### High grade (≥95% purity / ≥95% tensile retention)
**Optimistic — sits above nearly all standard thermal recovery results.** The best
tensile retention we found in the literature tops out around 90–93%, and that's under
carefully optimized, often multi-step or specialized conditions — not what a standard
kiln-style thermal process (as modeled in `ProcessModel.cs`) would typically deliver.
A blanket ≥95% threshold describes best-in-class or aspirational performance, not
typical output. It isn't impossible — some specialized techniques (e.g. molten-salt
assisted recovery) claim to approach near-virgin performance — but framing ≥95% as
representative of standard thermal recovery would overstate what's demonstrated.

---

## One important caveat on the comparison itself

"Purity" and "tensile retention" are **different metrics** measuring different things:
purity is broadly about how clean/resin-free the recovered fibre is, while tensile
retention is about how much mechanical strength survives the recovery process. Almost
all the literature we found reports tensile (and sometimes modulus) retention — there
is comparatively little published data expressing recovered fibre quality as a "purity
%" figure the way the app's model does. That means the tensile-retention half of each
threshold can be benchmarked against real data (as above); the purity half largely
cannot be independently verified against outside sources. This is a second, separate
reason the thresholds should stay labeled as project assumptions rather than
literature-derived values.

---

## How to defend this under questioning

1. **Don't claim the numbers are sourced or standardized — they aren't, and no standard
   exists to source them from.** State that directly; it's the correct and defensible
   answer per the CEE brief itself.
2. **Do defend the tiering logic and the low/mid tiers as realistic**, backed by the
   real process data above.
3. **Be upfront that the high-grade bar is aspirational**, not typical — if asked "is
   95% achievable?", the honest answer is "under specialized or heavily optimized
   conditions, approaching that range has been reported; as a routine output of a
   standard thermal process it would be considered strong, not typical."
4. **Flag the purity/tensile distinction** if pressed on why purity isn't independently
   verified — it's a genuine data gap in the literature, not an oversight on your part.

---

## Sources

1. **Recycling of waste glass fiber-reinforced plastics using pyrolysis with KOH**
   (two-step: 425°C pyrolysis + 475°C oxidation, wind-blade epoxy GFRP) — 76% tensile
   strength / 88% modulus retention.
   https://www.researchgate.net/publication/349385829_Recycling_of_waste_glass_fiber-reinforced_plastics_using_pyrolysis_with_KOH

2. **Microwave pyrolysis as a method of recycling glass fibre from used blades of wind
   turbines** — semi-wet method applied to real wind-blade waste, 72% tensile
   retention vs. virgin fibre (single-step baseline lost ~25% of tenacity, i.e. ~75%
   retained, in the same study).
   https://www.researchgate.net/publication/240162064_Microwave_pyrolysis_as_a_method_of_recycling_glass_fibre_from_used_blades_of_wind_turbines

3. **Recycling of Commercial E-glass Reinforced Thermoset Composites via Two
   Temperature Step Pyrolysis to Improve Recovered Fiber Tensile Strength and Failure
   Strain** (OSTI, peer-reviewed; feedstock: wind turbine blades + automotive SMC) —
   two-step process improved tensile strength by up to 19% and strain-to-failure by
   up to 43% over a single high-temperature-step baseline.
   https://www.osti.gov/pages/biblio/1525488-recycling-commercial-glass-reinforced-thermoset-composites-via-two-temperature-step-pyrolysis-improve-recovered-fiber-tensile-strength-failure-strain

4. **Recovery of glass fibers from glass fiber reinforced plastics by pyrolysis**
   (review) — recycled FRPs reported to maintain up to 93% of virgin tensile strength
   under optimal pyrolysis conditions; mechanical recycling can reduce tensile
   properties by up to 29% by comparison.
   https://www.researchgate.net/publication/257485767_Recovery_of_glass_fibers_from_glass_fiber_reinforced_plastics_by_pyrolysis

5. **Molten salt assisted pyrolysis recycling of glass fiber reinforced polymer
   composites** (patent/technology description, University of Tennessee Research
   Foundation) — molten-salt-assisted process claims to recover more of the tensile
   strength and properties of virgin fibre by avoiding strength-damaging effects of
   traditional char removal; treated as a specialized, non-standard process rather
   than typical thermal recovery.
   https://utrf.tennessee.edu/technologies/molten-salt-pyrolysis-recycling-glass-fiber-carbon-composites/

6. **Glass Specifications — letsrecycle.com** (background on PAS 101) — confirms the
   only comparable published grading standard (PAS 101, UK, 2006) applies to
   container cullet glass, not composite-derived recovered glass fibre — supporting
   that no equivalent standard exists for the material BladeLoop models.
   https://www.letsrecycle.com/prices/glass/glass-specifications/
