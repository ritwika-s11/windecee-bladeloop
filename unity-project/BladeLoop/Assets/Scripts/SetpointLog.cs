using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the operator changed while the run was on screen.
///
/// WHY THIS EXISTS AS ITS OWN CLASS
/// --------------------------------
/// The obvious home for this is OrderContext, next to the order it describes. It is
/// deliberately not there. OrderContext is Ritwika's file and is depended on by every
/// screen in the app; a run-scoped log that only the tour writes and only the outcome
/// report reads has no business widening that surface three days before submission.
/// Everything here is additive - nothing in this file is read by any existing code
/// path, so with the tour untouched it is inert.
///
/// WHAT IT IS FOR
/// --------------
/// The app's whole argument is that the settings change what the plant does. Until now
/// the user made that choice once, on the Custom Order screen, and then watched for
/// four minutes. PauseSetpointPanel lets them change a setpoint mid-run and see the
/// plant answer; this class is what stops that being a toy. It remembers what they
/// entered with, so the outcome report can say "you started at 4 mm and finished at
/// 15 mm, and here is what that cost you" rather than quietly reporting the last value
/// as though it had been true all along.
///
/// HONEST ABOUT WHAT IT IS NOT
/// ---------------------------
/// This does NOT make the run a time-weighted campaign. ProcessModel is a steady-state
/// model: one setting in, one outcome out. A real mid-campaign change would mean
/// blending outputs over the days spent at each setpoint, which reaches into
/// MeetsTarget, days-to-fill and the report verdict - shared code, and not a
/// three-days-out change. So the run resolves at the FINAL setting, and the report
/// states plainly that the setpoint moved rather than pretending it did not. That is
/// the honest version of this feature at this size.
///
/// LIFETIME
/// Static, like OrderContext's own last-run memory: it survives scene loads inside a
/// session and resets when the user returns to the menu. Never serialised.
/// </summary>
public static class SetpointLog
{
    /// <summary>One operator intervention, in the order it happened.</summary>
    public struct Change
    {
        public string label;      // "Particle size"
        public string unit;       // " mm"
        public string stage;      // "Shredding" - where they were standing
        public float  from;
        public float  to;
        public string format;     // "0.#" / "0"
    }

    /// <summary>THERE IS NO CAP, AND THERE WAS ONE.
    ///
    /// This used to allow two changes per run, justified as a plant rule: a setpoint
    /// change costs a settling period, so an operator does not make them casually.
    /// That reasoning does not survive contact with the model. ProcessModel is steady
    /// state - no transient, no settling time, no penalty of any kind for moving a
    /// setpoint. The cap charged a cost the simulation never imposes, which in a
    /// teaching tool is worse than no rule at all.
    ///
    /// It also failed at its own job. Preview-and-revert already makes exploration
    /// free, so the cap never limited sweeping the slider - only committing - and
    /// nobody was going to infer that from a counter. What it did do was strand the
    /// user: spend the budget at the shredder and the kiln control arrives dead.
    ///
    /// The consequence the cap was reaching for is already in the app and charged by
    /// the model: the report records what changed, and the verdict flips to TARGET
    /// MISSED when the fibre drops below the buyer's grade.</summary>
    public const int MaxShown = 5;   // report display only - see the report builders

    static readonly List<Change> changes = new List<Change>();

    /// <summary>The model as it stood when the tour began. Null outside a run.
    /// A COPY, never a reference - the live model is mutated in place by the panel,
    /// so holding a reference would quietly rewrite history to match the present.</summary>
    public static ProcessModel Entry { get; private set; }

    public static IReadOnlyList<Change> Changes => changes;
    public static bool Any   => changes.Count > 0;
    public static int  Used  => changes.Count;

    /// <summary>The most recent changes first, at most <paramref name="max"/> of them.
    ///
    /// Newest first because the last thing you did is the thing you are asking about,
    /// and capped because an unlimited log can otherwise run past the bottom of the
    /// report. The count of what was left out is <see cref="Used"/> minus the length
    /// of this list.</summary>
    public static List<Change> Recent(int max)
    {
        var outp = new List<Change>();
        for (int i = changes.Count - 1; i >= 0 && outp.Count < max; i--)
            outp.Add(changes[i]);
        return outp;
    }

    /// <summary>Snapshot the settings the run starts from.
    ///
    /// Idempotent by design: the panel calls this on every stage load, and only the
    /// first call inside a run does anything. Without that guard, walking into the
    /// kiln after changing the shredder would reset the baseline to the changed value
    /// and the report would show no change at all.</summary>
    public static void BeginRun()
    {
        if (Entry != null) return;
        if (!OrderContext.HasOrder || OrderContext.Model == null) return;

        var m = OrderContext.Model;
        Entry = new ProcessModel
        {
            TempC          = m.TempC,
            RetentionMin   = m.RetentionMin,
            FeedKgH        = m.FeedKgH,
            ParticleSizeMm = m.ParticleSizeMm
        };
        changes.Clear();
    }

    /// <summary>Forget the run. Called when the tour is left, so a second run does not
    /// inherit the first one's history.</summary>
    public static void Clear()
    {
        Entry = null;
        changes.Clear();
    }

    /// <summary>Record an intervention.
    ///
    /// A no-move is not an intervention: APPLY on a value the user never actually
    /// changed would otherwise pad the log with rows reading "6 mm → 6 mm". The panel
    /// already disables APPLY in that state; this is the second line of defence, and
    /// it is what keeps Used an honest count now that nothing is capped.</summary>
    public static void Record(string label, string stage, float from, float to,
                              string unit, string format)
    {
        if (Mathf.Approximately(from, to)) return;
        changes.Add(new Change
        {
            label = label, stage = stage, unit = unit, format = format,
            from = from, to = to
        });
    }

    /// <summary>"4 mm" / "600 °C", for whichever setting a change describes.</summary>
    public static string Fmt(Change c, float v) => v.ToString(c.format) + c.unit;
}
