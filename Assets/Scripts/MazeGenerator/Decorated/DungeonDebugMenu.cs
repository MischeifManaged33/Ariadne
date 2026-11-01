using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DungeonDebugMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DesignedDungeonGenerator generator;
    [SerializeField] private GameObject debugPanel;
    [SerializeField] private GameObject debugButton;

    [Header("Settings Fields")]
    [SerializeField] private TMP_InputField widthInput;
    [SerializeField] private TMP_InputField heightInput;
    [SerializeField] private TMP_InputField roomCountInput;
    [SerializeField] private TMP_InputField roomSpacingInput;
    [SerializeField] private TMP_InputField mazeSpacingInput;
    [SerializeField] private TMP_InputField corridorRadiusInput;
    [SerializeField] private TMP_InputField loopChanceInput;
    [SerializeField] private Toggle fixedSeedToggle;
    [SerializeField] private TMP_InputField seedInput;

    [Header("Feedback")]
    [SerializeField] private TMP_Text statusText;

    private void Start()
    {
        CloseMenu();
    }

    public void OpenMenu()
    {
        Vector2Int size = generator.DebugDungeonSize;

        widthInput.text = size.x.ToString();
        heightInput.text = size.y.ToString();

        roomCountInput.text = generator.DebugRoomCount.ToString();
        roomSpacingInput.text = generator.DebugRoomSpacing.ToString();
        mazeSpacingInput.text = generator.DebugMazeSpacing.ToString();

        corridorRadiusInput.text =
            generator.DebugCorridorRadius.ToString();

        loopChanceInput.text =
            generator.DebugLoopChance.ToString(
                CultureInfo.InvariantCulture
            );

        fixedSeedToggle.isOn = generator.DebugUseFixedSeed;
        seedInput.text = generator.DebugSeed.ToString();

        SetStatus("");

        debugPanel.SetActive(true);
        debugButton.SetActive(false);
    }

    public void CloseMenu()
    {
        debugPanel.SetActive(false);
        debugButton.SetActive(true);
    }

    public void ApplyAndGenerate()
    {
        // Validate everything before changing the generator.
        if (!TryReadInteger(widthInput, "Width", 1, out int width))
            return;

        if (!TryReadInteger(heightInput, "Height", 1, out int height))
            return;

        if (!TryReadInteger(
                roomCountInput, "Room count", 2, out int roomCount))
            return;

        if (!TryReadInteger(
                roomSpacingInput, "Room spacing", 1, out int spacing))
            return;

        if (!TryReadInteger(
                mazeSpacingInput, "Maze spacing", 4, out int maze))
            return;

        if (!TryReadInteger(
                corridorRadiusInput, "Corridor radius", 0,
                out int radius))
            return;

        if (radius > 3)
        {
            SetStatus("Corridor radius must be between 0 and 3.");
            return;
        }

        if (!float.TryParse(
                loopChanceInput.text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float loops) ||
            float.IsNaN(loops) ||
            float.IsInfinity(loops) ||
            loops < 0f ||
            loops > 0.3f)
        {
            SetStatus("Loop chance must be between 0 and 0.3.");
            return;
        }

        int seed = generator.DebugSeed;

        if (fixedSeedToggle.isOn &&
            !int.TryParse(seedInput.text, out seed))
        {
            SetStatus("Seed must be a valid whole number.");
            return;
        }

        generator.DebugDungeonSize = new Vector2Int(width, height);
        generator.DebugRoomCount = roomCount;
        generator.DebugRoomSpacing = spacing;
        generator.DebugMazeSpacing = maze;
        generator.DebugCorridorRadius = radius;
        generator.DebugLoopChance = loops;
        generator.DebugUseFixedSeed = fixedSeedToggle.isOn;
        generator.DebugSeed = seed;

        // The generator logs its result. Capture errors so they
        // are also visible in the menu when running a build.
        SetStatus("Generating...");
        Application.logMessageReceived += HandleGenerationLog;

        try
        {
            generator.GenerateDungeon();

            if (statusText != null &&
                statusText.text == "Generating...")
            {
                SetStatus("Generation finished. Close to inspect.");
            }
        }
        catch (System.Exception exception)
        {
            SetStatus(exception.Message);
            Debug.LogException(exception, this);
        }
        finally
        {
            Application.logMessageReceived -= HandleGenerationLog;
        }
    }

    private bool TryReadInteger(
        TMP_InputField input,
        string label,
        int minimum,
        out int value)
    {
        if (!int.TryParse(input.text, out value) ||
            value < minimum)
        {
            SetStatus(
                $"{label} must be a whole number of at least {minimum}."
            );

            return false;
        }

        return true;
    }

    private void HandleGenerationLog(
        string message,
        string stackTrace,
        LogType type)
    {
        if (type == LogType.Error ||
            type == LogType.Exception ||
            type == LogType.Assert)
        {
            SetStatus(message);
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}