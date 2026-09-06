using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransition : MonoBehaviour
{
    public void LoadNum(int sceneNumber)
    {
        SceneManager.LoadScene(sceneNumber);
    }

    public void LoadMaze()
    {
        LoadNum(1);
    }

    public void LoadMenu()
    {
        LoadNum(0);
    }

    public void LoadCredits()
    {
        LoadNum(2);
    }

    public void QuitGame()
    {
        Debug.Log("Quit");
        Application.Quit();
    }
}