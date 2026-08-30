# CEE Brief — Anjani & Hari

**For:** Anjani Lohith Kosana, Hari Krishna Kondam
**From:** Ritwika (DE Lead)

---

> ## ✅ ANSWERED — 30 August 2026
>
> Anjani and Hari delivered all three items ahead of schedule. **Their answers are in
> `docs/CEE-deliverable.md` and `docs/grade-threshold-reasoning.md`** — read those, not this brief.
>
> **What changed as a result:**
>
> 1. They found that our model's design case claimed **100 % tensile retention** — i.e. that thermal
>    recovery does no damage to the fibre. The literature does not support that: optimised
>    multi-step pyrolysis tops out at 90–93 %, and standard single-step processes cluster at 72–76 %.
> 2. **`ProcessModel.cs` has been re-anchored** — quality metrics now ceiling at 93 % purity / 90 %
>    tensile. Response curve shapes are unchanged; only the anchor moved. The mid-grade preset now
>    lands at 76.5 % tensile, within half a percent of their sourced two-step wind-blade result.
> 3. **Grade tiers moved to match:** High ≥90 %/≥85 %, Mid ≥78 %/≥70 %.
> 4. **Blade mass: 11.3 t** (2 MW-class, LM 56.8 P), stated in-app as an assumption.
> 5. They confirmed **no published grading standard exists** for recovered composite glass fibre,
>    and that **"purity %" is not a metric the literature uses** — so we define it ourselves.
>
> The original brief is kept below for the record.

---

**Original brief — due Wednesday 3 September 2026 · effort ~half a day**

---

## What changed

The professors' feedback was that our app looks like a video — pretty, but nothing the user does
changes anything. So we've reframed it.

Instead of *"watch a recycling plant"*, the app is now:

> **A customer orders recovered glass fibre at a certain grade. The app works out what plant
> settings deliver it, then shows the plant running at those settings.**

Full detail is in `docs/BLADELOOP-PRODUCT-VISION.md` if you want it — you don't need it for this task.

---

## Why we need you

The reframe rests on one claim: **recovered glass fibre isn't good or bad, it comes in grades, and
different grades go to different customers.** We're confident that's directionally right, but our
specific numbers are currently guesses — and we don't want to state guesses as fact in front of the
examiners.

You'll find this faster than we will, and you'll know what's defensible.

---

## Deliverable 1 — Grade tier thresholds

We split output into three tiers using two quality metrics our model already computes:
**fibre purity (%)** and **tensile strength retention (%)**.

Current placeholder boundaries:

| Tier | Fibre purity | Tensile retention | We're saying it goes to |
|---|---|---|---|
| **High** | ≥ 95 % | ≥ 95 % | Composite manufacturing — new panels, structural parts |
| **Mid** | ≥ 85 % | ≥ 80 % | Precast concrete and casting — reinforcement filler |
| **Low** | below that | below that | Cement works — co-processed as fuel and raw material |

**What we need:**

1. Is this tiering approach sound at all? If recovered fibre is normally graded some other way,
   tell us — better to change now than defend something wrong.
2. Are these numeric boundaries defensible? Give us better ones if you have them.
3. **If no published thresholds exist, say so explicitly.** That is a completely acceptable answer —
   we'll label them as project assumptions in the app and report, which is honest and defensible.
   What we can't do is guess without knowing we're guessing.

Sources for anything you can source, please.

> One data point we came across: recovered fibres have been reported to retain around **76 % of
> tensile strength and 88 % of modulus**. That's in the same territory as our mid tier, which is
> mildly reassuring — but please check it properly rather than us leaning on one number.

---

## Deliverable 2 — Average blade mass

The app tells the user how much blade material an order needs. We compute tonnes from our model,
but to say *"that's about N blades"* we need one number.

**We need:** an average mass for a decommissioned wind turbine blade, the turbine class it assumes,
and a source.

We know this varies enormously by turbine size and vintage. **We're not asking for a universal
figure — we're asking you to pick one defensible figure and tell us what it assumes**, so we can
state it as an explicit assumption. A range plus a recommended value is ideal.

---

## Deliverable 3 — End use per tier

One or two sentences per tier: what is recovered glass fibre of that quality **actually used for**,
in practice, today? With a source where you have one.

This becomes user-facing text, so plain language beats technical precision. Something a non-expert
would understand.

---

## Optional but genuinely useful — sanity-check our numbers

These come out of our process model. Do any look wrong to you as engineers?

**Design case** (600 °C, 35 min, 6,500 kg/h, 2 mm):

| Stream | Share of feed | kg/h |
|---|---|---|
| Glass fibre | 69.0 % | 4,482 |
| Pyrolysis oil | 15.8 % | 1,024 |
| Syngas | 7.9 % | 512 |
| Char | 5.9 % | 384 |
| Losses | 1.5 % | 98 |

Fibre purity 99 %, tensile retention 100 %, efficiency 100 %.

**Degraded case** (550 °C, 35 min, 8,800 kg/h, 16 mm):

| Stream | Share of feed |
|---|---|
| Glass fibre | 46.5 % |
| Char | 26.5 % |
| Losses | 7.5 % |

Fibre purity 74.5 %, tensile retention 64.9 %, efficiency 50 %.

**Two specific questions:**

1. At 16 mm particle size and 550 °C, is **26.5 % char** plausible for incomplete decomposition, or
   too high?
2. Our loss stream (fugitive dust past the baghouse, moisture flash-off, adhered residue) runs
   1.5 % at optimum and caps at 10 %. Reasonable?

Direction and rough magnitude is enough — don't worry about giving us replacement equations.

---

## Format

Markdown file or plain email. Please structure it as:

```
## 1. Grade thresholds
   - our numbers are: sound / should change to X
   - source: ...
   - or: no published standard found — treat as project assumption

## 2. Average blade mass
   - recommended value: ... t
   - assumes: ...
   - source: ...

## 3. End use per tier
   - High: ...
   - Mid: ...
   - Low: ...

## 4. Sanity check (optional)
```

Send to Ritwika by **Wednesday 3 September**. Sharan is building the app's *How it works* page
around your answers and has a placeholder waiting.

---

## What we're not asking for

- No Unity work, no code, no 3D
- No new LCA calculations
- Not a report — bullet points with sources are perfect

If anything here is unclear, or you think we're framing the engineering wrongly, please say so
early. Changing the plan on 3 September is easy; changing it on 10 September is not.
