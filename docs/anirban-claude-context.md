# Project Context Dossier — WinDECEE / BladeForge

> This document is a complete project briefing. It contains everything you need to know about the project, the team, the decisions made so far, and the work in flight. Read it end to end before responding to anything.

---

## 1. The Course

The project is being built for the course **"DE-Projekt: Digital Engineering of Process Engineering Applications"** at **Otto-von-Guericke University Magdeburg (OvGU)**, Summer Semester 2026. It's a cooperative course between two faculties:

- **FIN** — Faculty of Computer Science (Digital Engineering, "DE" students)
- **FVST** — Faculty of Process and Systems Engineering (Chemical Engineering, "CEE" students)

Course leads: Dr.-Ing. David Broneske (FIN), Dr.-Ing. Nicole Vorhauer-Huget (FVST), Prof. Saake (FIN).

### Course goal (verbatim from the brief)

> *"Development of an interactive online application (APP) that enhances the understanding of a specific chemical engineering process."*

### Past projects

The course has run for years and past student groups have built apps for: PEM/SOFC fuel cells, alkaline water electrolysis, single-stage batch distillation, fluidized bed granulator, aero-cyclone, energy-efficient house, large-scale hydrogen production, bio fuel cell, conveyor-belt dryer, two-phase separator, sodalok (soda locomotive). All previous apps share a pattern: a 3D scene of equipment with controllable parameters, animations, info bubbles, sometimes character explainers — they teach a *single process* through *visualization* and *interaction*. They are typically built in **Blender + Unity** and shipped as **Windows .exe + Android .apk** builds.

### Course structure & timeline

10 sprints, plus group formation and kickoff. Bi-weekly sprint demos with PowerPoint + live app build.

| Date | Milestone |
|---|---|
| 10.04.26 | Introduction, group formation begins |
| 17.04.26 | Group formation done |
| 24.04.26 | Kickoff: groups formed, topic roughly presented |
| **08.05.26** | **Sprint 1** — work plan, first concept, division of tasks (this is *tomorrow*) |
| 22.05.26 | Sprint 2 — process presented in full detail |
| 05.06.26 | Sprint 3 |
| 19.06.26 | Sprint 4 — concept fix, macroscopic design implemented |
| 03.07.26 | Sprint 5 |
| 17.07.26 | Sprint 6 — all flows implemented |
| 31.07.26 | Sprint 7 |
| 14.08.26 | Sprint 8 — beta version submitted |
| 11.09.26 | Sprint 9 — final app + report submitted |
| Sept TBD | Defense |

Course requires a **Plan B** running in parallel to the main concept. Only 2 sprint meetings (including kickoff) can be missed.

### Credit weighting (important)

- DE students: **12 credits** each
- CEE students: **4 credits** each

The 3:1 credit ratio means the DE side owns roughly 75% of the total workload. The app's complexity, polish, and feature set should reflect this — CEE owns the science backbone, DE owns essentially everything else.

---

## 2. The Team — WinDECEE (Group 4)

Six members, four DE + two CEE.

### DE side (4 people)

| Name | Role | Skills |
|---|---|---|
| **Ritwika Sen** | Project Lead, DE Architecture | Owns architecture, build pipeline, deployment, integration, oversight |
| **Anirban Maji** | UI/UX & Dashboard Lead | Strong at UI/UX design, dashboards. *(This is you — the Claude reading this is supporting Anirban.)* |
| **Akshat Daruka** | 3D & Process Scenes | Heavy lifter, can pick up anything. Owns Blender modelling, Unity scenes, animations, drill-down, educational layer, and Plan B |
| **Sharan Murali** | UI Components | Gets bounded technical tasks — splash/loading scenes, reusable UI prefabs, CO₂ translator widget, comparison toggle, localization. Cannot take on big modules but does need technical work, not just docs |

### CEE side (2 people)

| Name | Role | Background |
|---|---|---|
| **Anjani Lohith Kosana** | CEE Lead | Was on "Team Hyperion" that produced an LCA on wind turbine blade recycling — that report is the scientific foundation of this project |
| **Hari Krishna Kondam** | CEE | Also from Team Hyperion |

### Workload allocation (DE side)
- Ritwika: cross-cutting architecture and oversight, fills overflow
- Anirban: UI/UX and all dashboard widgets
- Akshat: 3D scenes, animations, educational layer, Plan B (intentionally heaviest)
- Sharan: small bounded modules with end-to-end ownership

---

## 3. Project Journey — Original Idea → Pivot

### The original idea (from the kickoff deck)

The team initially proposed **"BLADE LOOP — Life Cycle Intelligence for Windmill Blade Recycling"**: a comparative dashboard showing three recycling routes (shredding, thermal, chemical) side-by-side, with a country selector (Germany / Netherlands / Denmark), live KPIs, scenario comparison, and a "Human-Scale CO₂ translator" feature.

### Why we pivoted

Looking at the course brief and every past project, there is a clear pattern: the apps teach a *specific chemical engineering process* through *visualization*, not through dashboard-style scenario comparison. The original "BLADE LOOP" concept was effectively a BI/decision-support dashboard, not a process visualization. There was a real risk the professors would push back.

A second issue: the team only has detailed scientific data (DWSIM, OpenLCA, mass balances, transport math) for **one** of the three routes — thermal co-processing. The other two routes had no data backing them. With Sprint 2 only three weeks away requiring "the process presented in full detail," this was a major scope risk.

### The pivoted concept — BladeForge

(The name "BladeForge" or "Blade2Clinker" is a working title — the team can rename.)

The app is a 3D interactive plant where the user follows the journey of a shredded wind turbine blade through five stages of thermal co-processing: feed mixer → heater (HT-1) → solid-gas separator → secondary separator → cement kiln. The user adjusts parameters (temperature, blade composition, country of origin) and sees the live impact on mass flow, energy use, cost, and CO₂ emissions.

Critically, **the cool features from the original idea are not dropped** — they're *layered onto* the process visualization:

- The **Human-Scale CO₂ Translator** is now embedded inside the 3D scene as live overlays — hovering over the heater shows "this stage emits ≈ X kg CO₂, equivalent to 42 flights Berlin → New York"
- The **Country Selector** still exists — reframed as "where is the blade coming from? where is the cement kiln?" — adjusts the transport leg of the LCA
- A **vs Landfill comparison toggle** replaces the three-route comparison (matches the actual Hyperion LCA benchmark)
- A **story mode** ties it all together as a 5-minute guided tour, with free-exploration unlocked after

The pivot keeps everything that's interesting about the original concept while satisfying the course requirement that the app teach a specific process.

---

## 4. The Chemical Engineering Process

This is what the app visualizes. The science comes from the team's own LCA work (Team Hyperion's report on wind turbine blade recycling, attached separately).

### The problem

Germany alone will need to manage roughly **52,000 tonnes of decommissioned wind turbine blade waste by 2030**. Wind turbine blades are mostly **glass fibre reinforced polymer (GFRP)** — durable, lightweight, hard to recycle. Current disposal (landfill or incineration) is environmentally and economically poor.

### The process: thermal co-processing for cement industry

**Functional unit** for calculations: 5.7 tonnes of blade waste = 3 blades from a Vestas V52 turbine.

**Blade composition:**
- 56% E-glass fibres (3,192 kg)
- 30% epoxy resin (1,710 kg)
- 9% foam & adhesives (513 kg)
- 5% metals — steel bolts, copper lightning conductor (285 kg)

**Five process stages:**

1. **Blade feed + Air feed** — shredded GFRP arrives, mixed with air (oxidant)
2. **Feed mixer** — homogenizes feed, distributes oxygen, stabilizes flow
3. **Heater (HT-1)** — thermal treatment at 850–1450 °C, ~270 kW input. Resin matrix combusts, releases gaseous byproducts, liberates glass fibres
4. **Solid-Gas Separator** — cyclone or baghouse. Splits stream into gas outlet (CO₂, H₂O, traces) and solid residue (glass, ash, char)
5. **Secondary Separator (CS-2)** — final classification: high-purity GFRP (reusable glass fibre) vs waste (ash, fragments)

**Outputs and where they go:**
- **Char** (~1,200 kg) → substitutes coal in cement kilns (energy credit)
- **E-glass fibres** (~3,500 kg) → substitutes SiO₂ + Al₂O₃ in cement clinker
- **CO₂ fossil emissions** (~1,000 kg) — released during pyrolysis
- **Solid residue** (~400 kg) — disposal or low-grade use

**Key chemistry:**
- Epoxy combustion: C₉H₁₀O₂ + 11 O₂ → 9 CO₂ + 5 H₂O
- Calcium silicate formation in cement: 2 CaO + SiO₂ → 2 CaO·SiO₂
- Calcium aluminate: 3 CaO + Al₂O₃ → 3 CaO·Al₂O₃
- Calcium aluminoferrite: 4 CaO + Al₂O₃ + Fe₂O₃ → 4 CaO·Al₂O₃·Fe₂O₃

**Cost example (per 5.7 t batch):**
- Diesel (transport, 278 km, 83.4 L @ €2.30): ~€192
- Electricity (1,471 kWh @ €0.18): ~€265
- Total operating cost: ~€457

**Transport scenarios (Germany baseline):**
- Bremen → Lägerdorf (cement plant): 147 km
- Windfarm → preprocessing: 86 km
- Preprocessing → cement factory: 45 km

**Compared to landfill**, co-processing shows significant CO₂ reductions plus avoided burdens from coal substitution and virgin raw material substitution.

**LCA tools used by CEE team:** DWSIM (process modelling, mass-energy balance), OpenLCA (impact assessment).

**Impact categories evaluated:** acidification, climate change (fossil + biogenic), ecotoxicity, eutrophication (freshwater + terrestrial), human toxicity (cancer + non-cancer + metals), particulate matter, ionising radiation, ozone depletion, resource use, water use.

---

## 5. The App — BladeForge

### Product concept (one sentence)

An interactive online application where a non-expert user walks through a 3D wind turbine blade thermal co-processing plant, adjusts process parameters, and learns how blade waste becomes cement industry feedstock — while seeing live, human-scale impact numbers (CO₂, energy, cost).

### Core experience

The user opens a URL in a browser (no install). They land in a **macro view** — a clean 2.5D schematic of the full plant flowsheet, recognisably mapped to the DWSIM diagram. Particles (representing shredded blade material) move through the system with animation. A **parameter panel** lets them adjust temperature, blade composition, country of origin. A **KPI dashboard** updates live: mass processed, CO₂ emitted, energy used, cost in €, recovery efficiency %.

Clicking on any unit operation **drills down** into a richer 3D scene of that piece of equipment, with educational overlays (chemistry equations, plain-language explanations, hover tooltips). A **CO₂ translator** appears contextually — "this stage emits 580 kg CO₂ ≈ a car driving 4,300 km."

A **story mode** offers a 5-minute guided tour for first-time users; **free exploration** unlocks after.

### Layered feature architecture

- **Core layer:** 3D process visualization (the spine — the *specific process* the course requires)
- **Layer 1 — KPI Dashboard:** live metrics (mass, CO₂, energy, cost, recovery)
- **Layer 2 — Country Selector:** map UI, picks endpoints, updates transport leg of LCA
- **Layer 3 — CO₂ Human-Scale Translator:** contextual overlays converting kg CO₂ into relatable units (flights, cars, homes)
- **Layer 4 — Comparison Toggle:** "vs landfill" before/after view
- **Layer 5 — Story Mode:** guided tour with narration cards

### Target user

A non-expert — a sustainability-curious adult, a student outside chemical engineering, a member of the public reading about wind energy waste. Not a process engineer. The app is *educational*, not *analytical*.

### Success criterion

By the end of 5 minutes, the user can explain in their own words: what blade waste is, how thermal co-processing works at a high level, why E-glass and char are useful for cement, and how transport affects the impact.

---

## 6. Architecture & Tech Decisions (locked)

### Engine: **Unity**

Reasons: course explicitly suggests Blender → Unity pipeline, every past app is Unity-based, community support is strong, native cross-platform. Unity LTS version (latest stable LTS at sprint kickoff) — locked in Sprint 1 to prevent version drift across team.

### Primary deployment target: **WebGL build**

Unity exports HTML/JavaScript/WASM static files. Hosted on **GitHub Pages** at a public URL. The user opens a link in any modern browser — no install, no executable. This is what "online application" means in the brief.

### Secondary build: **Windows .exe**

Built in parallel from the same Unity project (one extra build target). Used for the live demo at sprint reviews and defense — better performance than WebGL, no network dependency.

### Computation strategy: **surrogate equations, not embedded DWSIM**

The CEE team will distill their DWSIM model into ~10–20 closed-form equations (e.g., energy_kWh = f(mass, target_temp); CO2_kg = f(epoxy_mass, combustion_efficiency); char_yield_kg = f(epoxy_mass, temperature)). The Unity app evaluates these in real time. This is the single biggest scope-saving decision — avoids embedding DWSIM, avoids pre-baking every scenario.

### Data contract: **versioned JSON schema**

Locked by Sprint 5 (the formal CEE → DE handoff). Every input parameter has name, unit, min/max, default, dependencies. Every output has a formula. Once signed off, DE can build against mocks while CEE finalises the science underneath.

### Visual fidelity: **hybrid 2.5D + drill-down 3D**

Macro view = clean 2.5D schematic of the DWSIM flowsheet. Click a unit op → drill into a detailed 3D scene with animated particles, heat gradients, micro-explanations. Avoids trying to model the full plant in photoreal 3D, which would eat the timeline.

### Repository: **GitHub**

Suggested name `windecee-bladeforge`. Branch strategy: `main` (auto-deploys to GitHub Pages), `develop` (integration), `feature/*` per-area branches. CI: GitHub Actions build Unity WebGL on PR, deploy on merge to main.

### Folder structure (planned)

```
windecee-bladeforge/
├── unity-project/
├── blender-assets/
├── ce-data/             # CEE deliverables: schema, equations, country data
├── plan-b/              # 2D-only fallback
├── docs/
└── README.md
```

### Localization: English-first

Hooks for German, but English is the MVP language. German is a Sprint 9 polish task.

### Plan B: **2D-only schematic version**

Same data schema, same surrogate equations, same parameter panel, same KPIs, same CO₂ translator — only the rendering layer differs. SVG/2D animations of the flowsheet instead of 3D scenes. Owned by Akshat with Ritwika support. Built in parallel as the course requires.

### Pedagogy approach

Information layered: minimal default text, expandable info panels on click, optional guided tour, no voice narration (out of scope for 6 students). User testing planned for Sprint 7 with 5 non-CEE students.

---

## 7. Sprint Roadmap & Anirban's Allocation

### Multi-sprint allocation (full team — for context)

Already documented in `/Users/.../WinDECEE/sprint-plan.md`. Summary:

- **Ritwika (lead):** GitHub + Unity + WebGL pipeline (Sprint 1), data schema design (Sprint 2), integration framework (Sprint 3), oversight throughout, performance optimization (Sprint 6), final integration (Sprint 8–9)
- **Anirban (UI/UX):** wireframes (Sprint 1), design system + hi-fi mockups (Sprint 2), KPI widgets in Unity (Sprint 3–4), parameter control panel (Sprint 5), country selector UI (Sprint 6), story mode UI (Sprint 7), polish (Sprint 8–9)
- **Akshat (3D):** Blender block models (Sprint 1), detailed models (Sprint 2), macro scene (Sprint 3), animations (Sprint 4), drill-down 3D (Sprint 5), interaction logic (Sprint 6), educational overlays (Sprint 7), Plan B 2D version (Sprint 8)
- **Sharan (UI components):** splash/main menu (Sprint 1), reusable UI prefabs (Sprint 2), loading manager (Sprint 3), CO₂ translator widget (Sprint 4–5), comparison toggle (Sprint 6), localization scaffolding (Sprint 7)
- **CEE (Anjani + Hari):** scope confirm (Sprint 1), full process presentation (Sprint 2), surrogate equations (Sprint 3), country data (Sprint 4), validated schema handoff (Sprint 5), chemistry educational content (Sprint 6), validation and report (Sprint 7–9)

### Anirban's role across the project

Anirban owns the entire UI/UX surface. From wireframes through to final polish. Specifically:

- **Sprint 1 (now):** initial wireframes for the macro process view and the dashboard, design system v0 (colours, typography, spacing tokens)
- **Sprint 2:** hi-fidelity mockups in Figma; component library specs; information architecture across screens
- **Sprint 3:** KPI widget specs translated into first Unity UI (e.g., mass-processed counter as the first vertical slice)
- **Sprint 4:** full KPI dashboard widgets in Unity — mass, CO₂, energy, cost, recovery efficiency
- **Sprint 5:** parameter control panel in Unity — sliders, dropdowns, presets
- **Sprint 6:** country selector UI (map + dropdown) integrated with transport data
- **Sprint 7:** story mode UI — tour navigation, step indicators, narration cards
- **Sprint 8:** polish, micro-animations, transitions, accessibility passes
- **Sprint 9:** final QA, presentation polish, screenshots for the report

---

## 8. CEE Deliverables Anirban Will Eventually Build Against

The UI work is currently building against **mocks** — placeholder values. The real numbers come from CEE in stages:

- By **Sprint 2**: full process documentation, all parameters, all data ranges (the scientific spec)
- By **Sprint 3**: surrogate equations (formulas that the app will compute live)
- By **Sprint 4**: country-specific data — distances, diesel rates, grid emission factors for Germany / Netherlands / Denmark
- By **Sprint 5**: validated JSON schema (the formal handoff)
- By **Sprint 6**: plain-language chemistry explanations for educational overlays
- Throughout: validation that the app's numbers match DWSIM/OpenLCA reference

UI design should anticipate ranges and units that aren't fully locked yet — design for flexibility (e.g., a CO₂ widget that could show 100 kg or 100,000 kg without breaking layout).

---

## 9. Source Materials That Exist

The team has these reference documents on hand:

1. **DE PROJECT 1.pdf** — official course brief from Dr. Broneske and Dr. Vorhauer-Huget. Specifies the goal, past projects, structure, sprint timeline, deliverables, Plan B requirement, and tooling suggestion (Blender + Unity).

2. **DE Project 2026 Kickoff.pdf** — the team's original kickoff presentation (the pre-pivot "BLADE LOOP" deck). Contains the original three-route comparison concept, market gap framing, Linear board screenshot, and project timeline. Useful as a record of the original thinking but **the concept it presents has been pivoted** — not all of its features survive.

3. **Gate III submission HYPERION — LCA of Wind Turbine recycle.pdf** — the team's existing LCA report. This is the **scientific foundation of the entire project**. It contains: full process flow, mass and energy balances, DWSIM diagram, OpenLCA impact results, transport calculations, cost analysis, blade composition, chemistry, and references. Anjani and Hari were on this team. Much of the app's data flows from this report.

4. **The chemical engineering team.docx** — short note describing the seven CEE pillars (Process Design, Math Model, Transport, Process Engineering, LCA, I/O Framework, System Logic).

5. **sprint-plan.md** (in the project workspace) — full sprint plan and per-person allocation.

6. **cee-handover.md** (in the project workspace) — onboarding doc for the CEE team.

---

## 10. Tools & Conventions

- **Project management:** Linear (board already initialized with WIN-XX issue numbering)
- **Repository:** GitHub (being set up in Sprint 1)
- **Design tool:** Figma (Anirban's primary)
- **3D modelling:** Blender
- **Engine:** Unity (LTS, locked version)
- **Communication:** Linear for tasks, WhatsApp/Slack for quick chat, bi-weekly sprint demos

---

## 11. Known Open Questions / Decisions Still in Motion

- Final app name (working: BladeForge / Blade2Clinker — team can rename)
- Exact Unity LTS version (locked in Sprint 1)
- Exact GitHub org name (will be created in Sprint 1)
- Whether to use Unity UI Toolkit or uGUI for dashboards (Anirban + Ritwika to decide before Sprint 3)
- How explicit to make the "vs landfill" comparison — full alternate scenario or just delta numbers
- Story mode narration: text-only vs animated character (past projects had characters; we're leaning text-only for scope)
- Whether to support touchscreen / tablet form factor or desktop-only

---

## 12. The Single Most Important Framing

The course wants an app that **teaches a specific chemical engineering process** through visualization and interaction. Everything else — KPIs, country selector, CO₂ translator, comparison — is a *layer on top* of that core. Whenever a design or scope decision is made, ask: *does this help the user understand the process better?* If yes, prioritize. If it's adding analysis or comparison without deepening process understanding, deprioritize or simplify.

This framing is what saved the project from drifting into "BI dashboard" territory. Hold the line.
