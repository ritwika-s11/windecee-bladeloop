#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Text;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

/// <summary>
/// Editor-only self-test for OrderContext and OrderSolver.
///
/// Run it from the menu: BladeLoop -> Verify Order Model.
///
/// It checks the numbers in docs/interface-contract.md section 8 against what
/// the code actually returns, and prints PASS/FAIL per row. Anyone touching
/// ProcessModel, OrderContext or OrderSolver should run this before pushing -
/// several of our numbers are quoted in the vision doc, the briefs and the
/// professor-facing narration, so a silent drift is expensive.
///
/// Wrapped in UNITY_EDITOR so none of it ships in the WebGL build.
/// </summary>
public static class OrderSelfTest
{
    [MenuItem("BladeLoop/Verify Order Model")]
    public static void Run()
    {
        var sb = new StringBuilder();
        bool allPass = true;
        sb.AppendLine("=== BladeLoop order-model self-test ===\n");

        // ---- 1. presets against the canonical table ----------------------------
        sb.AppendLine("PRESETS (contract section 8)");
        sb.AppendLine("  preset          purity   tensile   fibre kg/h");
        var expected = new[]
        {
            new { pur = 93.0f, ten = 90.0f, kgh = 4482f },
            new { pur = 82.5f, ten = 76.5f, kgh = 4691f },
            new { pur = 69.8f, ten = 58.3f, kgh = 4091f }
        };

        for (int i = 0; i < OrderContext.Presets.Length; i++)
        {
            var m = OrderContext.Presets[i].model;
            float pur = m.FiberPurityPct, ten = m.TensileRetentionPct, kgh = m.OutputSplit().GlassKgH;
            var e = expected[i];
            bool ok = Near(pur, e.pur, 0.1f) && Near(ten, e.ten, 0.1f) && Near(kgh, e.kgh, 1f);
            allPass &= ok;
            sb.AppendLine($"  {OrderContext.Presets[i].order.targetGrade,-6} {(ok ? "PASS" : "FAIL")}   " +
                          $"{pur,6:0.0}   {ten,6:0.0}   {kgh,8:0}" +
                          (ok ? "" : $"   expected {e.pur:0.0} / {e.ten:0.0} / {e.kgh:0}"));
        }

        // ---- 2. grade tiers must be reachable ---------------------------------
        sb.AppendLine("\nGRADE TIERS REACHABLE");
        // If a threshold is ever raised above the ProcessModel ceiling, High
        // becomes impossible and every run silently reads Mid. That bug is
        // invisible without a check like this.
        var design = OrderContext.DesignCase();
        bool highOk = OrderContext.GradeOf(design.FiberPurityPct, design.TensileRetentionPct) == Grade.High;
        allPass &= highOk;
        sb.AppendLine($"  design case reaches High: {(highOk ? "PASS" : "FAIL - thresholds are above the model ceiling")}" +
                      $"   ({design.FiberPurityPct:0.0} / {design.TensileRetentionPct:0.0} " +
                      $"vs {OrderContext.HighPurity:0} / {OrderContext.HighTensile:0})");

        // ---- 3. every preset must sit inside the shredder capacity curve ------
        sb.AppendLine("\nSHREDDER CAPACITY (presets must be feasible)");
        foreach (var p in OrderContext.Presets)
        {
            float cap = OrderSolver.MaxFeed(p.model.ParticleSizeMm);
            bool ok = p.model.FeedKgH <= cap + 0.5f;
            allPass &= ok;
            sb.AppendLine($"  {p.model.ParticleSizeMm,5:0.0} mm  feed {p.model.FeedKgH,6:0}  cap {cap,7:0.0}  {(ok ? "PASS" : "FAIL")}");
        }

        // ---- 4. the exploit must stay blocked ---------------------------------
        sb.AppendLine("\nEXPLOIT BLOCKED");
        // 600C / 35min / 9000 kg/h / 0.5mm returned 5,911 kg/h at high grade before
        // the capacity constraint, beating every preset on throughput AND quality.
        float exploitCap = OrderSolver.MaxFeed(0.5f);
        bool blocked = 9000f > exploitCap;
        allPass &= blocked;
        sb.AppendLine($"  0.5 mm caps feed at {exploitCap:0} kg/h, not 9000: {(blocked ? "PASS" : "FAIL")}");

        // ---- 5. solver ---------------------------------------------------------
        sb.AppendLine("\nSOLVER");
        var sw = Stopwatch.StartNew();
        foreach (Grade g in new[] { Grade.High, Grade.Mid, Grade.Low })
        {
            var r = OrderSolver.Solve(g);
            if (!r.feasible) { sb.AppendLine($"  {g,-5} INFEASIBLE - {r.note}"); allPass = false; continue; }
            var m = r.model;
            sb.AppendLine($"  {g,-5} {m.TempC:0}C / {m.RetentionMin:0}min / {m.FeedKgH:0} kg/h / {m.ParticleSizeMm:0.0} mm" +
                          $"  ->  {m.OutputSplit().GlassKgH:0} kg/h  (pur {m.FiberPurityPct:0.0}, ten {m.TensileRetentionPct:0.0})");
        }
        sw.Stop();
        sb.AppendLine($"  three solves in {sw.ElapsedMilliseconds} ms");
        sb.AppendLine("  expected: High 4725, Mid 4810, Low 4810 (Mid and Low the same is correct)");

        // ---- 6. campaign figures ----------------------------------------------
        sb.AppendLine("\nCAMPAIGN FIGURES (expect ~6,990 t / 619 blades / 206 turbines for all three)");
        for (int i = 0; i < OrderContext.Presets.Length; i++)
        {
            OrderContext.ApplyPreset(i);
            sb.AppendLine($"  {OrderContext.Active.targetGrade,-5} {OrderContext.Active.targetTonnes,6:0} t  ->  " +
                          $"{OrderContext.FeedTonnesNeeded,7:0} t feed, {OrderContext.BladesNeeded,4} blades, " +
                          $"{OrderContext.TurbinesNeeded,4} turbines, {OrderContext.CampaignDays,5:0.0} days");
        }

        // ---- 7. nothing may throw with no order --------------------------------
        OrderContext.Clear();
        sb.AppendLine("\nNO-ORDER SAFETY (Rule 6)");
        sb.AppendLine($"  HasOrder {OrderContext.HasOrder}, Model null? {OrderContext.Model == null}, " +
                      $"CampaignHours {OrderContext.CampaignHours:0}, BladesNeeded {OrderContext.BladesNeeded}");
        bool safe = !OrderContext.HasOrder && OrderContext.Model != null
                    && OrderContext.CampaignHours == 0f && OrderContext.BladesNeeded == 0;
        allPass &= safe;
        sb.AppendLine($"  {(safe ? "PASS" : "FAIL")} - must be false / not-null / 0 / 0");

        sb.AppendLine("\n" + (allPass ? "=== ALL CHECKS PASSED ===" : "=== SOMETHING FAILED - see above ==="));

        if (allPass) Debug.Log(sb.ToString());
        else         Debug.LogError(sb.ToString());
    }

    static bool Near(float a, float b, float tol) => Mathf.Abs(a - b) <= tol;

    // ---------------------------------------------------------------------------
    //  Tour smoke test helpers. Editor only - none of this ships.
    // ---------------------------------------------------------------------------

    const float FastSpeed = 8f;

    /// <summary>Runs the whole five-scene tour at 8x, so a smoke test costs about 35
    /// seconds instead of four and a half minutes. Audio pitches up and the stage
    /// animations run fast - that is fine, you are checking that the chain ADVANCES,
    /// not how it looks.
    ///
    /// Toggle it off before judging anything visual.</summary>
    [MenuItem("BladeLoop/Debug/Toggle fast tour (8x) %#f")]
    public static void ToggleFastTour()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Fast tour only does anything in play mode. Press Play first.");
            return;
        }
        bool goingFast = Mathf.Approximately(Time.timeScale, 1f);
        Time.timeScale = goingFast ? FastSpeed : 1f;
        Debug.Log($"Tour speed: {Time.timeScale}x" +
                  (goingFast ? "  — the full tour now takes about 35 seconds." : "  — back to normal."));
    }

    /// <summary>Where am I, is the order still alive, did the camera rect reset.
    /// Run this at any point during a tour instead of squinting at the screen.</summary>
    [MenuItem("BladeLoop/Debug/Where am I?")]
    public static void WhereAmI()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        var cam = Camera.main;
        var frames = Object.FindObjectsByType<TourViewportFrame>(FindObjectsSortMode.None);
        int disagree = 0;
        foreach (var f in frames)
            if (Mathf.Abs(f.splitWidth - OrderContext.TourSplitWidth) > 0.001f) disagree++;

        Debug.Log(
            $"scene            : {scene}\n" +
            $"time scale       : {Time.timeScale}x\n" +
            $"order            : {(OrderContext.HasOrder ? OrderContext.Active.targetGrade + " / " + OrderContext.Active.customerType : "none")}\n" +
            $"camera rect      : {(cam != null ? cam.rect.ToString() : "no camera")}   (want 0,0,1,1 on the menu)\n" +
            $"viewport frames  : {frames.Length}, disagreeing with TourSplitWidth: {disagree}\n" +
            $"sequencer alive  : {Object.FindFirstObjectByType<TourSceneSequencer>() != null}");
    }
}
#endif
