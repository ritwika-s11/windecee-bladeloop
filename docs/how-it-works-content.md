# How It Works

*Content copy for the How It Works screen (built in Task 5). Written for a non-expert —
a professor should be able to read this and know exactly what the app claims to model,
where the numbers come from, and which parts are our own assumptions.*

---

## What this models

BladeLoop simulates the **thermal co-processing of decommissioned wind turbine blades** —
the recovery of clean glass fibre from blade waste by heating it, without oxygen, until the
resin that binds the fibre breaks down and can be driven off. This is a real chemical
engineering process (pyrolysis), and a real end-of-life route for the tens of thousands of
tonnes of blade material now reaching retirement.

You control **four process inputs** — kiln temperature, how long material stays in the kiln,
how fast material is fed in, and how finely it is shredded first. From those, the model
computes **five output streams** — reclaimed glass fibre, pyrolytic oil, syngas, char dust,
and losses — together with two quality measures for the recovered fibre. The output streams
always add up to the feed rate: nothing appears or disappears, so the **mass balance is
closed**. Change an input and every output responds live.

The point of the app is not to run a perfect plant. It is to show that **there is no single
right setting — only a different customer.** Run for the highest quality and you recover
clean, structural-grade fibre slowly; push more material through and you recover more fibre
per hour at a lower grade — and there is a real buyer for each. What comes out is sold by
grade, not accepted or rejected.

---

## The four inputs, and why they matter

The whole model turns on four settings. Each one has a design set-point; moving away from it
trades quality for throughput, or throughput for quality.

**Kiln temperature (400–700 °C, optimum 600 °C).**
Around 600 °C the resin cracks cleanly and the glass fibre comes through intact. Run too cold
and the resin never fully cracks, so residue stays stuck to the fibre and purity falls. Run
too hot and the fibre itself begins to weaken while more of the material turns to char.

**Retention time (30–45 min, optimum 35 min).**
This is how long material spends inside the rotary kiln. On target, the fibres are fully
freed of resin without over-cooking. Too short and some resin stays bound to the fibre; too
long and the fibre embrittles and char output climbs.

**Feed rate (4,000–9,000 kg/h, optimum 6,500 kg/h).**
How fast shredded material is fed in. At design throughput each particle gets its ideal time
inside the kiln. Push the feed far above capacity and residence time per particle is cut
short — the extra material is worth having, but each piece is processed less completely.

**Particle size (1–20 mm, optimum 2 mm).**
How finely the blade is shredded before it enters the kiln. This is the **most influential
setting in the whole model** — it carries more weight than temperature. At about 2 mm, heat
penetrates evenly and every particle decomposes completely, giving clean fibre. Coarser
feedstock leaves particle cores that never fully decompose, which means poorer fibre and more
waste.

**Feed rate and particle size are linked.** Finer shredding is slower shredding, so the finer
you grind, the less material the shredder can pass per hour. The maximum feed rate the plant
can sustain therefore depends on the particle size — you cannot ask for the finest grind and
the highest throughput at the same time. On the Custom Order screen, the feed control is
bounded by the particle-size control for exactly this reason.

---

## Where the numbers come from

Two different sources sit behind the figures in this app, and it is worth being clear about
which is which.

The **baseline output proportions** — that at good conditions the recovered stream splits into
roughly 70 % glass fibre, 16 % oil, 8 % syngas and 6 % char — come from the **CEE reference
model** for this process. Those are the starting proportions the plant is calibrated to.

Everything that describes how the plant behaves **away from those ideal conditions** — how
much each input can drift before quality suffers, how the deviation of each input is weighted,
how losses grow, and how fibre purity and strength fall as conditions worsen — is **our own
model, calibrated to that baseline.** It reproduces the reference case exactly at the design
set-point and then models the trade-offs around it.

So: the reference model tells us what a well-run plant produces; our model tells you what
happens when you run it for a different target.

---

## Our assumptions, stated honestly

This is the most important part of the app for its credibility, so we state plainly what is
sourced, what is assumed, and what a word like "purity" actually means here.

### The grade tiers

We sort recovered fibre into three grades, each routed to a real end market:

| Tier | Fibre purity | Tensile retention | Goes to |
|---|---|---|---|
| **High** | ≥ 90 % | ≥ 85 % | Composite manufacturing — back into new panels and structural parts |
| **Mid** | ≥ 78 % | ≥ 70 % | Precast concrete and casting — as reinforcing filler |
| **Low** | below | below | Cement works — glass replaces sand, resin content replaces coal |

**The tiering approach is real; the specific threshold numbers are our own project
assumptions.** Grading recovered fibre by quality and routing each grade to a different market
is exactly how this material is handled in practice. But there is **no published grading
standard** for recovered composite glass fibre against which to set the cutoffs — the closest
analogue, PAS 101, covers container cullet glass only, not composite-derived fibre. Every
research group that recovers fibre defines its own quality bar. So we set ours, and we label
them as assumptions rather than claiming a standard that does not exist.

Where the tiers *are* grounded is in demonstrated performance. Real recovered fibre has been
measured across roughly a **72–93 % tensile-retention** range depending on the process:
ordinary single-step pyrolysis of real wind-blade waste lands in the low-to-mid 70s, while a
published two-step pyrolysis study on wind-blade epoxy composite reported **76 % tensile
strength and 88 % modulus retention**. Our mid tier sits right around that demonstrated
result, so it represents a genuinely achievable outcome, not a hypothetical one. Our high tier
(≥ 90 %) is deliberately aspirational — it describes best-in-class, carefully optimised
recovery, not the routine output of a standard thermal process.

**Both markets below the top tier already exist.** Recovered blade fibre is sold into precast
concrete today (for example by Regen Fiber, which supplies shredded blade fibre for slabs,
pavement and precast panels), and cement co-processing — where the glass substitutes for raw
silica and the resin burns as kiln fuel in place of coal — is currently the most commercially
mature end-of-life route for blade material at scale. We did not invent these customers.

> **Low grade is not something you choose — it is where you land.** Contaminated or oversized
> feedstock, a shredder at its limit, an under-fired kiln, or a deadline that forces
> throughput all produce low-grade output. The point of a grade-tiered market is that the
> material still has somewhere to go when the plant can't do better.

### What "purity" means here

The recycling literature reports **tensile and modulus retention** — how much mechanical
strength survives recovery — but it does not generally report a "purity %". That figure is
**our own definition**: *purity is the mass fraction of recovered material that is fibre,
rather than adhered char and resin residue.* We use it as an intuitive measure of how clean
the recovered fibre is. Because it isn't a standard literature metric, the purity half of each
threshold cannot be independently benchmarked the way tensile retention can — which is a
second, separate reason the thresholds are labelled as project assumptions.

### On the blade figures

Where the app translates an order into a number of blades or turbines, it assumes an
**average 2 MW-class blade at about 11 tonnes** (based on the LM 56.8 P design, representative
of the generation now being decommissioned in Europe, and cross-checked against the industry
10–14 t/MW rule of thumb). Actual blade mass varies significantly by turbine size and
generation.

### On the orders themselves

**Order quantities are illustrative; the plant and the orders are not real.** The preset
orders name a *type* of buyer (composite manufacturer, precast concrete producer, cement
works), not a specific company, and every end-use claim is drawn from the sourced CEE
material below.

---

## Sources

The grade-tier evidence and blade-mass figures come from the CEE team's sourcing
(`docs/CEE-deliverable.md`, `docs/grade-threshold-reasoning.md`, Anjani Lohith Kosana &
Hari Krishna Kondam, 30 Aug 2026):

1. Two-step pyrolysis (425 °C + 475 °C) of wind-blade epoxy GFRP — 76 % tensile / 88 % modulus retention.
   https://www.researchgate.net/publication/349385829_Recycling_of_waste_glass_fiber-reinforced_plastics_using_pyrolysis_with_KOH
2. Microwave / single-step pyrolysis of real wind-blade waste — ~72 % tensile retention.
   https://www.researchgate.net/publication/240162064_Microwave_pyrolysis_as_a_method_of_recycling_glass_fibre_from_used_blades_of_wind_turbines
3. Two-temperature-step pyrolysis of E-glass thermoset composites (wind blade + SMC) — up to 19 % tensile improvement over a single-step baseline (OSTI, peer-reviewed).
   https://www.osti.gov/pages/biblio/1525488
4. Review of glass-fibre recovery by pyrolysis — up to ~93 % tensile retention under optimal conditions.
   https://www.researchgate.net/publication/257485767_Recovery_of_glass_fibers_from_glass_fiber_reinforced_plastics_by_pyrolysis
5. Molten-salt-assisted pyrolysis (specialised, non-standard process approaching near-virgin performance), University of Tennessee Research Foundation.
   https://utrf.tennessee.edu/technologies/molten-salt-pyrolysis-recycling-glass-fiber-carbon-composites/
6. PAS 101 background (confirms no equivalent grading standard exists for recovered composite glass fibre — PAS 101 covers container cullet glass only).
   https://www.letsrecycle.com/prices/glass/glass-specifications/
7. Cement co-processing of end-of-life wind blades (most commercially mature route at scale) and Regen Fiber's use of shredded blade fibre in precast concrete — see `docs/CEE-deliverable.md` §3 for the CompositesWorld / ScienceDirect / PACA Web sources.
8. Blade mass: LM Wind Power LM 56.8 P spec (2 MW class, 11.3 t), cross-checked against the 10–14 t/MW rule of thumb — see `docs/CEE-deliverable.md` §2.
