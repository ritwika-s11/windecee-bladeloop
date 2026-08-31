using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps a Screen Space - Overlay canvas inside the tour viewport when an order
/// is running.
///
/// Akshat splits the window from code with <c>Camera.rect</c> - 3D tour in the
/// left 72 %, order panel in the right 28 %. A Screen Space - Overlay canvas
/// ignores <c>Camera.rect</c> completely: it renders straight to the
/// framebuffer and never consults a camera at all. So the split narrows the 3D
/// render and leaves every overlay stretched across the whole window, sitting
/// on top of the order panel.
///
/// This inserts one full-rect child between the canvas and its content and
/// anchors that child to the tour viewport instead. Every existing element then
/// follows automatically, keeping its own anchors and offsets - a subtitle box
/// anchored bottom-centre stays bottom-centre, just of the narrower frame.
///
/// Done at runtime rather than by re-parenting in the scene on purpose: it adds
/// one component per canvas to the scene file instead of rewriting 14
/// hierarchies, which keeps the diff reviewable and merge-safe. Unity scene
/// files cannot be merged, so a small diff is a real safety property here.
/// </summary>
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(RectTransform))]
public class TourViewportFrame : MonoBehaviour
{
    public const string FrameName = "ViewportFrame";

    [Tooltip("Fraction of the window width the 3D tour occupies when an order is running. " +
             "Must match the value Akshat sets on Camera.rect.")]
    [Range(0.2f, 1f)] public float splitWidth = 0.72f;

    [Tooltip("Force the split on even with no active order. Editor preview aid only - " +
             "leave off so free play and standalone scene playback are unchanged.")]
    public bool previewSplit = false;

    static readonly List<TourViewportFrame> Live = new List<TourViewportFrame>();

    RectTransform frame;

    /// <summary>True when the overlays should be confined to the left portion.</summary>
    public bool ShouldSplit => previewSplit || OrderContext.HasOrder;

    void Awake()
    {
        EnsureFrame();
        Apply();
    }

    void OnEnable()
    {
        if (!Live.Contains(this)) Live.Add(this);
    }

    void OnDisable()
    {
        Live.Remove(this);
    }

    /// <summary>
    /// Creates the frame if it isn't there yet and moves the canvas's content
    /// into it. Safe to call more than once - it re-uses an existing frame and
    /// never nests a second one.
    /// </summary>
    void EnsureFrame()
    {
        var canvasRect = (RectTransform)transform;

        var existing = canvasRect.Find(FrameName) as RectTransform;
        if (existing != null)
        {
            frame = existing;
        }
        else
        {
            var go = new GameObject(FrameName, typeof(RectTransform));
            frame = (RectTransform)go.transform;
            frame.SetParent(canvasRect, false);
        }

        // Collect first, then re-parent - moving children while iterating the
        // transform's own list skips every other element.
        var toMove = new List<Transform>();
        for (int i = 0; i < canvasRect.childCount; i++)
        {
            var child = canvasRect.GetChild(i);
            if (child != frame) toMove.Add(child);
        }

        // worldPositionStays: false keeps each child's anchors, offsets and
        // anchoredPosition exactly as authored, so nothing shifts while the
        // frame still covers the full canvas.
        foreach (var child in toMove) child.SetParent(frame, false);

        frame.localScale = Vector3.one;
        frame.localRotation = Quaternion.identity;
        frame.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>Anchors the frame to the tour viewport, or to the whole canvas when no order is running.</summary>
    public void Apply()
    {
        if (frame == null) EnsureFrame();

        float right = ShouldSplit ? Mathf.Clamp(splitWidth, 0.2f, 1f) : 1f;

        frame.anchorMin = new Vector2(0f, 0f);
        frame.anchorMax = new Vector2(right, 1f);

        // Zero offsets so the frame is exactly its anchor rect. Set after the
        // anchors, because changing anchors rewrites offsetMin/offsetMax.
        frame.offsetMin = Vector2.zero;
        frame.offsetMax = Vector2.zero;
        frame.anchoredPosition3D = Vector3.zero;
    }

    /// <summary>
    /// Re-applies every live frame. Call after the order changes mid-session -
    /// starting a run from the homepage, or returning to free play - so the
    /// overlays follow without a scene reload.
    /// </summary>
    public static void RefreshAll()
    {
        for (int i = 0; i < Live.Count; i++)
            if (Live[i] != null) Live[i].Apply();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying && frame != null) Apply();
    }
#endif
}
