using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour
{
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider progressBar;

    private bool isLoading;

    public void LoadScene(string sceneName)
    {
        if (isLoading)
            return;

        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        isLoading = true;
        loadingPanel.SetActive(true);

        if (progressBar != null)
            progressBar.value = 0f;

        // Let Unity draw the loading screen before starting.
        // Keep the panel visible for a moment before loading.
        yield return new WaitForSecondsRealtime(1f);

        AsyncOperation operation =
            SceneManager.LoadSceneAsync(sceneName);

        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            if (progressBar != null)
            {
                progressBar.value =
                    Mathf.Clamp01(operation.progress / 0.9f);
            }

            yield return null;
        }

        if (progressBar != null)
            progressBar.value = 1f;

        // Display the completed bar before switching scenes.
        yield return null;

        Time.timeScale = 1f;
        operation.allowSceneActivation = true;
    }
}