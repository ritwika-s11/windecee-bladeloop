using UnityEngine;

/// <summary>
/// Shared state for the Order -> Plan -> Prove flow.
///
/// STATIC ON PURPOSE. Not a MonoBehaviour, not a singleton prefab: it needs no
/// scene object, no inspector wiring, survives every scene load, and works in
/// WebGL builds. The stage scenes, the dashboards and the home page all read
/// from here; nothing needs to find or reference anything.
///
/// Implements docs/interface-contract.md section 2. Sharan and Anirban are
/// coding against these exact names - if a signature has to change, change the
/// contract first and tell the group.
///
/// Owner: Ritwika (taken from Akshat 31 Aug so the Unity-side work isn't blocked
/// behind it).
/// </summary>
public enum Grade { High = 0, Mid = 1, Low = 2 }

[System.Serializable]
public class Order
{
    public string customerName;     // user-typed on the Custom Order screen; EMPTY for presets
    public string customerType;     // "Composite manufacturer"
    public Grade  targetGrade;
    public float  targetTonnes;     // tonnes of recovered fibre requested

    public Order() { }

    public Order(string name, string type, Grade grade, float tonnes)
    {
        customerName = name;
        customerType = type;
        targetGrade  = grade;
        targetTonnes = tonnes;
    }
}

public static class OrderContext
{
    // ---------------------------------------------------------------- state --

    /// <summary>The order being run. Null means free play (editor, or a stage
    /// played on its own). Everything must behave sensibly in that case.</summary>
    public static Order Active;

    /// <summary>The plant settings for the active order. NEVER NULL - defaults to
    /// the design case so that a stage scene played on its own still has numbers
    /// to read instead of throwing.</summary>
    public static ProcessModel Model = DesignCase();

    public static bool HasOrder => Active != null;

    public static void SetOrder(Order o, ProcessModel m)
    {
        Active = o;
        Model  = m ?? DesignCase();
    }

    public static void Clear()
    {
        // Remember what just ran before throwing the order away, so the home page can
        // say "last run: mid grade, 4,691 kg/h" when you come back. Software that
        // remembers what you did feels like software; software that forgets every time
        // feels like a demo.
        if (Active != null && Model != null)
        {
            HasLastRun     = true;
            LastRunGrade   = AchievedGrade;
            LastRunTarget  = Active.targetGrade;
            LastRunFibreKgH = Model.OutputSplit().GlassKgH;
            LastRunPurity  = Model.FiberPurityPct;
            LastRunBuyer   = Active.customerType;
        }

        Active = null;
        Model  = DesignCase();
    }

    // ---- memory of the last completed run ------------------------------------
    // Static, so it survives scene loads but not an app restart. That is the right
    // lifetime: it is a convenience within a session, not saved state.
    public static bool   HasLastRun;
    public static Grade  LastRunGrade;
    public static Grade  LastRunTarget;
    public static float  LastRunFibreKgH;
    public static float  LastRunPurity;
    public static string LastRunBuyer;

    public static void ForgetLastRun() { HasLastRun = false; }

    /// <summary>600 C / 35 min / 6500 kg/h / 2 mm - every optimum in ProcessModel.</summary>
    public static ProcessModel DesignCase() => new ProcessModel
    {
        TempC          = ProcessModel.OptTemp,
        RetentionMin   = ProcessModel.OptRetention,
        FeedKgH        = ProcessModel.OptFeed,
        ParticleSizeMm = ProcessModel.OptParticle
    };

    // ----------------------------------------------------------- grade tiers --

    // Calibrated to published pyrolysis results, NOT taken from a standard - no
    // grading standard exists for recovered composite glass fibre. See
    // docs/CEE-deliverable.md and docs/grade-threshold-reasoning.md.
    //
    // These MUST stay below the ProcessModel ceilings (93% purity / 90% tensile)
    // or the High tier becomes unreachable and every run reads Mid.
    public const float HighPurity = 90f, HighTensile = 85f;
    public const float MidPurity  = 78f, MidTensile  = 70f;

    public static Grade GradeOf(float purityPct, float tensilePct)
    {
        if (purityPct >= HighPurity && tensilePct >= HighTensile) return Grade.High;
        if (purityPct >= MidPurity  && tensilePct >= MidTensile ) return Grade.Mid;
        return Grade.Low;
    }

    public static Grade AchievedGrade =>
        GradeOf(Model.FiberPurityPct, Model.TensileRetentionPct);

    /// <summary>True when the run reached the grade it was aiming for, or better.
    /// Grade is ordered High(0) &lt; Mid(1) &lt; Low(2), so "at least as good"
    /// means a numerically smaller or equal value.</summary>
    public static bool MeetsTarget => HasOrder && AchievedGrade <= Active.targetGrade;

    /// <summary>The product's argument, in one line. Sits above the order cards on
    /// the home page, so the obvious challenge - "why would anyone run the plant
    /// badly?" - is answered before it gets asked.</summary>
    public const string Thesis = "There is no wrong setting - only a different buyer.";

    /// <summary>Fraction of the window width the 3D tour occupies while an order is
    /// running. The order panel takes the rest.
    ///
    /// THE ONE PLACE THIS NUMBER LIVES. Two separate systems have to agree on it and
    /// they are owned by different people:
    ///   - TourViewportFrame (Anirban) confines each Screen Space - Overlay canvas to
    ///     the left of this fraction, because an Overlay canvas ignores Camera.rect.
    ///   - The viewport split (Akshat) sets Camera.rect to this width.
    /// If the two ever disagree, the subtitles and buttons sit slightly off the edge
    /// of the 3D view and nobody can work out why. Read this constant; do not type
    /// 0.72 anywhere.</summary>
    public const float TourSplitWidth = 0.72f;

    /// <summary>Who buys output of this grade. Used by the outcome report, which
    /// never says "fail" - a run below target is a different customer, not an error.
    /// Every claim here is sourced; see docs/CEE-deliverable.md section 3.</summary>
    public static string EndUseFor(Grade g) => g switch
    {
        Grade.High => "Clean and strong enough to go back into new composite parts, standing in for virgin glass fibre.",
        Grade.Mid  => "Not clean enough for structural reuse, but sold today as reinforcing filler for precast slabs, pavement and panels.",
        _          => "Co-processed in a cement kiln: the glass replaces raw sand, the resin replaces coal. The most commercially mature route at scale - there is always a buyer."
    };

    public static string GradeLabel(Grade g) => g switch
    {
        Grade.High => "HIGH GRADE",
        Grade.Mid  => "MID GRADE",
        _          => "LOW GRADE"
    };

    // ------------------------------------------------- sourced assumptions ----

    // Anjani Lohith Kosana & Hari Krishna Kondam, 30 Aug 2026.
    // 2 MW-class blade (LM 56.8 P). Blade mass varies widely by turbine class -
    // the How It Works page must state this as an assumption, not a fact.
    public const float BladeMassTonnes  = 11.3f;
    public const int   BladesPerTurbine = 3;

    // --------------------------------------------------- campaign figures -----
    // All return 0 when there is no order. None of these throw.

    /// <summary>Fibre produced per hour at the current settings.</summary>
    public static float FibreKgH => Model.OutputSplit().GlassKgH;

    /// <summary>Fraction of the feed that leaves as fibre (0..1).</summary>
    public static float FibreFraction
    {
        get
        {
            float feed = Model.FeedKgH;
            return feed > 0.01f ? FibreKgH / feed : 0f;
        }
    }

    /// <summary>Tonnes of blade material needed to fill the order.</summary>
    public static float FeedTonnesNeeded
    {
        get
        {
            if (!HasOrder) return 0f;
            float f = FibreFraction;
            return f > 0.0001f ? Active.targetTonnes / f : 0f;
        }
    }

    /// <summary>Hours of continuous running to fill the order.</summary>
    public static float CampaignHours
    {
        get
        {
            if (!HasOrder) return 0f;
            float kgh = FibreKgH;
            return kgh > 0.01f ? Active.targetTonnes * 1000f / kgh : 0f;
        }
    }

    public static float CampaignDays => CampaignHours / 24f;

    public static int BladesNeeded =>
        HasOrder ? Mathf.RoundToInt(FeedTonnesNeeded / BladeMassTonnes) : 0;

    public static int TurbinesNeeded =>
        HasOrder ? Mathf.RoundToInt(FeedTonnesNeeded / BladeMassTonnes / BladesPerTurbine) : 0;

    // ------------------------------------------------------------- presets ----

    public struct Preset
    {
        public Order        order;
        public ProcessModel model;
        public string       endUse;
    }

    // The order tonnages are NOT arbitrary and must not be rounded. They are
    // chosen so all three presets consume the same feedstock (~6,990 t, 619
    // blades, 206 turbines), which is what makes the three runs comparable:
    // one decommissioned wind farm, three customers, three outcomes.
    //
    // The feed rates all sit on the shredder capacity curve in OrderSolver.MaxFeed
    // - 2 mm and 16 mm exactly, 8 mm just under. Changing either breaks the other.
    public static readonly Preset[] Presets =
    {
        new Preset
        {
            order = new Order("", "Composite manufacturer", Grade.High, 4800f),
            model = new ProcessModel { TempC = 600f, RetentionMin = 35f, FeedKgH = 6500f, ParticleSizeMm = 2f },
            endUse = "Clean enough to go back into new structural parts."
        },
        new Preset
        {
            order = new Order("", "Precast concrete producer", Grade.Mid, 4100f),
            model = new ProcessModel { TempC = 580f, RetentionMin = 35f, FeedKgH = 8000f, ParticleSizeMm = 8f },
            endUse = "Not structural, but sold today as reinforcing filler."
        },
        new Preset
        {
            order = new Order("", "Cement works", Grade.Low, 3250f),
            model = new ProcessModel { TempC = 550f, RetentionMin = 35f, FeedKgH = 8800f, ParticleSizeMm = 16f },
            endUse = "Glass replaces sand, resin replaces coal. There is always a buyer."
        }
    };

    // DELIBERATELY NO COMPANY NAMES. An earlier draft invented three German firms.
    // They added nothing the customerType didn't already say, needed a "these are
    // fictional" disclaimer, and two of the first candidates turned out to be real
    // companies anyway. The buyer TYPE is what makes each grade legitimate; a name
    // was decoration with a maintenance cost.
    //
    // What replaced it is better: each card carries a true, sourced line about who
    // actually buys that grade (endUse above, EndUseFor below). That states the
    // product's argument on the home page instead of implying it.
    //
    // customerName stays on Order because the Custom Order screen lets the user
    // type one. For presets it is empty and the UI falls back to customerType.

    public static void ApplyPreset(int index)
    {
        if (index < 0 || index >= Presets.Length) return;
        var p = Presets[index];
        // Copy the model so running a preset twice doesn't inherit edits from the
        // first run - the Presets array is shared, static and long-lived.
        SetOrder(p.order, new ProcessModel
        {
            TempC          = p.model.TempC,
            RetentionMin   = p.model.RetentionMin,
            FeedKgH        = p.model.FeedKgH,
            ParticleSizeMm = p.model.ParticleSizeMm
        });
    }
}
