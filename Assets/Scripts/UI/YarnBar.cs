using UnityEngine;
using UnityEngine.UI;

public class YarnBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private AriadneThread thread;
    [SerializeField]
    private Image fillImage;

    [Header("Easing")]
    [SerializeField, Min(0f)]
    private float fillSpeed = 14f;

    private float _target = 1f;
    private float _displayed = 1f;

    private void Reset()
    {
        var fill = transform.Find("Fill");
        if (fill != null)
            fillImage = fill.GetComponent<Image>();
    }

    private void OnEnable()
    {
        TryBind();
    }

    private void OnDisable()
    {
        if (thread != null)
            thread.ThreadAmountChanged -= OnThreadChanged;
    }

    private void Update()
    {
        if (thread == null && !TryBind())
            return;

        _displayed = Ease(_displayed, _target, fillSpeed, Time.unscaledDeltaTime);
        ApplyFill(_displayed);
    }

    private bool TryBind()
    {
        if (thread == null)
            thread = FindAnyObjectByType<AriadneThread>();

        if (thread == null)
            return false;

        thread.ThreadAmountChanged -= OnThreadChanged;
        thread.ThreadAmountChanged += OnThreadChanged;

        _target = thread.RemainingFraction;
        _displayed = _target;
        ApplyFill(_displayed);
        return true;
    }

    private void OnThreadChanged(float remaining, float max)
    {
        _target = max > 0f ? Mathf.Clamp01(remaining / max) : 0f;
    }

    private void ApplyFill(float normalized)
    {
        if (fillImage != null)
            fillImage.fillAmount = normalized;
    }

    private static float Ease(float from, float to, float speed, float dt)
    {
        if (speed <= 0f || dt <= 0f)
            return to;

        return Mathf.Lerp(from, to, 1f - Mathf.Exp(-speed * dt));
    }
}
