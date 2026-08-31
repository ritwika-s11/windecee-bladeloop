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
    public string customerName;     // "Nordkomposit GmbH"
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
        Active = null;
        Model  = DesignCase();
    }

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

    /// <summary>Who buys output of this grade. Used by the outcome report, which
    /// never says "fail" - a run below target is a different customer, not an error.</summary>
    public static string EndUseFor(Grade g) => g switch
    {
        Grade.High => "Composite manufacturing - reinforcement in new panels and structural parts",
        Grade.Mid  => "Precast concrete and casting - reinforcing filler for slabs, pavement and panels",
        _          => "Cement works - co-processed, the glass replacing raw silica and the resin replacing fuel"
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
            order = new Order("Nordkomposit GmbH", "Composite manufacturer", Grade.High, 4800f),
            model = new ProcessModel { TempC = 600f, RetentionMin = 35f, FeedKgH = 6500f, ParticleSizeMm = 2f },
            endUse = "New composite parts - panels and structural laminates"
        },
        new Preset
        {
            order = new Order("Elbe Fertigteile GmbH", "Precast concrete producer", Grade.Mid, 4100f),
            model = new ProcessModel { TempC = 580f, RetentionMin = 35f, FeedKgH = 8000f, ParticleSizeMm = 8f },
            endUse = "Reinforcing filler for precast slabs, pavement and panels"
        },
        new Preset
        {
            order = new Order("Zementwerk Harz", "Cement works", Grade.Low, 3250f),
            model = new ProcessModel { TempC = 550f, RetentionMin = 35f, FeedKgH = 8800f, ParticleSizeMm = 16f },
            endUse = "Co-processed in the kiln - glass replaces silica, resin replaces fuel"
        }
    };

    // Customer names above are FICTIONAL and illustrative. Ritwika to confirm or
    // replace; the How It Works page should say they are illustrative.

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
