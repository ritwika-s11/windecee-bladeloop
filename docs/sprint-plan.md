# WinDECEE — Sprint Plan & Team Allocation
**Project:** BladeForge — Interactive Visualization of Wind Turbine Blade Thermal Co-Processing
**Course:** DE-Project Summer Semester 2026, OvGU Magdeburg
**Team:** WinDECEE (Group 4)

---

## Project Goal (one sentence)

Build an interactive online application that lets a non-expert user understand the *thermal co-processing of decommissioned wind turbine blades* — a real chemical engineering process — by walking through the full plant in 3D, adjusting key parameters, and seeing the live impact on mass flow, energy, cost, and CO₂ in human-relatable terms.

---

## Team & Capacity

### DE Side (12 credits each — owns ~75% of total workload)

| Person | Role | Primary Focus |
|---|---|---|
| **Ritwika Sen** | Project Lead & Architect | Architecture, build pipeline, deployment, integration, oversight, professor liaison |
| **Anirban Maji** | UI/UX & Dashboard Lead | Wireframes, design system, dashboard widgets, parameter panels, country selector, story mode UI |
| **Akshat Daruka** | 3D & Process Scene Lead | Blender modelling, Unity scene assembly, animations, drill-down scenes, educational layer, Plan B |
| **Sharan Murali** | UI Component Engineer | Splash/loading scenes, reusable UI prefabs, CO₂ translator widget, comparison toggle, localization scaffolding |

### CEE Side (4 credits each — owns ~25% of total workload, science backbone)

| Person | Role | Primary Focus |
|---|---|---|
| **Anjani Lohith Kosana** | CEE Lead — Process & LCA | Process scope, surrogate equations, LCA validation, schema co-design |
| **Hari Krishna Kondam** | CEE — Transport & Materials | Country-specific transport data, material data, chemistry educational content |

---

## Sprint Roadmap (overview)

| Sprint | Date | Theme | Key Deliverable |
|---|---|---|---|
| Kick-off | 24.04.26 | Done | Team formed, topic chosen |
| **Sprint 1** | **08.05.26** | **Plan & Pipeline** | **Concept locked, GitHub repo + WebGL hello-world live, work plan presented** |
| Sprint 2 | 22.05.26 | Process detail | CEE presents full process; DE shows wireframes; data schema v0 drafted |
| Sprint 3 | 05.06.26 | Foundation | Macro 2.5D scene, parameter UI shell, surrogate equations v1 |
| Sprint 4 | 19.06.26 | **Concept fix** | Macroscopic Unity scene live, KPI widgets working, dashboard layer integrated |
| Sprint 5 | 03.07.26 | **CEE → DE handoff** | Validated JSON data schema; DE owns end-to-end calculation |
| Sprint 6 | 17.07.26 | All flows live | Drill-down scenes, animations, country selector, CO₂ translator, comparison toggle |
| Sprint 7 | 31.07.26 | Story & polish | Story/tour mode, full integration, end-to-end QA begins, user testing |
| Sprint 8 | 14.08.26 | **Beta freeze** | Beta version submitted, preliminary report, only debugging after |
| Sprint 9 | 11.09.26 | **Final** | Final app + full report, polish, user guide, defense prep |

---

## Sprint 1 — Plan & Pipeline

**Dates:** 04.05.26 — 08.05.26 (5 days)
**Sprint Goal:** Walk into Sprint 1 with concept locked, architecture decided, work plan presented, Plan B in parallel, and a working WebGL deployment URL — to prove the pipeline before any feature work.

### Sprint 1 Backlog

| P | Item | Owner | Notes |
|---|---|---|---|
| P0 | Sprint 1 presentation deck (concept, gantt, work division, Plan B, architecture) | Ritwika + Anirban | Required deliverable |
| P0 | Initialize GitHub repo with branch strategy & folder layout | Ritwika | Public org repo, README, .gitignore |
| P0 | Initialize Unity project (LTS version, locked) committed to repo | Ritwika | Everyone pulls from same baseline |
| P0 | Set up GitHub Actions CI for Unity WebGL build | Ritwika | Auto-build on PR, deploy on merge to main |
| P0 | Hello-world WebGL deployment to GitHub Pages | Ritwika | A live URL by Friday |
| P0 | First-pass wireframes: macro view, parameter panel, KPI dashboard | Anirban | Hand-drawn or Figma is fine |
| P0 | Design system v0: colors, typography, spacing tokens | Anirban | Used across all UI work |
| P0 | Begin Blender block-models of 5 unit operations | Akshat | Rough placeholders, to be detailed later |
| P0 | Set up Unity locally + onboard repo | Sharan | Build a splash screen scene as first PR |
| P0 | CEE: confirm scope alignment, prepare Sprint 2 process presentation | Anjani + Hari | The Sprint 2 deliverable is on them |
| P1 | Plan B written summary (2D-only fallback approach) | Ritwika + Akshat | Required by course |
| P1 | Linear board mirrors GitHub issues, task labels set | Ritwika | Already started on Linear |

### Definition of Done (Sprint 1)
- [ ] Sprint 1 deck approved by team before Friday
- [ ] GitHub repo accessible to all 6 members
- [ ] Hello-world WebGL build deployed and shareable URL exists
- [ ] Each person knows their tasks for Sprint 2
- [ ] Plan B documented in repo

---

## Per-Person Multi-Sprint Allocation

### Ritwika — Lead, Architecture, Build Pipeline

| Sprint | Tasks |
|---|---|
| 1 | GitHub repo, Unity project init, CI/CD, WebGL pipeline, hello-world deploy, Sprint 1 deck |
| 2 | Data schema design (with Anjani), state management architecture, scene management framework |
| 3 | Integration framework — glue between Anirban's UI and Akshat's 3D scenes; review all PRs |
| 4 | Mid-project integration push; prepare Sprint 4 concept-fix demo; oversight |
| 5 | CEE → DE handoff coordination; integrate surrogate equations; schema validation |
| 6 | Performance optimization, build size reduction (WebGL is bandwidth-sensitive) |
| 7 | Story mode logic, integration QA, plan user testing sessions |
| 8 | Beta freeze: final integration, deployment hardening, release notes |
| 9 | Final polish, defense rehearsal, report contributions, user guide |

**Overflow buffer:** picks up Akshat's overflow as needed.

### Anirban — UI/UX & Dashboards

| Sprint | Tasks |
|---|---|
| 1 | Wireframes (macro view, parameter panel, KPI dashboard); design system v0 |
| 2 | Hi-fidelity mockups in Figma; component library specs; info architecture |
| 3 | KPI widget specs → first Unity UI implementation (mass processed counter) |
| 4 | Full KPI dashboard widgets in Unity (mass, CO₂, energy, cost, recovery) |
| 5 | Parameter control panel UI in Unity (sliders, dropdowns, presets) |
| 6 | Country selector UI (map + dropdown) integrated with transport data |
| 7 | Story mode UI: tour navigation, step indicators, narration card |
| 8 | Polish, micro-animations, transitions, accessibility |
| 9 | Final QA, presentation polish, screenshots for report |

### Akshat — 3D & Process Scenes (heaviest contributor)

| Sprint | Tasks |
|---|---|
| 1 | Blender block models of 5 unit ops (feed mixer, heater, solid-gas separator, CS-2, output) |
| 2 | Detailed Blender models, materials, textures; export pipeline to Unity |
| 3 | Macro process scene assembly — the 2.5D schematic flowsheet view |
| 4 | Animations: shredded-blade particle flow, gas plume, char/glass output streams; heat glow on heater |
| 5 | Drill-down 3D scenes for each unit op (click → focus → richer view) |
| 6 | Interactive logic — parameter binding (e.g., temperature slider drives heater glow + animation speed) |
| 7 | Educational overlay system — info panels, chemistry equation cards, hover tooltips |
| 8 | **Plan B implementation** — the simpler 2D-only schematic version (parallel artifact) |
| 9 | Polish, bug fixes, performance tuning |

**Note:** Akshat's load is intentionally heavy. Ritwika monitors weekly and absorbs overflow (especially around Sprint 4 and Sprint 7).

### Sharan — UI Components (small, technical, bounded)

| Sprint | Tasks |
|---|---|
| 1 | Local Unity setup, repo onboarding, build splash screen scene + main menu (first PR) |
| 2 | Reusable UI prefab library — sliders, number inputs, dropdowns, info-buttons (used by Anirban + Akshat) |
| 3 | Loading screen + scene transition manager (clean async loading between scenes) |
| 4 | **CO₂ Translator widget** — takes CO₂ in kg, returns "X flights / Y cars / Z homes." Pure formula + UI panel. End-to-end ownership. |
| 5 | Continuation of CO₂ translator: integrate into process scene as hover overlays |
| 6 | Comparison toggle UI for "vs landfill" — before/after numerical display |
| 7 | Localization scaffolding (English/German switch infrastructure — strings table, key-value lookup) |
| 8 | QA on his own modules, bug fixes |
| 9 | Polish, accessibility on his components |

**Why these tasks:** each is technical (Unity scripting + UI binding), bounded in scope, has clear inputs/outputs, and gives Sharan visible end-to-end ownership of small modules he can succeed at.

### Anjani (CEE Lead) + Hari (CEE)

| Sprint | Tasks |
|---|---|
| 1 | Confirm scope alignment with DE; flag any gaps in existing Hyperion LCA data |
| 2 | **Detailed process presentation** (course requirement) — 5-stage process, all parameters, all flows, all data ranges. This is the science Sprint 2 demands. |
| 3 | Distill DWSIM model into surrogate equations — closed-form formulas for mass balance, energy demand, CO₂ output, char yield, glass yield. Document each: input → output, units, valid ranges. |
| 4 | Country-specific data: transport distances + diesel use for Germany / Netherlands / Denmark; electricity grid emission factors |
| 5 | **Validated JSON data schema delivered** — the formal CEE → DE handoff |
| 6 | Educational chemistry content for info panels (resin combustion, calcium silicate formation, etc., explained simply) |
| 7 | Validation/accuracy review of DE app's calculations against DWSIM/OpenLCA reference numbers |
| 8 | Final review of beta app; contribute CEE sections to preliminary report |
| 9 | Defense prep, finalize CEE sections of full report, contribute to user guide chemistry explanations |

---

## GitHub Repository Setup

**Suggested name:** `windecee-bladeforge`
**Owner:** Create a GitHub organization `windecee` (or use Ritwika's account with collaborators)

### Folder structure
```
windecee-bladeforge/
├── unity-project/        # Unity project root
├── blender-assets/       # Source .blend files + exports
├── ce-data/              # CEE deliverables (schema, equations, datasets)
│   ├── schema-v1.json
│   ├── surrogate-equations.md
│   └── country-data.json
├── plan-b/               # 2D-only fallback version
├── docs/
│   ├── sprint-reports/
│   ├── architecture.md
│   └── ce-handover.md
└── README.md
```

### Branching strategy
- `main` — production; auto-deploys to GitHub Pages on merge
- `develop` — integration branch; everyone targets this for PRs
- `feature/<area>-<short-name>` — e.g., `feature/dashboard-kpi-widgets`, `feature/scene-heater-drilldown`
- All PRs require 1 review; Ritwika reviews architecture-level changes

### CI/CD (GitHub Actions)
- On PR: build Unity WebGL → run as artifact preview
- On merge to `main`: build → deploy to GitHub Pages
- Live URL format: `https://windecee.github.io/windecee-bladeforge/`

### Linear integration
- Issues mirrored from your existing Linear board
- Use the same WIN-XX numbering you already have
- Each PR references its Linear ticket

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Akshat's workload too heavy | Sprint 4–7 slips | Ritwika absorbs overflow; Plan B remains 2D-only to reduce 3D burden |
| WebGL build size > 200 MB | Slow loads, bad demo | Aggressive texture compression from Sprint 2; lazy-load assets; performance budget in CI |
| CEE schema slips past Sprint 5 | DE blocked on real data | DE builds against mocks until Sprint 5; schema v0 drafted in Sprint 2 to de-risk |
| Sharan unable to deliver a module | UI gaps | Tasks deliberately small + bounded; Anirban shadows; Ritwika picks up |
| Unity version drift across team | Merge conflicts, broken builds | Locked Unity LTS version in Sprint 1; documented in README |
| Plan B never built | No fallback at defense | Akshat allocates explicit Sprint 8 time; same data schema as Plan A so it's not a separate codebase |
| Professor pushes back on dashboard-y feel | Concept rework | Sprint 1 framing emphasizes "process understanding" first, KPIs second |

---

## Plan B (parallel artifact)

A 2D-only animated schematic version of the same process, using the same JSON data schema and same surrogate equations. Only the rendering layer differs — instead of Unity 3D scenes, it's clean SVG/2D animations of the flowsheet with the same parameter panel, KPI dashboard, CO₂ translator, country selector, and comparison toggle. Owned by Akshat with Ritwika support.

---

## Key Dates

| Date | Event |
|---|---|
| 08.05.26 | Sprint 1 demo (Friday) |
| 22.05.26 | Sprint 2 — process in full detail |
| 19.06.26 | Sprint 4 — concept fixed |
| 03.07.26 | Sprint 5 — CEE → DE handoff |
| 17.07.26 | Sprint 6 — all flows implemented |
| 14.08.26 | Sprint 8 — beta freeze |
| 11.09.26 | Sprint 9 — final submission |
| Sept TBD | Defense |
