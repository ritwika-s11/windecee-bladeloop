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

    bool cutawayOn = false;

    void Start()
    {
        if (button != null) button.onClick.AddListener(Toggle);
        UpdateLabel();
        if (toggler != null) toggler.ShowExterior();
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
