using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public enum JoystickRole
{
    Move,
    Aim
}

[RequireComponent(typeof(RectTransform))]
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField]
    private RectTransform background;
    [SerializeField]
    private RectTransform handle;

    [Header("UI")]
    [Tooltip("How far head travels from center at full tilt")]
    [SerializeField, Min(1f)]
    private float handleRange = 75f;
    [SerializeField, Range(0f, 0.9f)]
    private float deadZone = 0.1f;

    [Header("Movement")]
    [SerializeField]
    private JoystickRole role = JoystickRole.Move;
    [SerializeField]
    private bool floating = true;
    [SerializeField]
    private bool hideWhenIdle = true;
    [SerializeField]
    private bool mobileOnly = false;

    private static readonly Dictionary<JoystickRole, VirtualJoystick> Sticks = new();

    public static VirtualJoystick Get(JoystickRole role) => Sticks.TryGetValue(role, out var stick) ? stick : null;
    public static VirtualJoystick Move => Get(JoystickRole.Move);
    public static VirtualJoystick Aim => Get(JoystickRole.Aim);

    public Vector2 Direction { get; private set; }
    public bool IsPressed { get; private set; }
    public Vector2 ReleasedDirection { get; private set; }

    public event Action Pressed;
    public event Action<Vector2> Released;

    private RectTransform _root;
    private CanvasGroup _canvasGroup;
    private Vector2 _restPosition;

    private void Awake()
    {
        _root = (RectTransform)transform;

        if (mobileOnly && !Application.isMobilePlatform) {
            gameObject.SetActive(false);
            return;
        }

        if (background == null)
            Debug.LogError("No background assigned.", this);
        else
            _restPosition = background.anchoredPosition;

        _canvasGroup = background != null ? background.GetComponent<CanvasGroup>() : null;
        if (_canvasGroup == null && background != null)
            _canvasGroup = background.gameObject.AddComponent<CanvasGroup>();

        Sticks[role] = this;
        SetVisible(!hideWhenIdle);
    }

    private void OnDestroy()
    {
        if (Get(role) == this)
            Sticks.Remove(role);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (background == null)
            return;

        if (floating && RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, eventData.position, 
            eventData.pressEventCamera, out var local)) {
            background.anchoredPosition = local;
        }

        IsPressed = true;

        SetVisible(true);
        OnDrag(eventData);

        Pressed?.Invoke();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (background == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, 
            eventData.pressEventCamera, out var local)) {
            return;
        }

        var raw = Vector2.ClampMagnitude(local / handleRange, 1f);
        Direction = ApplyDeadZone(raw);

        if (handle != null)
            handle.anchoredPosition = raw * handleRange;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ReleasedDirection = Direction;
        IsPressed = false;
        Direction = Vector2.zero;

        if (handle != null)
            handle.anchoredPosition = Vector2.zero;
        if (background != null && floating)
            background.anchoredPosition = _restPosition;

        SetVisible(!hideWhenIdle);

        Released?.Invoke(ReleasedDirection);
    }

    /// Rescalling due to dead zone
    private Vector2 ApplyDeadZone(Vector2 raw)
    {
        var magnitude = raw.magnitude;
        if (magnitude <= deadZone)
            return Vector2.zero;

        return raw / magnitude * ((magnitude - deadZone) / (1f - deadZone));
    }

    private void SetVisible(bool visible)
    {
        if (_canvasGroup != null)
            _canvasGroup.alpha = visible ? 1f : 0f;
    }
}
