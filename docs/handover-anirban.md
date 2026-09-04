# Claude Brief â€” Anirban

**Read `docs/BLADELOOP-PRODUCT-VISION.md` first**, especially Â§6b and Â§7.
*Rewritten Wed 2 September. This replaces everything you were given on 31 August.*

Branch off current `main`: `feature/grade-visuals`
Feature freeze **Wed 9 Sep** Â· sprint review **Fri 11 Sep**

---

## Your job, and why it is the most important thing left

**Make the plant visibly different for each grade.**

Everything else is built. Pick an order today and the panel on the right correctly shows
600 Â°C / 2 mm or 550 Â°C / 16 mm â€” and the plant behind it looks **exactly the same either way.**

That is the professor's original criticism, still unanswered. The panel proves the numbers changed.
Only you can prove the *plant* changed. **Nothing else in the project matters as much this week.**

When you are done, someone across the room should be able to tell a high-grade run from a low-grade
run without reading a single number.

---

## ðŸ”´ Read this before you open Unity â€” the scene rule changed

Ritwika now owns the tour: shot lists, timelines, narration, audio. **Her work and yours live in the
same five `.unity` files**, and Unity scene files cannot be merged. If you are both in
`Stage2_StoryMode.unity` this week, one of you loses a day.

**So you get one scene pass, today, and then you stay out.**

1. **Batch every scene edit you will need into a single pass**: add one `StageParameterBinder`
   component per stage, plus any scene-level bug fixes you already know about.
2. **Push it immediately** â€” a small, quick PR.
3. **After that, the stage scenes are Ritwika's.** All your remaining work â€” tuning how much the
   char stream grows, how dim the kiln goes, how coarse the granules look â€” happens in the `.cs`
   file, with no scene edit at all.
4. If you later need a scene change, **message Ritwika. Do not open the file.**

Think hard before step 2 about what you will need. Getting back in costs a coordination round trip.

---

## The pattern: one hook in the scene, all behaviour in code

Create `Assets/Scripts/StageParameterBinder.cs`. One component, added once per stage scene, which
on `Start()` finds the objects it needs by name and drives them from `OrderContext.Model`.

This is the same pattern `OrderPanel` and `TourViewportFrame` already use, and it exists precisely
so the scene diff stays tiny and the file stays mergeable.

```csharp
public class StageParameterBinder : MonoBehaviour
{
    public enum Stage { Farm, Shred, Kiln, Separate }
    public Stage stage;                    // set once, in the scene

    void Start()
    {
        if (!OrderContext.HasOrder) return;   // free play must look exactly as it does today
        var m = OrderContext.Model;
        switch (stage) { /* find objects by name, apply m */ }
    }
}
```

Everything after that is editing this one file.

---

## Paste this to your Claude

> I'm working on BladeLoop, a Unity 6 (6000.4.7f1) URP project. Read
> `docs/BLADELOOP-PRODUCT-VISION.md` for product context, then `docs/handover-anirban.md` for my
> tasks.
>
> My job is making the plant visibly respond to `OrderContext.Model` â€” the four plant settings.
> The critical constraint: I get ONE pass of scene edits, then all further work must be in C#,
> because Ritwika owns those scene files from now on and Unity scenes cannot be merged.
>
> Project constraints:
> - **New Input System only.** `OnMouseDown` and the legacy `Input` class never fire.
> - UI positions live in `RectTransform.anchoredPosition`, not `transform.position`.
> - `GameObject.Find` ignores inactive objects â€” use
>   `FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)`.
> - **Never edit a shared material asset to change how one scene looks.** `WF_Mat_Terrain` is shared
>   with the home page through the wind farm FBX; editing it changed both scenes, and multiplying a
>   Color scales alpha along with rgb. Use a `MaterialPropertyBlock`.
> - Guard everything with `if (OrderContext.HasOrder)` â€” free play must be unchanged.
>
> Work one stage at a time and check the Unity console after each change.

---

## Priority order â€” do Stage 4 first

### 1. Stage 4 â€” the output split ðŸŽ¯ the money shot

`Stage4_V2.unity`. Three particle systems already exist:

```
EL_PS_FibreToBox      â†’ fibre stream into the fibre box
EL_PS_CharToDrum_0    â†’ char stream into drum 0
EL_PS_CharToDrum_1    â†’ char stream into drum 1
```

Drive their **emission rates** from the split:

```csharp
var split = OrderContext.Model.OutputSplit();
// GlassPct: 69.0 (high) â†’ 58.6 (mid) â†’ 46.5 (low)
// CharPct :  5.9 (high) â†’ 15.5 (mid) â†’ 26.5 (low)   â† more than four times
```

| Run | Fibre stream | Char streams |
|---|---|---|
| High | strong, steady, clean | barely a trickle |
| Mid | still strong | clearly visible char flow |
| Low | thinner | heavy â€” obviously the dominant product |

**This single contrast is the product's whole argument.** On a low-grade run the char drums should
be visibly busy while the fibre box fills slowly. Do this before anything else; if only one thing
lands this week, make it this.

If time allows: tint the fibre particles by `FiberPurityPct` (93.0 â†’ 82.5 â†’ 69.8), so high grade
reads clean off-white and low grade reads grey and dirty.

### 2. Stage 2 â€” particle size

`S2_OutputPile_0` â€¦ `S2_OutputPile_19` already exist, framed by `CAM_S2_04_OutputGranules`.

| `ParticleSizeMm` | Granules |
|---|---|
| 2 mm (high) | small, uniform, dense â€” coarse sand |
| 8 mm (mid) | visibly chunkier, more irregular, gaps appearing |
| 16 mm (low) | clearly chunky flakes, loose pile |

Scale each by roughly `particleSizeMm / 2f`, vary each Â±25 % with a **fixed seed** so it looks like
shredded material rather than 20 identical cubes, hide some at larger sizes so the pile volume stays
believable, and add random rotation â€” coarse shred is angular.

Particle size carries weight **0.32** in the model, more than temperature, so this is the second
most valuable stage.

### 3. Stage 3 â€” temperature and retention

`TemperatureRampAnimator.cs` ramps to a hardcoded `tempEnd = 620f`, driving the kiln shell colour,
burner ring, nozzles and a `TextMeshPro` label.

> `KilnVisualizer.cs` with its tidy `SetHeat()` is **not in Stage 3** â€” it belonged to the dashboard
> kiln, which has been removed. Don't go looking for it.

- Set `tempEnd = OrderContext.Model.TempC` in `Start()` when an order is active. The existing colour
  lerp and label then follow automatically.
- **Widen the colour range** so 550 and 600 look genuinely different: 600 bright even orange,
  580 duller and redder, 550 visibly under-fired. Judge by eye.
- `KilnRotator.rpm`: `1.5f * (35f / RetentionMin)`. Keep it subtle.

### 4. Stage bug fixes

Anything you already know is wrong in these scenes â€” **fold it into the single scene pass.** After
that, send Ritwika the fix rather than opening the file.

### 5. Stage 1 â€” blade count *(only if 1â€“4 are done)*

`OrderContext.BladesNeeded` and `TurbinesNeeded` are live (about 616 blades, 205 turbines). Reflecting
that in the wind farm would be a nice opening beat. Low priority.

---

## What you must not break

- **Free play must be unchanged.** Guard every change with `if (OrderContext.HasOrder)`.
- **Do not move any camera shot or timeline.** The narration is being written to the existing cuts.
  Ritwika owns re-cutting; if a shot needs to move, that is her call, not yours.
- **Do not edit shared material assets** â€” property blocks only. See the warning above; this has
  already bitten us once.
- Don't edit `OrderContext.cs`, `OrderSolver.cs`, `TourRunner.cs`, `OrderPanel.cs`,
  `MainMenuController.cs`, `OrderDashboardController.cs`.

---

## Definition of done

- [ ] One scene pass merged today: binders in place, known bugs fixed
- [x] Stage 4 fibre and char streams visibly track the output split
- [x] Stage 2 granules visibly differ at 2 / 8 / 16 mm
- [ ] Stage 3 kiln visibly differs at 550 / 580 / 600 Â°C
- [ ] All three presets played end to end â€” each looks distinctly different
- [ ] Free play with no order is identical to today
- [ ] No further scene edits after the first pass
- [x] Unity console: zero errors
