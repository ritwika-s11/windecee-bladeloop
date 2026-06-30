using UnityEngine;

public class CutawayToggler : MonoBehaviour
{
    [Header("Exterior objects to hide when cutaway is active")]
    public GameObject[] exteriorRoots;

    [Header("Cutaway root to show when active")]
    public GameObject cutawayRoot;

    [Header("Start state")]
    public bool startWithCutaway = false;

    void Start()
    {
        if (startWithCutaway) ShowCutaway(); else ShowExterior();
    }

    public void ShowCutaway()
    {
        foreach (var g in exteriorRoots) if (g) g.SetActive(false);
        if (cutawayRoot) cutawayRoot.SetActive(true);
    }

    public void ShowExterior()
    {
        foreach (var g in exteriorRoots) if (g) g.SetActive(true);
        if (cutawayRoot) cutawayRoot.SetActive(false);
    }
}
