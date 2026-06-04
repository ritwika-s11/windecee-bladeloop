# Project Context Dossier — WinDECEE / BladeLoop · For Akshat's Claude

> Complete project briefing for the team member handling 3D assets and Unity scene assembly. Read this entire document before starting any work. The Blender scene and reference renders mentioned here are already prepared and saved to the workspace.

---

## 1. The Course

**DE-Projekt: Digital Engineering of Process Engineering Applications** — Otto-von-Guericke University Magdeburg (OvGU), Summer Semester 2026. Cooperative course between:

- **FIN** — Faculty of Computer Science (Digital Engineering students)
- **FVST** — Faculty of Process & Systems Engineering (Chemical Engineering students)

**Course brief (verbatim):** *"Development of an interactive online application (APP) that enhances the understanding of a specific chemical engineering process."*

**Past projects pattern:** All previous course apps are Unity 3D scenes showing one process — animated equipment, controllable parameters, info bubbles, educational scaffolding. Typical pipeline: **Blender → Unity → WebGL build → Windows + Android executables.** This is the proven path; we are following it.

**Credit weighting:** DE students get 12 credits each, CEE students 4 — DE owns roughly 75% of the workload. Akshat (the user of this prompt) is on the DE side.

**Timeline up to now:**

| Date | Milestone |
|---|---|
| 24.04.26 | Kickoff |
| 08.05.26 | Sprint 1 — concept pivot, work plan, deployment pipeline live |
| 22.05.26 | Sprint 2 — process presented in full detail (where we are now) |
| 05.06.26 | Sprint 3 — coming up: macroscopic Unity scene starts |
| 19.06.26 | Sprint 4 — concept fix, macroscopic design implemented |
| 03.07.26 | Sprint 5 — CEE → DE handoff (validated data schema) |
| 17.07.26 | Sprint 6 — all flows implemented |
| 14.08.26 | Sprint 8 — beta freeze |
| 11.09.26 | Sprint 9 — final submission |

---

## 2. The Team — WinDECEE (Group 4)

| Name | Role | Owns |
|---|---|---|
| Ritwika Sen | Project Lead · Architecture | Build pipeline, GitHub repo, deployment, integration, oversight |
| **Akshat Daruka** | **3D Scenes · Animations** | **(this is you) Blender modelling, Unity scene assembly, animations, drill-down scenes, educational overlays, Plan B** |
| Anirban Maji | UI/UX · Dashboards | Wireframes, Figma mockups, KPI widgets, parameter panel, country selector UI, story mode UI |
| Sharan Murali | UI Components | Splash/menu scenes, reusable UI prefabs, CO₂ translator widget, comparison toggle, localization |
| Anjani Lohith Kosana | CEE Lead | Process modelling, LCA, surrogate equations, validation |
| Hari Krishna Kondam | CEE | Transport data, materials, chemistry educational content |

**Akshat's role intentionally has the heaviest creative load.** Ritwika absorbs overflow if needed.

---

## 3. The App We're Building — BladeLoop

### One-line concept

An interactive online application where a non-expert user walks through a 3D wind turbine blade recycling plant, adjusts process parameters, and learns how blade waste becomes cement industry feedstock — while seeing live human-scale impact numbers (CO₂, energy, cost).

### Why this concept (the pivot story)

At kickoff the team initially proposed a comparative dashboard showing three recycling routes side-by-side. After reviewing the course brief and past projects, we realised it was leaning more towards a BI dashboard than a process visualisation — so we pivoted. The pivoted BladeLoop keeps the spirit but the core is now a single specific chemical engineering process (thermal co-processing of decommissioned wind turbine blades) visualised so anyone, even without chemistry background, can understand it.

### The five process stages

1. **Decommissioning & On-Site Pre-Treatment** — hydraulic shears cut blades at the wind farm into transportable segments
2. **Mechanical Shredding & Processing** — twin-shaft industrial shredder reduces blade segments to 20–30mm GFRP granules; magnets pull out metals
3. **Thermal Treatment & Decomposition** — rotary kiln at 850–1450°C cracks the composite into mineral phase + volatile gas phase
4. **Solid–Gas Separation & Fine Classification** — cyclone separators + electrostatic precipitators clean the gas, homogenise the mineral stream
5. **Material & Energy Substitution (Clinker Synthesis)** — recycled minerals replace sand/bauxite in cement clinker; heat replaces coal

Per 5.7 tonnes blade waste: outputs include 4,415 kg mineral slag (cement substitute), 342 kg coal avoided, 177 kg SiO₂ + 32 kg Al₂O₃ raw material avoided.

### The app's three views

- **Process view** — 2.5D macro flowsheet, click any stage to drill into a detailed 3D scene
- **Dashboard view** — KPI cards (mass, CO₂, energy, cost, recovery) + human-scale CO₂ translator ("≈ 42 flights Berlin → New York")
- **Story mode** — 5-minute guided tour through all 5 stages, free exploration unlocks after

Plus layers: country selector (Germany / Netherlands / Denmark adjusting transport leg), vs landfill comparison toggle.

---

## 4. Locked Architecture Decisions (don't relitigate)

- **Engine:** Unity 6.4 (6000.4.7f1) — same exact version across the team
- **Primary deployment:** WebGL build → GitHub Pages (already live at `ritwika-s11.github.io/windecee-bladeloop`)
- **Secondary deployment:** Windows `.exe` build in parallel (one extra build target, used for live demo at sprint reviews and defense)
- **Compression format for WebGL:** **Disabled** or **Gzip** (NOT Brotli — GitHub Pages doesn't serve Brotli, breaks loading)
- **Computation strategy:** Surrogate equations from CEE (~10–20 closed-form formulas) — NOT embedded DWSIM
- **Data contract:** Versioned JSON schema locked by Sprint 5 (the formal CEE → DE handoff)
- **Visual fidelity strategy:** Hybrid — macro view is 2.5D schematic flowsheet, click into a unit op to drill into richer 3D scene
- **Code editor:** Visual Studio Code with GitHub Copilot (free via Student Pack) + Microsoft Unity extension
- **Repository:** GitHub at `github.com/ritwika-s11/windecee-bladeloop`, branch strategy is `main` (auto-deploys) ← `develop` (integration) ← `feature/*` per-area branches
- **Project tracking:** Linear (already initialised with WIN-XX issue numbering)
- **Language:** English MVP, German is a Sprint 9 polish task — wire localisation hooks from day one
- **Plan B (required by course):** 2D-only SVG version of the same flowsheet with same data schema — owned by Akshat in Sprint 8 as parallel artefact

---

## 5. What's Ready in Blender Right Now

The Blender scene file is saved at:

```
/Users/ritwikasen/Desktop/Digital Engineering/Summer 2026/WinDECEE/blender-scene/BladeLoop-Sprint2.blend
```

It contains **1,389 named objects** organised by stage prefix:

### Stage 1 — Decommissioning (59 custom-built objects, prefix `S1_*`)
- Wind turbine blade: root flange section with 12 bolts, transition section, main airfoil section, tip taper
- Excavator: tracks (L/R) with wheels, lower body, upper rotating body, operator cab with glass windows, exhaust pipe, articulated boom + stick with hydraulic cylinders, hydraulic shears (body + upper jaw + lower jaw + pivot pin)
- Wooden pallet with three support blocks
- Ground patch (dirt material)
- 10 scattered blade debris pieces
- 4 background blade stack cylinders
- Dedicated key light + fill light

### Stage 2 — Shredder (114 custom-built objects, prefix `S2_*`)
- Concrete floor plate
- Main shredder housing with bolted access panel and 8 rivets
- Four-walled feed hopper (front, back, left, right) with 6 visible blade pieces being fed
- Twin counter-rotating shafts with 8 hooked teeth each (16 total)
- Drive motor with red end cap
- Magnetic separator: housing, support arm, side bin for metals, 5 metal scraps
- Output conveyor: belt, frame, two end rollers, two side rails
- 35 shredded GFRP granules on the belt
- 20 output pile pieces at conveyor end
- 4 yellow leg supports

### Stage 3 — Rotary Kiln (24 custom-built objects, prefix `S3_*`)
- Main kiln tube (long inclined cylinder with dark industrial steel + rust noise shader)
- Hot zone band near discharge (emission glow material)
- 3 riding rings around kiln
- 6 roller stations (2 per ring, L/R)
- 3 concrete piers under roller stations
- Drive gear ring around kiln + drive motor + pinion gear
- Flame outer envelope + flame core (emission strength 25-50)
- Discharge hood
- Feed-end housing + feed duct (preheater connection)
- Ground pad
- Flame point light (5000W energy)

### Stage 4 — Cyclone Separator
- Imported from SketchFab: "Cyclone dust separator" by RealShmell (UID `0e4b22a07d8c44208fa46b49ee7ebe9a`)
- Root: `S4_CycloneSeparator_Root` at position (10, 0, 0), scaled to 4m tall
- Production-grade blue painted cyclone with structural frame and conical bottom
- CC Attribution licence — **must credit RealShmell in the app's about/credits section**

### Stage 5 — Clinker Silo
- Imported from SketchFab: "Industrial_Silo" by solankidhruv123 (UID `5a63e69b6469487ebee813c9120fe5b7`)
- Root: `S5_ClinkerSilo_Root` at position (16, -1, 0), scaled to 7m tall
- Heavily weathered concrete silo with rivets, panels, yellow safety ladders/railings, support legs
- CC Attribution licence — **must credit solankidhruv123 in the app's about/credits section**

### Lighting & Camera

- Sun light (key, warm afternoon tone, 4.0 energy)
- Area fill light (cool sky bounce, 800 energy)
- Spot rim light (warm backlight, 500 energy)
- Single camera `S1_Camera` with depth-of-field configured

### Reference Goal Renders (already saved)

Located at `/Users/ritwikasen/Desktop/Digital Engineering/Summer 2026/WinDECEE/assets/blender-goals/`:

- `stage-1-decommissioning.png`
- `stage-2-shredder.png`
- `stage-3-kiln.png`
- `stage-4-cyclone.png`
- `stage-5-silo.png`
- `process-overview-all-stages.png` ← shows all 5 stages in one wide shot

These represent the visual bar for each stage. Stages 4 and 5 are SketchFab quality; Stages 1, 2, 3 are intermediate-detail starter scenes that need refinement.

---

## 6. Akshat's Immediate Tasks (Sprint 3 starts 05.06.26)

### Task 1 — Get a clean Unity project set up

1. In Unity Hub, create a new project from the **3D (Universal Render Pipeline)** template using **Unity 6.4 (6000.4.7f1)**
2. Name it `windecee-bladeloop-unity`
3. Place inside `/Users/ritwikasen/Desktop/Digital Engineering/Summer 2026/WinDECEE/windecee-bladeloop/unity-project/`
4. **Build settings:** switch platform to **WebGL** as default (File → Build Settings → WebGL → Switch Platform). This forces all assets to import WebGL-compatible from day one.
5. **Player Settings:**
   - Resolution: 1280 × 720 default canvas size
   - **Publishing Settings → Compression Format → Disabled** (or Gzip, NOT Brotli)
   - Memory size: 512 MB initial heap
6. Set up the folder structure inside `Assets/`:
   ```
   Assets/
   ├── 3D/
   │   ├── Stage1_Decommissioning/
   │   ├── Stage2_Shredder/
   │   ├── Stage3_Kiln/
   │   ├── Stage4_Cyclone/
   │   ├── Stage5_Silo/
   │   └── Shared/             ← shared materials, prefabs
   ├── Scenes/
   │   ├── MainMenu.unity
   │   ├── ProcessView.unity   ← macro 2.5D flowsheet (Anirban's UI lives here)
   │   ├── Stage1_Drill.unity  ← drill-down 3D scene for Stage 1
   │   ├── Stage2_Drill.unity
   │   ├── Stage3_Drill.unity
   │   ├── Stage4_Drill.unity
   │   └── Stage5_Drill.unity
   ├── Scripts/
   ├── Materials/
   └── Textures/
   ```

### Task 2 — Export each stage from Blender to FBX

Open the `.blend` file. For each stage:

1. Select all objects with that stage's prefix (e.g. type `S1_*` in the Outliner search, then Select All)
2. **File → Export → FBX (.fbx)**
3. In the FBX export dialog:
   - Selected Objects: ✅
   - Transform → Apply Scalings: **FBX All** (avoids Unity scale issues)
   - Transform → Forward: `-Z Forward`
   - Transform → Up: `Y Up`
   - Geometry → Smoothing: `Face`
   - Geometry → Apply Modifiers: ✅
4. Save filename as `Stage1.fbx`, `Stage2.fbx`, etc., into the corresponding Unity folder

For the SketchFab Stage 4 and Stage 5 — same process, but select `S4_CycloneSeparator_Root` (and its children) and `S5_ClinkerSilo_Root` respectively.

### Task 3 — Import to Unity

Drag each FBX into its Unity 3D/StageN/ folder. For each:

1. Set **Scale Factor: 1.0** (we exported with applied scalings)
2. **Generate Materials: ✅** (creates a baseline)
3. Materials → **Search Outside Material**, **Recursive-Up Material Search**
4. Drag the imported prefab into the corresponding `StageN_Drill.unity` scene
5. Verify the model orientation — Y is up in Unity, Z is forward; the kiln should lie horizontally, the silo should stand vertically

### Task 4 — Materials in Unity (URP)

URP doesn't directly import Blender's Principled BSDF setups. For each stage:

1. Create a baseline URP/Lit material per surface type (`MAT_Steel_Industrial`, `MAT_Concrete_Weathered`, `MAT_GFRP_Blade`, `MAT_Yellow_Painted`, etc.) — store these in `Assets/3D/Shared/Materials/`
2. Match the Blender BSDF settings: Albedo color, Metallic, Roughness (Unity calls it Smoothness — invert: 1 - roughness)
3. For SketchFab models, the FBX import should bring textures along — assign them to the URP/Lit material's Base Map, Metallic, Normal, Occlusion slots
4. For the flame in Stage 3: use **URP/Unlit** material with HDR emission color set to bright orange (R:5, G:2, B:0.5)

### Task 5 — Lighting per drill-down scene

Don't copy Blender lights directly — set up Unity-native:

- One **Directional Light** acting as sun (warm tint 1.0, 0.95, 0.85, intensity 1.0–1.5)
- Skybox: use Unity's procedural sky for now, swap to HDRI in Sprint 7 polish pass
- **Reflection Probe** at scene centre, baked, to give materials proper reflections
- For Stage 3: add a **Point Light** at the flame position (orange tint, intensity 8.0, range 12m, shadows: soft)
- Enable Post-Processing volume with subtle bloom (threshold 1.2, intensity 0.5) — makes the flame and hot zone pop

### Task 6 — Set up the macro Process view

This is the 2.5D flowsheet (the "macro" view from the wireframes, what users see first). Anirban is owning the UI layer but the 3D layout is yours:

1. Create `ProcessView.unity`
2. Place all 5 stage models in a horizontal row along the X axis (this matches the Blender scene layout already)
3. Set up an orthographic-ish perspective camera at ~30° tilt, slow orbit-able
4. Each stage gets a clickable trigger collider — clicking transitions to the corresponding `StageN_Drill.unity` scene (use Unity's `SceneManager.LoadScene` with a fade transition)
5. The UI overlay (parameter panel, KPI bar, country selector) goes on a separate Canvas at this level — Anirban will build the prefabs, you wire them in

### Task 7 — First milestone for Sprint 3 demo (05.06.26)

By Sprint 3 demo, you should be able to:

1. Launch the Unity project and the WebGL build
2. Show all 5 stages laid out in the ProcessView scene with their imported FBX models
3. Click any stage and have it transition to that stage's drill-down scene
4. The drill-down shows the model at a closer camera angle with basic lighting

That's the bar. UI parameter wiring and live KPIs come in Sprint 4–5 once the CEE schema lands.

---

## 7. References You Should Look At

All in the project workspace (`/Users/ritwikasen/Desktop/Digital Engineering/Summer 2026/WinDECEE/`):

- `sprint-plan.md` — full per-person sprint allocation across all 9 sprints
- `index.html`, `sprint-2.html` — the live website (with Sprint 1 and Sprint 2 decks embedded)
- `assets/wireframes/` — Anirban's wireframes for Process view, Dashboard view, Story mode
- `assets/blender-goals/*.png` — the 6 reference renders described above
- `cee-handover.md` — what the CEE team is delivering and when
- `anirban-claude-context.md` — Anirban's parallel context dossier (for reference)
- `blender-visual-goals.md` — detailed per-stage modelling targets with quality bar definitions
- `Gate_III_submission_HYPERION_LCA of Wind Turbine recycle.pdf` (in uploads) — the scientific foundation from the CEE team's prior work; contains DWSIM diagrams, mass balances, all the numbers

---

## 8. Things to Avoid (Common Traps)

- **Don't use Unity's Standard render pipeline** — we committed to URP. Mixing pipelines breaks WebGL.
- **Don't enable Brotli compression on the WebGL build** — GitHub Pages can't serve it; the app fails to load with a console error.
- **Don't try to embed Blender models with vertex colors as a substitute for textures** — Unity handles them but WebGL builds bloat. Use proper URP materials.
- **Don't go above 100 MB total build size** if you can help it — GitHub Pages soft limit is 1 GB but browsers hate large WebGL loads. Compress textures to 2K max, decimate high-poly imports.
- **Don't auto-import the silo's full 1,170 sub-meshes** — flatten/decimate in Blender first using the **Decimate** modifier or by selecting → Mesh → Cleanup → Decimate Geometry. Target <50k triangles for the silo.
- **Don't put logic in MonoBehaviours scattered across GameObjects** — use a central `ProcessController` ScriptableObject for the data schema once CEE delivers it.
- **Don't model Stage 3 from scratch ignoring my custom build** — refine what's there (especially the kiln tube material and flame), don't restart.

---

## 9. What Success Looks Like by End of Sprint 3

A live WebGL build deployed to `ritwika-s11.github.io/windecee-bladeloop/app/` showing the 5-stage process layout, clickable transitions between scenes, basic materials applied, basic lighting. No KPI logic yet, no UI wiring yet — just the 3D backbone working end-to-end through the build pipeline. That alone is a massive sprint deliverable and the foundation for everything else.

If you achieve that by Sprint 3, the project is on track. If not, escalate to Ritwika early — overflow plan exists.

---

## 10. One Last Note

The Blender scene currently has 1,389 objects. Most are SketchFab sub-meshes that don't need to live in the Unity project. **Before exporting, do a heavy decimation pass** on the silo and cyclone — Unity doesn't need every bolt to be a separate mesh. Aim for the silo at <50k triangles and the cyclone at <20k. Use Blender's Decimate modifier with Collapse → ratio 0.3 to start, eyeball the result, adjust.

The Blender file is also already added to the team repo at `WinDECEE/windecee-bladeloop/blender-assets/` — pull latest, open, follow the workflow above. Good luck.
