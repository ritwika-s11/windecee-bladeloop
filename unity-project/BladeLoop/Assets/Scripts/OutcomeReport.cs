using System.Text;
using UnityEngine;

/// <summary>
/// The run report: one self-contained HTML file describing what the planner asked
/// for, what they set, and what the plant actually produced.
///
/// ---------------------------------------------------------------------------
///  WHY IT LEADS WITH A VERDICT
///
///  The tour already shows the numbers - the panel has been carrying them for
///  forty-six seconds. What it never answers is the planner's actual question:
///  did this run fill the order, and if not, what went wrong. OrderContext has
///  had MeetsTarget, AchievedGrade and EndUseFor() all along; nothing displayed
///  them. So the report opens with the verdict and only then shows the working.
///
///  A missed target is not a failure state. Missing HIGH and landing on MID
///  still produces sellable fibre - it just sells to a different buyer, which is
///  what EndUseFor() says. The report is careful to state that rather than
///  colouring the whole thing red.
///
///  SELF-CONTAINED: no external CSS, no images, no web fonts. The file opens
///  from a USB stick on a machine with no network. IBM Plex is named first in
///  each font stack and degrades to the system UI font if it is not installed.
///
///  PRINTS: the screen style is the app's dark palette so the report feels like
///  part of what they just watched; @media print inverts it to paper so it does
///  not swallow a toner cartridge.
/// ---------------------------------------------------------------------------
/// </summary>
public static class OutcomeReport
{
    // ---- palette, mirrored from BladeLoopTheme so the file stands alone -------
    //
    // MIRRORED, not referenced: the output is a standalone HTML file that has to
    // render with no Unity runtime, so the colours have to exist here as text.
    // That makes this the one place in the project that does NOT update itself
    // when the theme changes - it has to be edited by hand, as it was for the
    // 8 Sep palette revamp. If BladeLoopTheme.InitPalette changes again, change
    // these too, or the downloaded report will quietly disagree with the app it
    // came from.
    const string CBone  = "#F2F4F7";   // BladeLoopTheme.Bone
    const string CMuted = "#9BA4B0";   // Muted
    const string CFaint = "#5F6A77";   // Faint
    const string COxide = "#FF6B35";   // Oxide
    const string CRule  = "#23272E";   // Rule
    const string CPanel = "#0A0B0D";   // Panel
    const string CSky   = "#11141A";   // SkyWarm
    const string CGood  = "#74B36C";   // StreamGas, used as the "order filled" green

    // fibre, oil, syngas, char, loss - same order as BladeLoopTheme.StreamColours
    static readonly string[] StreamCols = { "#EFE9DB", "#E0A63F", "#74B36C", "#46403A", "#6B7480" };

    /// <summary>A filename that sorts chronologically and never collides.</summary>
    public static string FileName()
    {
        string who = OrderContext.HasOrder ? GradeSlug(OrderContext.Active.targetGrade) : "freeplay";
        return "BladeLoop-run-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + who + ".html";
    }

    static string GradeSlug(Grade g) => g == Grade.High ? "high" : g == Grade.Mid ? "mid" : "low";

    /// <summary>HTML-escape. customerName is typed by the user on the Custom Order
    /// screen, so it genuinely can contain characters that would break the
    /// document or inject markup into it.</summary>
    static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;").Replace("'", "&#39;");
    }

    public static string BuildHtml()
    {
        var m = OrderContext.Model ?? OrderContext.DesignCase();
        var s = m.OutputSplit();
        var refM = OrderContext.Reference;
        bool hasOrder = OrderContext.HasOrder;

        var sb = new StringBuilder(16000);
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<title>BladeLoop run report</title>");
        AppendCss(sb);
        sb.Append("</head><body><div class=\"page\">");

        // ---- masthead --------------------------------------------------------
        sb.Append("<header><div class=\"brand\">BLADELOOP</div>")
          .Append("<div class=\"sub\">Run report &middot; ")
          .Append(System.DateTime.Now.ToString("d MMMM yyyy, HH:mm"))
          .Append("</div></header>");

        // ---- verdict ---------------------------------------------------------
        AppendVerdict(sb, m, hasOrder);

        // ---- three columns ---------------------------------------------------
        sb.Append("<div class=\"cols\">");
        AppendOrderColumn(sb, m, hasOrder);
        AppendPlantColumn(sb, m, refM);
        AppendOutputColumn(sb, s, refM.OutputSplit());
        sb.Append("</div>");

        // ---- campaign + end use ---------------------------------------------
        AppendFooterBlocks(sb, m, hasOrder);

        sb.Append("<footer>Generated by BladeLoop, a teaching simulation of thermal ")
          .Append("co-processing for decommissioned wind turbine blades. Figures are ")
          .Append("model output, not measurements from an operating plant.</footer>");

        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ parts --

    static void AppendVerdict(StringBuilder sb, ProcessModel m, bool hasOrder)
    {
        if (!hasOrder)
        {
            // Unreachable from the app: every route into the tour carries an order -
            // the three worked examples and the Custom Order screen all call
            // SetOrder before loading FullPlantTour. This branch exists only so a
            // stage opened directly in the editor cannot throw. It describes the
            // design case rather than claiming an order is missing, because a user
            // who somehow saw this would have no idea what "no order" meant.
            sb.Append("<section class=\"verdict neutral\"><div class=\"vtitle\">Design case</div>")
              .Append("<div class=\"vbody\">This run was not started from an order, so it is ")
              .Append("shown against the plant's design settings.</div></section>");
            return;
        }

        Grade target   = OrderContext.Active.targetGrade;
        Grade achieved = OrderContext.AchievedGrade;
        bool met = OrderContext.MeetsTarget;

        sb.Append("<section class=\"verdict ").Append(met ? "good" : "warn").Append("\">");
        sb.Append("<div class=\"vtitle\">").Append(met ? "Order filled" : "Target missed").Append("</div>");
        sb.Append("<div class=\"vbody\">");
        sb.Append("Asked for <b>").Append(Esc(OrderContext.GradeLabel(target))).Append("</b>, produced <b>")
          .Append(Esc(OrderContext.GradeLabel(achieved))).Append("</b>. ");
        if (met)
        {
            sb.Append("The fibre meets the grade the buyer ordered.");
        }
        else
        {
            // Grade is ordered High(0) < Mid(1) < Low(2), so the gap is a real
            // count of steps - and High -> Low is two, not one. Saying "one step
            // lower" for every miss would understate the worst runs.
            int steps = (int)achieved - (int)target;
            sb.Append("The fibre is still sellable, but not to this buyer &mdash; it grades out ")
              .Append(steps >= 2 ? "two steps" : "one step")
              .Append(" lower, so it goes to a different market.");
        }
        sb.Append("</div>");
        sb.Append("<div class=\"vuse\">").Append(Esc(OrderContext.EndUseFor(achieved))).Append("</div>");
        sb.Append("</section>");
    }

    static void AppendOrderColumn(StringBuilder sb, ProcessModel m, bool hasOrder)
    {
        sb.Append("<section class=\"col\"><h2>The order</h2>");
        if (!hasOrder)
        {
            sb.Append("<p class=\"none\">Not started from an order.</p></section>");
            return;
        }
        var o = OrderContext.Active;
        // No buyer NAME is ever collected. Every Order in the project is built with
        // customerName = "" - the three presets and the Custom Order screen alike
        // (OrderDashboardController passes BuyerName(achieved) as the TYPE). So the
        // report shows the buyer type and says so, rather than labelling a sector
        // as if it were a company.
        Row(sb, "Buyer type", Esc(o.customerType));
        Row(sb, "Grade requested", Esc(OrderContext.GradeLabel(o.targetGrade)));
        Row(sb, "Fibre requested", o.targetTonnes.ToString("0.#") + " t");
        sb.Append("</section>");
    }

    static void AppendPlantColumn(StringBuilder sb, ProcessModel m, ProcessModel r)
    {
        sb.Append("<section class=\"col\"><h2>How the plant was set</h2>");
        // The divisors are the model's own deviation denominators (ProcessModel.DevTemp
        // and friends), so a full-width bar is a full-strength penalty rather than an
        // arbitrary scale. MIRRORED like the palette above: they are not public
        // constants, so if ProcessModel's denominators change, change these too.
        SettingRow(sb, "Temperature", m.TempC,          r.TempC,          "0",   " °C",  150f);
        SettingRow(sb, "Retention",   m.RetentionMin,   r.RetentionMin,   "0.#", " min",  10f);
        SettingRow(sb, "Feed rate",   m.FeedKgH,        r.FeedKgH,        "0",   " kg/h", 2500f);
        SettingRow(sb, "Particle",    m.ParticleSizeMm, r.ParticleSizeMm, "0.#", " mm",   18f);
        sb.Append("<p class=\"note\">Each bar runs from the design case at the centre. ")
          .Append("Longer means further off spec, scaled by how far that setting is ")
          .Append("allowed to drift before the model treats it as fully deviated. ")
          .Append("Deviation is what moves every number in the next column.</p>");
        sb.Append("</section>");
    }

    /// <summary>
    /// The five streams, each against its design-case share.
    ///
    /// The bar is the actual split; the pale tick on it is where the design case sits.
    /// Showing actual alone answers "what came out" but not "was that good", which is
    /// the question a reader of a RUN REPORT is actually holding - and the design
    /// figures were already computed, just never displayed.
    ///
    /// A tick rather than a second bar: doubling the bars would double the height of
    /// the busiest block on the page to carry a number that only matters as a
    /// comparison. The tick is drawn taller than the track so it stays visible over
    /// both the near-white fibre fill and the near-black char fill.
    /// </summary>
    static void AppendOutputColumn(StringBuilder sb, ProcessModel.Split s, ProcessModel.Split d)
    {
        sb.Append("<section class=\"col wide\"><h2>What the plant made</h2>");
        string[] names = { "Reclaimed glass fibre", "Pyrolysis oil", "Syngas", "Carbon char", "Loss" };
        float[]  pct   = { s.GlassPct, s.OilPct, s.SyngasPct, s.CharPct, s.LossPct };
        float[]  kgh   = { s.GlassKgH, s.OilKgH, s.SyngasKgH, s.CharKgH, s.LossKgH };
        float[]  dpct  = { d.GlassPct, d.OilPct, d.SyngasPct, d.CharPct, d.LossPct };

        // Scale across BOTH series, or a design tick could land past the end of its
        // own track on any run that under-performs the design case.
        float widest = 0f;
        for (int i = 0; i < pct.Length; i++) widest = Mathf.Max(widest, Mathf.Max(pct[i], dpct[i]));
        if (widest <= 0f) widest = 1f;

        for (int i = 0; i < names.Length; i++)
        {
            sb.Append("<div class=\"bar\"><div class=\"blab\">").Append(names[i]).Append("</div>");
            sb.Append("<div class=\"btrack\"><div class=\"bfill\" style=\"width:")
              .Append((pct[i] / widest * 100f).ToString("0.#"))
              .Append("%;background:").Append(StreamCols[i]).Append("\"></div>");
            sb.Append("<div class=\"bmark\" style=\"left:")
              .Append((dpct[i] / widest * 100f).ToString("0.#"))
              .Append("%\" title=\"design ").Append(dpct[i].ToString("0.0")).Append("%\"></div></div>");
            sb.Append("<div class=\"bval\">").Append(pct[i].ToString("0.0")).Append("%</div>");
            sb.Append("<div class=\"bkg\">").Append(kgh[i].ToString("#,0")).Append(" kg/h</div></div>");
        }
        sb.Append("<p class=\"note\"><span class=\"legtick\"></span>&nbsp;marks the design case &mdash; ")
          .Append("where each stream sits when all four settings are on spec. ")
          .Append("Fibre below its tick, or char above its own, means the kiln is not ")
          .Append("decomposing the feed completely.</p>");
        sb.Append("<p class=\"note\"><b>Loss</b> is feed that never becomes any product &mdash; ")
          .Append("fines carried off with the gas, dust, and residue left inside the plant. ")
          .Append("It sits near 1.5% when the plant is on spec and climbs toward 10% as the ")
          .Append("feed gets coarser, which is why particle size moves it more than anything else.</p>");
        sb.Append("</section>");
    }

    static void AppendFooterBlocks(StringBuilder sb, ProcessModel m, bool hasOrder)
    {
        sb.Append("<div class=\"cols\">");

        sb.Append("<section class=\"col\"><h2>Fibre quality</h2>");
        QualityBar(sb, "Purity", m.FiberPurityPct,
                   OrderContext.MidPurity,  OrderContext.HighPurity);
        QualityBar(sb, "Tensile retention", m.TensileRetentionPct,
                   OrderContext.MidTensile, OrderContext.HighTensile);
        Row(sb, "Efficiency", m.EfficiencyPct + "%");
        sb.Append("<p class=\"note\">Ticks mark the grade thresholds &mdash; <b>mid</b> at ")
          .Append(OrderContext.MidPurity.ToString("0")).Append("% purity / ")
          .Append(OrderContext.MidTensile.ToString("0")).Append("% strength, <b>high</b> at ")
          .Append(OrderContext.HighPurity.ToString("0")).Append("% / ")
          .Append(OrderContext.HighTensile.ToString("0")).Append("%. ")
          .Append("A bar is coloured by the grade <i>that measure alone</i> would earn, so when ")
          .Append("the two differ the shorter one is what held the run back.</p>");
        sb.Append("</section>");

        sb.Append("<section class=\"col wide\"><h2>What the run costs</h2>");
        if (hasOrder)
        {
            Row(sb, "Blade material", OrderContext.FeedTonnesLabel);
            Row(sb, "Blades",         OrderContext.BladesLabel);
            Row(sb, "Turbines",       OrderContext.TurbinesLabel);
            Row(sb, "Campaign",       OrderContext.CampaignLabel);
        }
        else
        {
            sb.Append("<p class=\"none\">No order, so there is no campaign to size.</p>");
        }
        sb.Append("</section>");

        sb.Append("</div>");
    }

    // ------------------------------------------------------------------ atoms --

    /// <summary>
    /// One quality measure on a 0-100 scale with the two grade thresholds ticked on it.
    ///
    /// WHY THIS EARNS ITS SPACE. The report opens with a verdict - "produced MID GRADE" -
    /// and then never says how close the run came to the grade above, or which of the two
    /// measures decided it. Both are already known; nothing displayed them. Three bare
    /// percentages became two bars that answer "how far off were we, and because of what".
    ///
    /// The scale is a literal 0-100 because both values ARE percentages, so bar length is
    /// the number itself rather than a rescaling the reader has to decode.
    ///
    /// Each bar is coloured by the tier THAT MEASURE ALONE would earn. Grade needs both to
    /// clear, so when purity is green and strength is orange the binding constraint is
    /// visible at a glance - which is the actionable part.
    /// </summary>
    static void QualityBar(StringBuilder sb, string label, float value, float midBar, float highBar)
    {
        string tier = value >= highBar ? "hi" : value >= midBar ? "mid" : "lo";

        sb.Append("<div class=\"qrow\"><div class=\"qhead\"><span class=\"qlab\">").Append(label)
          .Append("</span><span class=\"qval\">").Append(value.ToString("0.0")).Append("%</span></div>");
        sb.Append("<div class=\"qtrack\"><div class=\"qfill ").Append(tier)
          .Append("\" style=\"width:").Append(Mathf.Clamp(value, 0f, 100f).ToString("0.#")).Append("%\"></div>");
        sb.Append("<div class=\"qtick\" style=\"left:").Append(midBar.ToString("0.#"))
          .Append("%\" title=\"mid grade: ").Append(midBar.ToString("0")).Append("%\"></div>");
        sb.Append("<div class=\"qtick\" style=\"left:").Append(highBar.ToString("0.#"))
          .Append("%\" title=\"high grade: ").Append(highBar.ToString("0")).Append("%\"></div>");
        sb.Append("</div></div>");
    }

    static void Row(StringBuilder sb, string k, string v)
    {
        sb.Append("<div class=\"row\"><span class=\"k\">").Append(k)
          .Append("</span><span class=\"v\">").Append(v).Append("</span></div>");
    }

    /// <summary>A setting beside its design value, with the signed gap and a
    /// centre-zero deviation bar. Reads "on spec" at zero rather than "+0", matching
    /// the in-app panel.</summary>
    static void SettingRow(StringBuilder sb, string k, float actual, float design,
                           string fmt, string unit, float span)
    {
        float d = actual - design;
        bool onSpec = Mathf.Abs(d) < 0.05f;
        string delta = onSpec ? "on spec" : (d > 0f ? "+" : "−") + Mathf.Abs(d).ToString(fmt) + unit;

        sb.Append("<div class=\"row\"><span class=\"k\">").Append(k)
          .Append("</span><span class=\"v\">").Append(actual.ToString(fmt)).Append(unit)
          .Append("</span><span class=\"").Append(onSpec ? "d ok" : "d off").Append("\">")
          .Append(delta).Append("</span></div>")
          .Append("<div class=\"dsg\">design ").Append(design.ToString(fmt)).Append(unit).Append("</div>");

        // Centre-zero bar. Half-width is a full deviation, so the fill can never
        // escape its track however far the user has dragged a slider.
        float u = Mathf.Clamp(d / Mathf.Max(span, 0.0001f), -1f, 1f);
        float half = Mathf.Abs(u) * 50f;
        float left = u >= 0f ? 50f : 50f - half;

        sb.Append("<div class=\"dvtrack\"><div class=\"dvzero\"></div>");
        if (half > 0.15f)                       // below this it renders as a smudge on the centre line
            sb.Append("<div class=\"dvfill").Append(onSpec ? " ok" : "")
              .Append("\" style=\"left:").Append(left.ToString("0.#"))
              .Append("%;width:").Append(half.ToString("0.#")).Append("%\"></div>");
        sb.Append("</div>");
    }

    static void AppendCss(StringBuilder sb)
    {
        sb.Append("<style>");
        sb.Append("*{box-sizing:border-box}");
        sb.Append("body{margin:0;background:").Append(CPanel).Append(";color:").Append(CBone)
          .Append(";font-family:'IBM Plex Sans','Segoe UI',system-ui,-apple-system,sans-serif;")
          .Append("font-size:15px;line-height:1.5}");
        sb.Append(".page{max-width:1080px;margin:0 auto;padding:44px 40px 64px}");

        sb.Append("header{border-bottom:1px solid ").Append(CRule).Append(";padding-bottom:18px;margin-bottom:26px}");
        sb.Append(".brand{font-size:26px;letter-spacing:.26em;font-weight:600}");
        sb.Append(".sub{color:").Append(CMuted).Append(";font-size:13px;letter-spacing:.08em;margin-top:6px}");

        sb.Append(".verdict{border-left:3px solid ").Append(COxide).Append(";background:").Append(CSky)
          .Append(";padding:20px 24px;margin-bottom:30px}");
        sb.Append(".verdict.good{border-left-color:").Append(CGood).Append("}");
        sb.Append(".verdict.warn{border-left-color:").Append(COxide).Append("}");
        sb.Append(".verdict.neutral{border-left-color:").Append(CFaint).Append("}");
        sb.Append(".vtitle{font-size:19px;font-weight:600;letter-spacing:.05em;margin-bottom:8px}");
        sb.Append(".verdict.good .vtitle{color:").Append(CGood).Append("}");
        sb.Append(".verdict.warn .vtitle{color:").Append(COxide).Append("}");
        sb.Append(".vbody{font-size:15px}");
        sb.Append(".vuse{color:").Append(CMuted).Append(";font-size:13.5px;margin-top:10px;font-style:italic}");

        sb.Append(".cols{display:flex;gap:30px;margin-bottom:30px;flex-wrap:wrap}");
        sb.Append(".col{flex:1 1 240px;min-width:230px}");
        sb.Append(".col.wide{flex:1.7 1 340px}");
        sb.Append("h2{font-size:11.5px;letter-spacing:.18em;text-transform:uppercase;color:")
          .Append(CMuted).Append(";font-weight:600;margin:0 0 14px;padding-bottom:8px;border-bottom:1px solid ")
          .Append(CRule).Append("}");

        sb.Append(".row{display:flex;align-items:baseline;gap:10px;padding:5px 0}");
        sb.Append(".k{color:").Append(CMuted).Append(";flex:1;font-size:13.5px}");
        sb.Append(".v{font-variant-numeric:tabular-nums;font-weight:600}");
        sb.Append(".d{font-size:12px;font-variant-numeric:tabular-nums}");
        sb.Append(".d.ok{color:").Append(CGood).Append("}");
        sb.Append(".d.off{color:").Append(COxide).Append("}");
        sb.Append(".dsg{color:").Append(CFaint).Append(";font-size:11.5px;margin:-3px 0 7px}");
        sb.Append(".note{color:").Append(CFaint).Append(";font-size:12px;margin-top:14px;line-height:1.45}");
        sb.Append(".none{color:").Append(CFaint).Append(";font-size:13px}");

        sb.Append(".bar{display:flex;align-items:center;gap:10px;padding:6px 0}");
        sb.Append(".blab{flex:0 0 150px;font-size:13px;color:").Append(CMuted).Append("}");
        // NOT overflow:hidden any more - the design tick is deliberately taller than
        // the track so it stays legible over the near-white fibre fill and the
        // near-black char fill alike, and hiding the overflow would clip it back to
        // invisibility on exactly those two rows.
        sb.Append(".btrack{position:relative;flex:1;height:9px;background:").Append(CRule).Append(";border-radius:2px}");
        sb.Append(".bfill{height:100%;border-radius:2px}");
        sb.Append(".bmark{position:absolute;top:-3px;width:2px;height:15px;margin-left:-1px;background:")
          .Append(CBone).Append("}");
        sb.Append(".legtick{display:inline-block;width:2px;height:10px;vertical-align:-1px;background:")
          .Append(CBone).Append("}");

        // quality against the grade thresholds
        sb.Append(".qrow{margin:0 0 15px}");
        sb.Append(".qhead{display:flex;align-items:baseline;gap:10px;margin-bottom:5px}");
        sb.Append(".qlab{flex:1;color:").Append(CMuted).Append(";font-size:13.5px}");
        sb.Append(".qval{font-variant-numeric:tabular-nums;font-weight:600;font-size:15px}");
        sb.Append(".qtrack{position:relative;height:9px;background:").Append(CRule).Append(";border-radius:2px}");
        sb.Append(".qfill{height:100%;border-radius:2px;background:").Append(CFaint).Append("}");
        sb.Append(".qfill.hi{background:").Append(CGood).Append("}");
        sb.Append(".qfill.mid{background:").Append(COxide).Append("}");
        sb.Append(".qtick{position:absolute;top:-3px;width:2px;height:15px;margin-left:-1px;background:")
          .Append(CBone).Append("}");

        // centre-zero deviation bars in the settings column
        sb.Append(".dvtrack{position:relative;height:6px;background:").Append(CRule)
          .Append(";border-radius:2px;margin:0 0 12px}");
        sb.Append(".dvzero{position:absolute;left:50%;top:-2px;width:1px;height:10px;background:")
          .Append(CFaint).Append("}");
        sb.Append(".dvfill{position:absolute;top:0;height:100%;background:").Append(COxide)
          .Append(";border-radius:2px}");
        sb.Append(".dvfill.ok{background:").Append(CGood).Append("}");
        sb.Append(".bval{flex:0 0 52px;text-align:right;font-variant-numeric:tabular-nums;font-weight:600;font-size:13.5px}");
        sb.Append(".bkg{flex:0 0 82px;text-align:right;color:").Append(CFaint)
          .Append(";font-size:12px;font-variant-numeric:tabular-nums}");

        sb.Append("footer{border-top:1px solid ").Append(CRule).Append(";margin-top:34px;padding-top:16px;color:")
          .Append(CFaint).Append(";font-size:11.5px;line-height:1.5}");

        // paper
        sb.Append("@media print{body{background:#fff;color:#1a1a1a}");
        sb.Append(".page{padding:0;max-width:none}");
        sb.Append(".verdict{background:#f4f2ee}");
        sb.Append(".k,.sub,h2{color:#5a5248}.dsg,.note,.none,footer,.bkg{color:#7a7268}");
        sb.Append(".btrack,.dvtrack,.qtrack{background:#e2ded6}");
        // The design tick and the legend swatch are near-white on screen, which is
        // invisible on paper. Flip them, and darken the centre line with them.
        sb.Append(".bmark,.legtick,.qtick{background:#1a1a1a}");
        sb.Append(".qlab{color:#5a5248}");
        sb.Append(".dvzero{background:#8a8278}");
        sb.Append(".cols{page-break-inside:avoid}}");

        sb.Append("</style>");
    }
}
