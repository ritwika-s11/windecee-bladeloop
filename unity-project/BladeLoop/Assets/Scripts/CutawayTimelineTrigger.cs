using UnityEngine;
using UnityEngine.Playables;

public class CutawayTimelineTrigger : MonoBehaviour
{
    public PlayableDirector director;
    public CutawayToggler toggler;
    public float showAtTime = 25f;
    public float hideAtTime = 40f;

    bool shown = false;

    void Update()
    {
        if (director == null || toggler == null) return;
        double t = director.time;
        bool inWindow = t >= showAtTime && t < hideAtTime;
        if (inWindow && !shown) { toggler.ShowCutaway(); shown = true; }
        else if (!inWindow && shown) { toggler.ShowExterior(); shown = false; }
    }
}
