using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Settings overlay that pauses the game while it is open
public class PauseMenu : MonoBehaviour
{
    [SerializeField]
    private GameObject menu;
    [SerializeField]
    private string menuScene = "Menu";

    public static bool IsPaused { get; private set; }

    private InputAction _toggleAction;

    private void Awake()
    {
        _toggleAction = new InputAction("Settings", InputActionType.Button);
        _toggleAction.AddBinding("<Keyboard>/escape");
        _toggleAction.AddBinding("<Gamepad>/start");

        if (menu != null)
            menu.SetActive(false);
    }

    private void OnEnable() => _toggleAction.Enable();

    private void OnDisable() => _toggleAction.Disable();

    private void OnDestroy()
    {
        _toggleAction?.Dispose();

        if (IsPaused)
            ApplyPause(false);
    }

    private void Update()
    {
        if (!_toggleAction.WasPressedThisFrame())
            return;

        if (IsPaused)
            Close();
        else
            Open();
    }

    public void Open() => SetOpen(true);

    public void Close() => SetOpen(false);

    public void QuitToMenu()
    {
        SetOpen(false);
        SceneManager.LoadScene(menuScene);
    }

    private void SetOpen(bool open)
    {
        if (menu != null)
            menu.SetActive(open);

        ApplyPause(open);
    }

    private static void ApplyPause(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PauseSfx(paused);
    }
}
