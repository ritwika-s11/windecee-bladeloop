# Blender Visual Goals — Per Stage

Reference guide for the team modeling the 5 process stages. Use this as the bar to clear.

---

## Overall quality target (all stages)

What every stage should achieve before being called "done":

- **Primary shapes recognisable from 20 metres away** — someone glancing at the scene should know what each piece of equipment is without labels
- **Proper PBR materials** — at minimum: brushed steel for structural metal, painted steel (faded industrial colours — safety yellow, dark grey, faded blue) for housings, weathered concrete for foundations, glowing emission for heat zones
- **Believable proportions** — keep human-scale reference (a 1.8m human silhouette near the equipment helps calibrate)
- **Three-point lighting minimum** — key light, fill light, rim/back light. No flat lighting.
- **Subtle imperfection** — rust streaks, dirt at base, weathering on edges. Pristine = uncanny
- **Edge bevel modifier on every hard edge** — sharp 90° corners in render look CGI; 0.5–2mm bevel reads as real

Style direction: **photoreal industrial**, not stylised. Reference: documentary photography of working cement plants, not video game props.

---

## Stage 1 — Decommissioning & On-Site Pre-Treatment

**Inspiration image:** `assets/inspiration/stage-1-dismantling.jpg` (Komatsu excavator with hydraulic shears cutting a blade root section in an outdoor lot)

**What the Blender scene should show:**

A wind turbine blade section (cylindrical root visible, transitioning into the airfoil shape) lying on a wooden pallet base in a dirt/gravel field. A tracked excavator with hydraulic shear attachment positioned beside it, jaws engaging the blade. Stacked blade sections in the background suggesting an accumulation yard.

**Key modelling targets:**

- **Blade root section** — large hollow cylindrical end with visible bolt flange (this is the part that connects to the hub — circular ring with ~50–80 bolt holes)
- **Blade airfoil transition** — the asymmetric curved cross-section. This is the hardest shape. Use a lofted curve from circle to airfoil profile.
- **Excavator** — tracked base, articulated arm with three pivot points, shear attachment with two large blade jaws. The shear can be simplified — just the silhouette reads correctly.
- **Material weathering** — blade has cream/white gel coat with wear; metals are unpainted with chipped paint patches and rust
- **Ground** — gravel/dirt texture (use a procedural displacement)

**Lighting:** outdoor daylight, slightly overcast — long soft shadows. HDRI from PolyHaven ("kloofendal_overcast_puresky" or similar).

**Reference search terms** for finding more: *wind turbine blade decommissioning blender*, *Komatsu shear 3D model*, *GFRP blade hollow section render*.

---

## Stage 2 — Mechanical Shredding & Processing

**Inspiration image:** `assets/inspiration/stage-2-shredder.jpg` (twin-shaft industrial shredder shown from above, mid-shred, blade panels feeding in)

**What the Blender scene should show:**

A heavy industrial twin-shaft shredder seen from a 3/4 angle. Top-opening feed hopper with shredded GFRP material visible at the entry. Twin rotating shafts (counter-rotating, with interlocking hooked blades) at the core. Output conveyor beneath. Separate magnetic conveyor to one side for metal extraction. Industrial floor with grating.

**Key modelling targets:**

- **Shredder housing** — heavy welded steel box, painted industrial yellow or dark grey. Bolted access panels.
- **Twin shafts with hooked blades** — this is the hero of this stage. ~12–16 hooked teeth per shaft, alternating. Use array modifier on a curved blade prefab.
- **Feed hopper** — angled steel walls, weathered at the top edge from material impact
- **Output conveyor belt** — rubber belt over rollers, with shredded GFRP material modeled as scattered small irregular chunks
- **Magnetic separator** — visible cross-belt magnet positioned above the conveyor, separated metal scraps falling to a side bin

**Lighting:** indoor industrial — overhead bay lights, slight green-grey tinge from fluorescent. Volumetric dust haze in the air.

**Reference search terms:** *industrial shredder 3D model render*, *twin shaft shredder blender*, *GFRP shredder visualisation*.

---

## Stage 3 — Thermal Treatment & Decomposition (Kiln Zone)

**Inspiration image:** `assets/inspiration/stage-3-kiln.jpg` (rotary kiln with visible drying, calcining and clinkering zones labelled, with white-hot flame at the discharge end)

**What the Blender scene should show:**

The flagship visual of the entire process. A long inclined rotary kiln (~3m diameter, ~50m long in real life — your model can be foreshortened) supported on multiple riding rings and roller stations. White-hot flame visible at the discharge end. Steel shell with visible refractory brick lining (where exposed). Heat shimmer/glow on the shell surface. Preheater tower at the elevated feed end.

**Key modelling targets:**

- **The kiln tube itself** — long cylinder with subtle taper, slightly inclined. Use a Bevel modifier with weighted normals for the riding rings (raised circular bands every ~10m).
- **Riding rings + roller stations** — 3–4 along the kiln length, each ring rests on two angled rollers in a concrete-foundation cradle
- **Flame at discharge end** — emission material with orange-to-white gradient. This is your hero shot — get it right.
- **Heat glow on shell** — use emission with a gradient (cool grey at feed end → red-hot near discharge). Subtle volumetric haze around the hot section.
- **Drive system** — large gear ring around the kiln circumference, with a smaller drive gear engaging it, motor on a steel pedestal
- **Concrete piers** — supporting each roller station, weathered with stains

**Lighting:** outdoor industrial, late afternoon (warm light). The flame should be the brightest thing in the scene — let other elements fall into shadow. HDRI like "industrial_sunset" or "industrial_workshop_foundry".

**Reference search terms:** *cement rotary kiln 3D model*, *industrial kiln blender render*, *rotary kiln cinema 4d*, *clinker production visualisation*.

This is the most visually impactful stage — invest the most modelling time here.

---

## Stage 4 — Solid–Gas Separation & Fine Classification

**Inspiration image:** `assets/inspiration/stage-4-plant.jpg` (cement plant cyclone preheater tower with associated piping)

**What the Blender scene should show:**

A multi-stage cyclone preheater tower — typically 4–5 stacked cyclone separators in a vertical tower, connected by ductwork. Each cyclone is a cylindrical top + conical bottom. Steel structural frame around the cyclones. Inlet duct from the kiln (carrying hot gas), outlet pipe going to the stack. Electrostatic precipitator or baghouse filter shown as a large rectangular housing adjacent.

**Key modelling targets:**

- **Cyclone stack** — 4 cyclones at vertically descending positions, decreasing slightly in diameter. Each: cylindrical top + steep cone below + vortex finder pipe going down the center
- **Connecting ductwork** — wide steel pipes connecting cyclone outlets to next stage inlets. Insulated (add a thicker outer cladding cylinder, painted aluminum or galvanised).
- **Structural steel frame** — open-truss tower around the cyclones, painted safety yellow or red oxide primer. I-beams, cross-bracing.
- **Filter house** — large rectangular box housing the baghouse, with a sloped roof and an array of vertical filter bag access doors on top
- **Stack** — tall vertical stack (the chimney) at the top, with subtle steam plume

**Lighting:** outdoor, mid-morning, slight haze. Sky HDRI. The cyclone tower should silhouette nicely against the sky.

**Reference search terms:** *cement plant cyclone preheater 3D model*, *industrial cyclone separator render*, *preheater tower blender*, *baghouse filter 3D*.

---

## Stage 5 — Material & Energy Substitution (Clinker Synthesis)

**Inspiration image:** Use the same plant image as Stage 4 (clinker line is part of the same facility) — `assets/inspiration/stage-4-plant.jpg`

**What the Blender scene should show:**

The discharge end of the kiln connecting to the **clinker cooler** — a long, broad steel structure with a grate floor where hot clinker tumbles out and is cooled by upward-blowing air fans. Clinker (small grey-brown nodules, the size of marbles, glowing red at the kiln end) visible on the grate. Output conveyor carrying cooled clinker to storage silos. Tall cylindrical clinker silos (3–4 of them, often white or grey concrete with vertical ribs).

**Key modelling targets:**

- **Clinker cooler housing** — large rectangular steel-clad enclosure, with vents on the sides and exhaust ducts on top. Inclined slightly downward toward the output.
- **Grate floor with clinker** — array of small rounded clinker particles (use a particle system with a small clinker mesh). Glowing red-orange at the kiln end, fading to dark grey at the output end.
- **Cooling fans** — large axial fans visible underneath the cooler, with ducting
- **Clinker conveyor** — covered chain conveyor or apron conveyor leading from the cooler to the silos
- **Clinker silos** — 3–4 tall vertical cylindrical silos with conical bottoms, painted white or in raw concrete with vertical reinforcing ribs

**Lighting:** outdoor industrial, afternoon. The glowing clinker at the kiln-end is the hero element — let it cast warm light on nearby surfaces.

**Reference search terms:** *clinker cooler 3D model*, *cement clinker production render*, *cement silos blender*, *clinker grate cooler*.

---

## Where to source free high-quality 3D references

Once SketchFab integration is enabled in your Blender (see instructions below), you can browse and import models directly. Without it, search these sites for free reference models:

- **Sketchfab.com** — search any of the keywords above; many free downloadable models
- **PolyHaven.com** — free PBR materials and HDRIs (excellent for industrial textures + lighting)
- **TurboSquid** (filter: free) — older but good for industrial equipment
- **CGTrader** (filter: free) — similar to TurboSquid
- **GrabCAD** — engineering-grade CAD models, useful for accurate mechanical parts

## Suggested asset budget per stage

To keep WebGL build size reasonable:

- **Each stage scene: under 500K triangles total**
- Use texture maps at 2K resolution (1K for distant elements)
- Reuse materials across stages (one "industrial steel" material with parameter variation)
- Bake high-poly detail to normal maps for the hero pieces (kiln flame area, shredder blades)
- Hide back-faces and interior geometry the camera never sees

---

## Workflow per stage (suggested)

1. **Block-in phase** — what we have now. Primitives in correct positions.
2. **Detail pass** — add modifiers (bevel, subdivision surface for organic curves), add secondary elements (pipes, ladders, railings)
3. **Material pass** — apply PBR materials, dirt/grime/rust through procedural masks
4. **Lighting pass** — set up HDRI + key/fill/rim lights, fine-tune exposure
5. **Composition pass** — final camera angle, depth of field, post-processing in compositor (slight bloom on the hot zones, subtle vignette)
6. **Optimisation pass** — decimate where possible, bake textures, check WebGL export

Don't try to do all five passes in one go for one stage and then start the next. Do block-in for all five first, then detail pass for all five, etc. Keeps the team in sync visually.
