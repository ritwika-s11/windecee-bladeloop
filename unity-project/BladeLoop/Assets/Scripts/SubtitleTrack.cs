using UnityEngine;
using UnityEngine.Playables;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Additive: shows the narration as on-screen text, with Next / Back buttons that
/// step through the narration beats.
///
/// Cues come from a plain text asset so wording or timing can be corrected in any
/// text editor without opening Unity:
///     start | end | text          (seconds on this scene's timeline; '#' = comment)
///
/// Next/Back seek the PlayableDirector, so the camera, audio and subtitle all move
/// together. Back restarts the current line if you are already part-way into it
/// (standard media behaviour), otherwise it goes to the previous line.
///
/// Read-only with respect to the story: it never modifies the timeline asset.
/// </summary>
public class SubtitleTrack : MonoBehaviour
{
    [Header("Wiring")]
    public PlayableDirector director;
    public TextAsset cueFile;
    public TMP_Text label;
    public CanvasGroup group;

    [Header("Behaviour")]
    [Tooltip("OFF (default): Back always jumps to the previous line - one click, one line back.\n" +
             "ON: Back first restarts the current line if you are already past the threshold below " +
             "(music-player style), which means it can take two clicks to actually go back.")]
    public bool backRestartsCurrentLine = false;
    [Tooltip("Only used when 'Back restarts current line' is ON.")]
    public float backRestartThreshold = 1.5f;
    [Tooltip("Hold the last line on screen through short gaps between cues.")]
    public bool holdBetweenCues = true;

    struct Cue { public double start, end; public string text; }
    readonly List<Cue> cues = new List<Cue>();
    int shown = -1;

    void Awake()
    {
        if (director == null) director = FindFirstObjectByType<PlayableDirector>();
        Parse();
        if (label != null) label.text = "";
    }

    void Parse()
    {
        cues.Clear();
        if (cueFile == null) { Debug.LogWarning("[SubtitleTrack] No cue file assigned."); return; }
        foreach (var raw in cueFile.text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            var parts = line.Split('|');
            if (parts.Length < 3) continue;
            if (!double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture, out double s)) continue;
            if (!double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture, out double e)) continue;
            cues.Add(new Cue { start = s, end = e, text = string.Join("|", parts, 2, parts.Length - 2).Trim() });
        }
        cues.Sort((a, b) => a.start.CompareTo(b.start));
    }

    void Update()
    {
        if (director == null || label == null || cues.Count == 0) return;
        double t = director.time;

        int idx = -1;
        for (int i = 0; i < cues.Count; i++)
        {
            if (t >= cues[i].start && t < cues[i].end) { idx = i; break; }
            // between cues: optionally keep the previous line up
            if (holdBetweenCues && t >= cues[i].end && (i + 1 >= cues.Count || t < cues[i + 1].start)) idx = i;
        }

        if (idx != shown)
        {
            shown = idx;
            label.text = idx >= 0 ? cues[idx].text : "";
        }
        if (group != null) group.alpha = string.IsNullOrEmpty(label.text) ? 0f : 1f;
    }

    /// <summary>Jump to the start of the next narration beat.</summary>
    public void Next()
    {
        if (director == null || cues.Count == 0) return;
        double t = director.time;
        for (int i = 0; i < cues.Count; i++)
            if (cues[i].start > t + 0.05) { Seek(cues[i].start); return; }
        Seek(director.duration - 0.1);   // already at the last line
    }

    /// <summary>Restart the current beat, or jump back to the previous one.</summary>
    public void Back()
    {
        if (director == null || cues.Count == 0) return;
        double t = director.time;
        int cur = -1;
        for (int i = 0; i < cues.Count; i++) if (t >= cues[i].start) cur = i;

        if (cur < 0) { Seek(cues[0].start); return; }
        // optional music-player behaviour; off by default so one click = one line back
        if (backRestartsCurrentLine && t - cues[cur].start > backRestartThreshold) { Seek(cues[cur].start); return; }
        Seek(cur > 0 ? cues[cur - 1].start : cues[0].start);
    }

    void Seek(double time)
    {
        director.time = Mathf.Clamp((float)time, 0f, (float)director.duration - 0.05f);
        // force an immediate refresh so camera/audio/subtitle update even while paused
        director.Evaluate();
        shown = -1;
    }
}
