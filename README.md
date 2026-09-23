# WinDECEE — BladeLoop

Interactive application that visualises the **thermal co-processing of decommissioned wind turbine blades** — a real chemical engineering process for recovering glass fibre from blade waste and routing the remaining carbon and glass to cement kiln co-processing.

Built for **DE-Project Summer Semester 2026** at Otto-von-Guericke University Magdeburg, a cooperative course between FIN (Computer Science) and FVST (Process & Systems Engineering).

The deliverable is a **standalone Windows player**. Unpack the build folder and run `BladeLoop.exe` — no installer, no runtime, nothing to configure. It launches fullscreen at the desktop resolution; `Alt+Enter` windows it. A mouse with a scroll wheel is required, not optional: Explore mode orbits on left-drag and zooms on scroll.

> **Internal WebGL preview:** https://ritwika-s11.github.io/windecee-bladeloop/app/BladeLoop_V1/
> For the team only, and **currently stale** — the committed build is from 10 Sep and predates the Stage 4 retime, the outcome report, and the final Custom Order and shredder work. Rebuild before relying on it, or use the Windows player.

## What it does

You take the planner's seat: **take the order, set the plant, see what you made.**

1. **Place an order** — pick one of three worked examples, or build a custom order for a target grade and tonnage.
2. **Watch the run** — a narrated tour through the four stages: wind farm and transport, shredding, the rotary kiln, and separation. The panel on the right tracks your settings and outputs as they happen.
3. **Read the outcome** — the panel expands into a run report saying whether the order was filled, what the settings cost you, and where every output stream goes. Saves as a self-contained HTML file.

The point of the app is that **there is no single correct setting**. Temperature, retention, feed rate and particle size trade against each other, and the same plant serves a composite manufacturer, a precast concrete producer and a cement works differently.

Nothing in the run is prerecorded. The same scenes play for every order and each stage carries a *binder* — a component that reads the order and drives what the scene shows, from the size of the fragments on the belt to the emissive glow of the kiln and the emission rates of the oil, syngas and char streams.

## Team — WinDECEE (Group 4)

**Digital Engineering**
- Ritwika Sen — Project lead, architecture, order model, story mode
- Akshat Daruka — Custom Order screen and the trade-off frontier
- Sharan Murali — How It Works screen
- Anirban Maji — 3D scenes, materials and visual effects

**Chemical Engineering**
- Anjani Lohith Kosana
- Hari Krishna Kondam

## Tech Stack

- **Engine:** Unity 6 (6000.4.7f1), URP, Cinemachine 3, Timeline, Input System, TextMesh Pro
- **3D Assets:** Blender — source `.blend` files kept alongside the FBX exports
- **Targets:** Windows standalone (deliverable), WebGL → GitHub Pages (internal preview)
- **Science backbone:** DWSIM (process modelling), OpenLCA (impact assessment). Neither runs inside the app — they established the reference figures that `ProcessModel` is fitted to, and those enter the code as constants.
- **Report:** LaTeX, in `report/`

## Repository Structure

```
unity-project/       Unity project root
blender-assets/      source .blend files and FBX exports
report/              the project report (LaTeX) — main.tex, sections/, refs.bib, figures/
docs/                architecture notes, VO scripts, handover docs, CEE deliverables
assets/              sprint presentations (PDF and PPTX)
app/BladeLoop_V1/    the committed WebGL build — see the note above
ce-data/             empty; CEE material ended up in docs/
```

Worth knowing about `docs/`: it is the project's written memory and several files in it are load-bearing rather than archival.

- `BLADELOOP-PRODUCT-VISION.md` — the single source of truth for what the product is
- `interface-contract.md` — the shared names the screens code against
- `grade-threshold-reasoning.md` — why the grade thresholds are where they are, with sources. Read this before changing any threshold
- `final-task-division.md` — who owned what in the last sprint

## Building

### Windows player

1. Open `unity-project/BladeLoop` in Unity 6000.4.7f1.
2. Platform is **Windows, x86_64**.
3. Build to a folder outside the repository. The build is not committed — it is far too large for git history.
4. Ship the whole folder. `BladeLoop.exe` cannot find its data directory if it is moved out on its own.

The build is unsigned, so Windows shows a SmartScreen warning on first run (*More info → Run anyway*). The run report saves to the Desktop.

### WebGL

The WebGL build **is** committed, and pushing to `main` deploys the whole repository to GitHub Pages.

1. Switch platform to **WebGL** (the first switch reimports every asset and takes a while).
2. Build into **`app/BladeLoop_V1`**, overwriting it. The folder name determines the output filenames, so keeping it identical keeps the URL working.
3. Commit `app/BladeLoop_V1` and push to `main`.

Two things to know before you rebuild:

- **Player Settings → WebGL → Publishing Settings → Enable Exceptions** should be at least *Explicitly Thrown Exceptions Only*. Set to *None*, an unhandled exception aborts the WASM module and the page freezes with no message.
- Each build adds roughly **43 MB to git history permanently**. Rebuild deliberately, not casually.

The report download on WebGL goes through `Assets/Plugins/WebGL/BladeLoopDownload.jslib` rather than the file system, since a WebGL build has no disk. That path has **not been exercised in a deployed build** — check it if you rebuild.

## Working on this

**Scene files cannot be merged.** A Unity scene is serialised YAML keyed by generated file IDs; git will merge two edits into something Unity opens wrong rather than something it refuses to open. So **every scene has one owner**, and only that person edits it.

What makes that survivable is that most interface is built in **code, not the editor** — `MainMenuController`, `OrderDashboardController` and `OrderPanel` construct their layouts at runtime, and several components bootstrap through `[RuntimeInitializeOnLoadMethod]` and exist in no scene at all. Adding a button is a code change. Looking for the UI in the scene hierarchy will not find it.

Two traps that cost real time here, both of which fail silently:

- **Write choreography as fractions of its sequence, never in absolute seconds.** Retiming a timeline desynchronises anything keyed to a wall-clock time, and nothing throws.
- **`GameObject.Find` skips inactive objects.** Use `FindObjectsByType(..., FindObjectsInactive.Include, ...)`. Several particle systems start disabled.

`ProcessModel` has no Unity dependency and can be evaluated without a scene — which is how the constants were checked and how the mass balance is verified to close on every evaluation. Keep it that way.

## Notes for reviewers

- Narration is written to be **true at any setting**. No script quotes a yield or a temperature, because both move with the order — the panel and the on-screen labels carry the numbers instead, and they update per run.
- Output percentages come from `ProcessModel.OutputSplit()`. Glass fibre spans 30–72% and char 6–42% across the achievable settings, so a well-set and a badly-set run genuinely look different, not just read differently.
- Figures are model output for teaching, **not measurements from an operating plant**. The model's coefficients were chosen to behave the way the process behaves; they are not regressed from data. Report §1.6 sets out every assumption and where it comes from.
- "Purity" is a project definition — the mass fraction of recovered material that is fibre rather than char and resin residue. No published standard expresses recovered fibre quality this way.

## License

MIT
