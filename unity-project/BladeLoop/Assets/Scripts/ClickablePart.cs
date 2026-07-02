using UnityEngine;

/// <summary>
/// Attach to any GameObject with a Collider — clicking it opens the PartInfoPanel
/// with the configured title + description.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ClickablePart : MonoBehaviour
{
    public string partTitle;
    [TextArea(2, 6)] public string partDescription;

    void OnMouseDown()
    {
        if (PartInfoPanel.Instance != null)
            PartInfoPanel.Instance.Show(partTitle, partDescription);
    }
}
