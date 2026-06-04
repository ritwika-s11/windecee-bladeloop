# Welcome to WinDECEE — CEE Team Handover

**For:** Anjani Lohith Kosana & Hari Krishna Kondam
**From:** Ritwika (DE Lead) and the WinDECEE team
**Project:** BladeForge — DE-Project Summer Semester 2026

---

## Hi Anjani and Hari 👋

Thanks for joining the team. This document is meant to give you a clear picture of:

1. **What we're building**
2. **Why your existing LCA work is the foundation of the project**
3. **What we need from you, sprint by sprint**
4. **How we'll work together**

It's intentionally short. Read it once, message us with questions, and we'll go from there.

---

## What we're building

We're building an **interactive online application** that helps a non-expert person understand how decommissioned wind turbine blades are recycled using thermal co-processing — the same process you already studied in the Hyperion LCA report.

Think of it as a **virtual tour of the recycling plant**. The user opens a web link, sees a 3D plant on screen, and walks through it step by step:

> Shredded blade arrives → mixed with air → heated → solids and gases separate → pure glass fibres and char come out → those go into a cement kiln.

At each stage, the user can:

- Adjust simple parameters (like temperature, blade composition, or where the blade is coming from)
- See the live impact on **mass, energy, cost, and CO₂ emissions**
- See CO₂ translated into things people understand — *"this batch ≈ 42 flights from Berlin to New York"*
- Compare against landfilling (the old way) to see how much better recycling is

The goal is **education, not analysis**. By the end of 5 minutes, the user should *understand* the process — what each piece of equipment does, what's happening chemically, and why this matters environmentally.

It's a single-page application — no installation needed, just a URL.

---

## Why your LCA work is the heart of this

Your **Team Hyperion LCA report on wind turbine blade recycling** is *the* scientific foundation of the entire app.

Specifically, we're using:

- The **process flow** you modelled in DWSIM — feed mixer → heater (HT-1) → solid-gas separator → secondary separator
- The **mass and energy balances** for the 5.7-tonne functional unit
- The **transport math** (km × tonnes × emission factor)
- The **OpenLCA impact results** — climate change, eutrophication, particulate matter, etc.
- The **substitution calculations** — char replacing coal, E-glass replacing SiO₂ and Al₂O₃ in cement

You're not starting from scratch. You're translating work you've already done into a form the app can use. That's a much smaller lift than it sounds — most of it is repackaging, not new modelling.

---

## What we need from you

Your role across the project:

### Sprint 1 (this week — 08.05.26)

- Read this handover, raise questions early
- Confirm the scope makes sense — single thermal co-processing route, based on your Hyperion work
- Flag anything in the existing LCA you think won't transfer cleanly

### Sprint 2 (22.05.26) — *your big presentation*

The course requires the full process to be presented in detail at Sprint 2. That's your stage.

Prepare a presentation covering:

- The 5-stage process flow (with the DWSIM diagram you already have)
- Inputs, outputs, and conditions at each stage
- Key parameters that matter (temperature ranges, feed rates, air ratios)
- Material composition of the blade (E-glass, epoxy, foam, metals)
- Where the chemistry happens — what reactions occur and why

Most of this is already in your LCA report. You're mostly reorganising it for presentation.

### Sprint 3 (05.06.26) — *the most important deliverable*

Translate your DWSIM model into **simple equations the app can run live**.

Right now, your model lives inside DWSIM software. The app can't open DWSIM. We need you to extract roughly **10–20 simple formulas** that represent the same calculations, e.g.:

- Energy required (kWh) = f(mass, target temperature)
- CO₂ output (kg) = f(epoxy mass, combustion efficiency)
- Char yield (kg) = f(epoxy mass, temperature)
- Glass yield (kg) = f(blade mass, E-glass fraction)

Each formula should have:

- A clear **input** (what goes in)
- A clear **output** (what comes out)
- **Units**
- **Valid ranges** (e.g., temperature 500–1500 °C)

Don't worry about programming this. Just give us the formulas in a Word doc or spreadsheet. We'll wire them into the app.

### Sprint 4 (19.06.26) — *transport & country data*

The app has a **country selector** — Germany, Netherlands, Denmark.

For each country, give us:

- Typical transport distance from windfarm → recycling facility (km)
- Typical transport distance from facility → cement kiln (km)
- Diesel consumption rate for the truck type
- Country's electricity grid CO₂ intensity (kg CO₂ / kWh)

You already have most of this for Germany in your LCA. Netherlands and Denmark are extensions — public LCA databases and government reports have this data.

### Sprint 5 (03.07.26) — *the big handoff*

This is the formal CEE → DE handoff.

Deliver: a **finalized data file** (we'll provide a template) with all the equations and country data validated and locked. After Sprint 5, the DE team owns the calculation layer of the app — your job shifts from delivering data to validating accuracy.

### Sprint 6 (17.07.26) — *educational content*

When the user clicks on a unit operation, the app shows a small info panel explaining the chemistry. We need short, plain-language explanations from you for:

- The heater (HT-1): what happens to epoxy when heated, what happens to glass fibres
- The separators: how cyclone/baghouse separation works
- Why E-glass works as a cement raw material (the calcium silicate / aluminate reactions)
- Why char works as a coal substitute

Aim for **2–4 sentences each**, written for someone who hasn't studied chemistry. Plus the relevant chemical equation.

### Sprints 7–9 (Aug–Sept)

- **Sprint 7:** Validate the app's numbers against your DWSIM/OpenLCA reference. If the app says "1,000 kg CO₂" and DWSIM says "950 kg," tell us — we need to fix it.
- **Sprint 8:** Beta freeze — final review, contribute CEE sections to the preliminary report.
- **Sprint 9:** Final report contributions, defense preparation.

---

## How we'll work together

- **Repository:** GitHub. Ritwika will set this up and add you both. You don't need to know git deeply — for your deliverables (documents, spreadsheets), we can manage the uploads on the DE side if needed.
- **Communication:** Linear board for tasks (you'll be added). WhatsApp / Slack for quick questions. Bi-weekly sync calls before each sprint demo.
- **Document deliverables:** Word docs, spreadsheets, or PDFs are fine. You don't need to write code.
- **Demos:** Every other Friday is a sprint demo with the professors. CEE leads Sprint 2, supports the rest.

---

## What success looks like for you

By the end of the project, your contribution is:

1. The validated **process and chemistry** behind the app — the science is sound and accurate
2. A clean **set of equations and data** that the DE team built the app on top of
3. Plain-language **chemistry explanations** baked into the app's educational layer
4. Confidence at the defense that the numbers in the app match real-world behaviour

You're the scientific authority on this project. The app is only as credible as the science you give it.

---

## First steps for you

1. Read this once, end to end
2. Skim your own Hyperion LCA report again with the app in mind
3. Reply to the team with:
   - Anything in the scope that worries you
   - Any data gaps you anticipate (especially for Netherlands / Denmark)
   - Anything you don't understand from this handover

We'll meet this week to confirm scope alignment and answer your questions before Sprint 1 on Friday.

Excited to build this with you.

— Ritwika & the DE team
