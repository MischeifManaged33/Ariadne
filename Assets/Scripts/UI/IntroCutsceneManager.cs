using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Shows the starting screen
public class IntroCutsceneManager : MonoBehaviour
{
    [Header("Screen")]
    [SerializeField]
    private Sprite image;
    [SerializeField]
    private bool keepAspect;
    [SerializeField]
    private int sortingOrder = 1000;
    [SerializeField, Min(0f)]
    private float fadeDuration = 0.25f;
    private bool dismissOnClick = true;

    [Header("Player")]
    [SerializeField]
    private Player player;
    [SerializeField]
    private bool freezeWorld = true;

    public static bool IsShowing { get; private set; }

    public event Action Finished;

    private InputAction _continueAction;
    private GameObject _screen;
    private CanvasGroup _group;
    private readonly List<Behaviour> _locked = new();
    private bool _dismissing;

    private void Awake()
    {
        _continueAction = new InputAction("Continue", InputActionType.Button);
        _continueAction.AddBinding("<Keyboard>/e");
        _continueAction.AddBinding("<Gamepad>/buttonSouth");
        if (dismissOnClick)
            _continueAction.AddBinding("<Pointer>/press");

        if (player == null)
            player = FindAnyObjectByType<Player>();

        Show();
    }

    private void OnEnable() => _continueAction.Enable();

    private void OnDisable() => _continueAction.Disable();

    private void OnDestroy()
    {
        _continueAction?.Dispose();

        if (IsShowing)
            Release();
    }

    private void Update()
    {
        if (IsShowing && !_dismissing && _continueAction.WasPressedThisFrame())
            StartCoroutine(FadeOut());
    }

    private void Show()
    {
        IsShowing = true;

        if (image != null)
            BuildScreen();

        LockPlayer();

        if (freezeWorld)
            Time.timeScale = 0f;
    }

    private void BuildScreen()
    {
        _screen = new GameObject("Intro Screen", typeof(RectTransform));
        _screen.transform.SetParent(transform, false);

        var canvas = _screen.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        _screen.AddComponent<GraphicRaycaster>();

        _group = _screen.AddComponent<CanvasGroup>();
        _group.alpha = 1f;
        _group.blocksRaycasts = true;

        var imageObject = new GameObject("Image", typeof(RectTransform));
        imageObject.transform.SetParent(_screen.transform, false);

        var rect = (RectTransform)imageObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var graphic = imageObject.AddComponent<Image>();
        graphic.sprite = image;

        if (keepAspect) {
            var fitter = imageObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = image.rect.width / image.rect.height;
        }
    }

    private IEnumerator FadeOut()
    {
        _dismissing = true;

        if (_group != null) {
            _group.blocksRaycasts = false;

            for (var elapsed = 0f; elapsed < fadeDuration; elapsed += Time.unscaledDeltaTime) {
                _group.alpha = 1f - elapsed / fadeDuration;
                yield return null;
            }

            Destroy(_screen);
        }

        Release();
        Finished?.Invoke();
    }

    private void Release()
    {
        IsShowing = false;

        if (freezeWorld)
            Time.timeScale = 1f;

        foreach (var behaviour in _locked) {
            if (behaviour != null)
                behaviour.enabled = true;
        }

        _locked.Clear();
    }

    private void LockPlayer()
    {
        if (player == null)
            return;

        Lock(player.GetComponentsInChildren<PlayerController>());
        Lock(player.GetComponentsInChildren<PlayerWeapon>());
        Lock(player.GetComponentsInChildren<PlayerInteractor>());

        if (player.TryGetComponent<Rigidbody2D>(out var body))
            body.linearVelocity = Vector2.zero;
    }

    private void Lock(IEnumerable<Behaviour> behaviours)
    {
        foreach (var behaviour in behaviours) {
            if (!behaviour.enabled)
                continue;

            behaviour.enabled = false;
            _locked.Add(behaviour);
        }
    }
}
