using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Singleton UI panel shown when a ClickablePart is clicked.
/// Displays part name + description with a Close button.
/// </summary>
public class PartInfoPanel : MonoBehaviour
{
    public static PartInfoPanel Instance;

    public CanvasGroup group;
    public TMP_Text titleText;
    public TMP_Text descText;
    public Button closeButton;

    void Awake()
    {
        Instance = this;
        if (group != null) { group.alpha = 0; group.blocksRaycasts = false; group.interactable = false; }
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
    }

    public void Show(string title, string desc)
    {
        if (titleText != null) titleText.text = title;
        if (descText != null) descText.text = desc;
        if (group != null) { group.alpha = 1; group.blocksRaycasts = true; group.interactable = true; }
    }

    public void Hide()
    {
        if (group != null) { group.alpha = 0; group.blocksRaycasts = false; group.interactable = false; }
    }
}
