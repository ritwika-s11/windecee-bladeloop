using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Wires a Toggle-style Button to a CutawayToggler.
/// Changes button label between "Show Interior" and "Show Exterior".
/// </summary>
public class CutawayToggleButton : MonoBehaviour
{
    public Button button;
    public TMP_Text buttonLabel;
    public CutawayToggler toggler;

    [Header("Visibility")]
    [Tooltip("Only offer the toggle while the shot that can actually show an interior " +
             "is on screen. Left empty, one is found in the scene.")]
    public CutawayTimelineTrigger window;

    bool cutawayOn = false;
    GameObject root;   // what gets hidden - the button, not this component

    void Start()
    {
        if (button != null) button.onClick.AddListener(Toggle);
        if (window == null) window = FindFirstObjectByType<CutawayTimelineTrigger>();
        root = button != null ? button.gameObject : gameObject;
        UpdateLabel();
        if (toggler != null) toggler.ShowExterior();
    }

    /// <summary>The button only makes sense over the kiln shots. Offered on a wide
    /// exterior or over the output streams it promises an interior that camera
    /// cannot show, and pressing it appears to do nothing.
    ///
    /// The window is taken from CutawayTimelineTrigger rather than duplicated here,
    /// so the button and the automatic reveal can never disagree about when an
    /// interior is available.</summary>
    void Update()
    {
        if (root == null || window == null || window.director == null) return;
        double t = window.director.time;
        bool available = t >= window.showAtTime && t <= window.hideAtTime;
        if (root.activeSelf != available) root.SetActive(available);
    }

    void Toggle()
    {
        cutawayOn = !cutawayOn;
        if (toggler != null)
        {
            if (cutawayOn) toggler.ShowCutaway();
            else toggler.ShowExterior();
        }
        UpdateLabel();
    }

    void UpdateLabel()
    {
        if (buttonLabel != null)
            buttonLabel.text = cutawayOn ? "Show Exterior" : "Show Interior";
    }
}
