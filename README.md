# WinDECEE — BladeLoop

Interactive application that visualises the **thermal co-processing of decommissioned wind turbine blades** — a real chemical engineering process for recovering glass fibre from blade waste and routing the remaining carbon and glass to cement kiln co-processing.

Built for **DE-Project Summer Semester 2026** at Otto-von-Guericke University Magdeburg, a cooperative course between FIN (Computer Science) and FVST (Process & Systems Engineering).

🔗 **Live app:** https://ritwika-s11.github.io/windecee-bladeloop/app/BladeLoop_V1/

Runs in the browser — no install. Chrome or Edge on a desktop is the tested path; it needs WebGL 2 and roughly 45 MB of download on first load.

## What it does

You take the planner's seat: **take the order, set the plant, see what you made.**

1. **Place an order** — pick one of three worked examples, or build a custom order for a target grade and tonnage.
2. **Watch the run** — a narrated tour through the four stages: wind farm and transport, shredding, the rotary kiln, and separation. The panel on the right tracks your settings and outputs as they happen.
3. **Read the outcome** — the panel expands into a run report saying whether the order was filled, what the settings cost you, and where every output stream goes. Downloadable as a self-contained HTML file.

The point of the app is that **there is no single correct setting**. Temperature, retention, feed rate and particle size trade against each other, and the same plant serves a composite manufacturer, a precast concrete producer and a cement works differently.

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

- **Engine:** Unity 6 (6000.4.7f1), URP, Cinemachine 3, Timeline
- **3D Assets:** Blender
- **Deployment:** Unity WebGL → GitHub Pages via GitHub Actions
- **Science backbone:** DWSIM (process modelling), OpenLCA (impact assessment)

## Repository Structure

```
app/BladeLoop_V1/   the deployed WebGL build
unity-project/      Unity project root
blender-assets/     source .blend files and FBX exports
ce-data/            CEE deliverables — schema, equations, country data
docs/               architecture notes, VO scripts, handover docs
```

## Building

The WebGL build is committed to the repository and published automatically — pushing to `main` deploys the whole repo to GitHub Pages.

To produce a new build:

1. Open `unity-project/BladeLoop` in Unity 6000.4.7f1.
2. Switch platform to **WebGL** (the first switch reimports every asset and takes a while).
3. Build into **`app/BladeLoop_V1`**, overwriting it. The folder name determines the output filenames, so keeping it identical keeps the URL working.
4. Commit `app/BladeLoop_V1` and push to `main`.

Two things to know before you rebuild:

- **Player Settings → WebGL → Publishing Settings → Enable Exceptions** should be at least *Explicitly Thrown Exceptions Only*. Set to *None*, an unhandled exception aborts the WASM module and the page freezes with no message.
- Each build adds roughly **43 MB to git history permanently**. Rebuild deliberately, not casually.

A Windows player can be built from the same project with no code changes; the run report saves to the Desktop there, and downloads through the browser in WebGL.

## Notes for reviewers

- Narration is written to be **true at any setting**. No script quotes a yield or a temperature, because both move with the order — the panel and the on-screen labels carry the numbers instead, and they update per run.
- Output percentages come from `ProcessModel.OutputSplit()`. Glass fibre spans 30–72% and char 6–42% across the achievable settings, so a well-set and a badly-set run genuinely look different, not just read differently.
- Figures are model output for teaching, **not measurements from an operating plant**.

## License

MIT
