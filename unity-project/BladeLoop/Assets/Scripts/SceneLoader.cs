using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadStage1() { SceneManager.LoadScene("Stage1_StoryMode"); }
    public void LoadStage2() { SceneManager.LoadScene("Stage2_StoryMode"); }
    public void LoadStage3() { SceneManager.LoadScene("Stage3_StoryMode"); }
    public void LoadMainMenu() { SceneManager.LoadScene("MainMenu"); }
    public void QuitApp() { Application.Quit(); }
}