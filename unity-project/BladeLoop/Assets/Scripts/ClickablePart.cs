using UnityEngine;

/// <summary>
/// Data only. Attach to any GameObject with a Collider to give it a title and a
/// description; ExploreClickRaycaster is what actually opens the PartInfoPanel.
///
/// THIS USED TO HANDLE ITS OWN CLICKS, via OnMouseDown, and that was the bug where
/// an info panel appeared during story playback without anyone pausing.
///
/// OnMouseDown is a second, completely ungated route to the same panel. It ignores
/// every rule ExploreClickRaycaster enforces:
///   - only respond while the story is PAUSED (controller.IsPaused)
///   - ignore clicks that land on UI (EventSystem.IsPointerOverGameObject)
///   - ignore click-drags used to orbit the camera (clickDragTolerance)
/// So a single click mid-narration opened a dialog over the film.
///
/// Worth recording why it survived: our own briefs state that "OnMouseDown and the
/// legacy Input class never fire" under the New Input System. The legacy Input CLASS
/// does not, but OnMouseDown is a MonoBehaviour message raised by physics picking,
/// and it fires perfectly well. The note in the docs is wrong.
///
/// Keep this component free of input handling. One path in, and it is the gated one.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ClickablePart : MonoBehaviour
{
    public string partTitle;
    [TextArea(2, 6)] public string partDescription;
}
