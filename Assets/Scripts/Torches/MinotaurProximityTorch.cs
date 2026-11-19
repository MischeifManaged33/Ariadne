using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MinotaurProximityTorch : MonoBehaviour
{
    [Header("Distance Sampling")]
    [Tooltip("Optional ground-level marker. Defaults to this object's position. " +
        "Useful when a torch's artwork sits high above its floor position.")]
    [SerializeField] private Transform distanceOrigin;

    [Header("Static Sprite Mode")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite farSprite;
    [SerializeField] private Sprite nearbySprite;
    [SerializeField] private Sprite closeSprite;

    [Header("Optional Animated Mode")]
    [Tooltip("Enable when an Animator controls the flame sprite. Use an Integer parameter: " +
        "0 = Far, 1 = Nearby, 2 = Close. Static sprites above are ignored in this mode.")]
    [SerializeField] private bool useAnimatorParameter;
    [SerializeField] private Animator targetAnimator;
    [SerializeField] private string proximityParameter = "Proximity";

    public int CurrentBand { get; private set; } = -1;
    private Sprite originalSprite;
    private int parameterId;
    private bool animatorParameterValid;
    private static readonly HashSet<MinotaurProximityTorch> active = new HashSet<MinotaurProximityTorch>();

    private void Awake()
    {
        if (targetRenderer == null) targetRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (targetAnimator == null) targetAnimator = GetComponentInChildren<Animator>(true);
        if (targetRenderer != null) originalSprite = targetRenderer.sprite;
        CacheAnimatorParameter();
    }

    private void CacheAnimatorParameter()
    {
        animatorParameterValid = false;
        parameterId = Animator.StringToHash(proximityParameter ?? "");
        if (!useAnimatorParameter || targetAnimator == null) return;
        foreach (AnimatorControllerParameter parameter in targetAnimator.parameters)
            if (parameter.nameHash == parameterId && parameter.type == AnimatorControllerParameterType.Int)
            { animatorParameterValid = true; break; }
        if (!animatorParameterValid)
            Debug.LogWarning("Torch needs an Animator Integer parameter named '" + proximityParameter + "'.", this);
    }

    private void OnEnable()
    {
        active.Add(this);
        CurrentBand = -1;
        Refresh(MazeTorchGuidance.Current);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    internal static void RefreshAll(MazeTorchGuidance guidance)
    {
        // A sprite-change callback may enable/disable a torch. Snapshot the
        // registry first rather than mutating it during enumeration.
        var snapshot = new MinotaurProximityTorch[active.Count];
        active.CopyTo(snapshot);
        foreach (MinotaurProximityTorch torch in snapshot)
            if (torch != null && torch.isActiveAndEnabled) torch.Refresh(guidance);
    }

    private void Refresh(MazeTorchGuidance guidance)
    {
        int band = guidance != null && guidance.isActiveAndEnabled
            ? guidance.BandAt(distanceOrigin != null ? distanceOrigin.position : transform.position) : 0;
        if (band == CurrentBand) return;
        CurrentBand = band;
        if (useAnimatorParameter)
        {
            if (targetAnimator != null && animatorParameterValid) targetAnimator.SetInteger(parameterId, band);
            return;
        }
        if (targetRenderer == null) return;
        Sprite fallback = farSprite != null ? farSprite : originalSprite;
        Sprite selected = fallback;
        if (band >= 1 && nearbySprite != null) selected = nearbySprite;
        if (band >= 2 && closeSprite != null) selected = closeSprite;
        targetRenderer.sprite = selected;
    }
}
