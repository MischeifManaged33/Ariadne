using UnityEngine;
using UnityEngine.SceneManagement;

public class MinotaurWinTrigger : MonoBehaviour
{
    [SerializeField]
    private string winSceneName = "Win";

    private bool triggered;

    public void ShowWinScreen()
    {
        if (triggered)
            return;

        if (string.IsNullOrWhiteSpace(winSceneName) ||
            !Application.CanStreamedLevelBeLoaded(winSceneName))
        {
            Debug.LogError(
                "Assign a win scene included in the build's scene list.",
                this
            );
            return;
        }

        triggered = true;
        Time.timeScale = 1f;

        SceneManager.LoadScene(winSceneName);
    }
}