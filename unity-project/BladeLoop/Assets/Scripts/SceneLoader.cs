using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadStage1()   { SceneManager.LoadScene("Stage1_StoryMode"); }
    public void LoadStage2()   { SceneManager.LoadScene("Stage2_StoryMode"); }
    public void LoadStage3()   { SceneManager.LoadScene("Stage3_StoryMode"); }
    public void LoadStage4()   { SceneManager.LoadScene("Stage4_StoryMode"); }
    public void LoadStage5()   { SceneManager.LoadScene("Stage5_StoryMode"); }
    public void LoadFullTour() { SceneManager.LoadScene("FullPlantTour"); }
    public void LoadMainMenu() { SceneManager.LoadScene("MainMenu"); }
    public void LoadPlantExplorer() { SceneManager.LoadScene("PlantExplorer"); }

    public void QuitApp()
    {
#if UNITY_WEBGL
        Debug.Log("Quit requested in WebGL build - no-op.");
#else
        Application.Quit();
#endif
    }
}
