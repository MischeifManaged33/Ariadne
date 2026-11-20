using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class DialogueUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private TMP_Text nameText;
    [SerializeField]
    private TMP_Text bodyText;

    [Header("Typing")]
    [SerializeField, Min(0f)]
    private float charactersPerSecond = 40f;

    public static DialogueUI Instance { get; private set; }

    public bool IsOpen { get; private set; }

    private InputAction _advanceAction;
    private Coroutine _routine;
    private Action _onFinished;

    public static DialogueUI Find()
    {
        if (Instance == null)
            Instance = FindAnyObjectByType<DialogueUI>(FindObjectsInactive.Include);

        return Instance;
    }

    private void Awake()
    {
        Instance = this;

        if (nameText == null)
            nameText = FindText("Tag/Name");
        if (bodyText == null)
            bodyText = FindText("Dialogue/Text");

        _advanceAction = new InputAction("Advance Dialogue", InputActionType.Button);
        _advanceAction.AddBinding("<Keyboard>/e");
        _advanceAction.AddBinding("<Keyboard>/enter");
        _advanceAction.AddBinding("<Gamepad>/buttonSouth");
        _advanceAction.AddBinding("<Pointer>/press");

        if (!IsOpen)
            gameObject.SetActive(false);
    }

    private void OnEnable() => _advanceAction?.Enable();

    private void OnDisable() => _advanceAction?.Disable();

    private void OnDestroy()
    {
        _advanceAction?.Dispose();

        if (Instance == this)
            Instance = null;
    }

    public void Play(DialogueData dialogue, Action onFinished = null)
    {
        if (_routine != null) {
            StopCoroutine(_routine);
            _routine = null;
        }

        _onFinished = onFinished;
        IsOpen = true;
        gameObject.SetActive(true);

        _routine = StartCoroutine(Run(dialogue));
    }

    private IEnumerator Run(DialogueData dialogue)
    {
        if (nameText != null)
            nameText.text = dialogue != null ? dialogue.DisplayName : string.Empty;

        if (dialogue != null && !dialogue.IsEmpty) {
            yield return null;

            foreach (var line in dialogue.Lines) {
                yield return TypeLine(line);
                yield return WaitForAdvance();
            }
        }

        Close();
    }

    private IEnumerator TypeLine(string line)
    {
        if (bodyText == null)
            yield break;

        bodyText.text = line;
        bodyText.maxVisibleCharacters = int.MaxValue;

        if (charactersPerSecond <= 0f)
            yield break;

        bodyText.ForceMeshUpdate();
        var total = bodyText.textInfo.characterCount;
        var shown = 0f;

        bodyText.maxVisibleCharacters = 0;

        while (bodyText.maxVisibleCharacters < total) {
            if (_advanceAction.WasPressedThisFrame())
                break;

            shown += charactersPerSecond * Time.unscaledDeltaTime;
            bodyText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
            yield return null;
        }

        bodyText.maxVisibleCharacters = int.MaxValue;

        yield return null;
    }

    private IEnumerator WaitForAdvance()
    {
        while (!_advanceAction.WasPressedThisFrame())
            yield return null;
    }

    private void Close()
    {
        _routine = null;
        IsOpen = false;

        if (bodyText != null)
            bodyText.text = string.Empty;

        var callback = _onFinished;
        _onFinished = null;

        gameObject.SetActive(false);
        callback?.Invoke();
    }

    private TMP_Text FindText(string path)
    {
        var child = transform.Find(path);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }
}
