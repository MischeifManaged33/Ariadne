using UnityEngine;

// Interact add it by hand, but can be manually added to adjust the look.
[DisallowMultipleComponent]
public class InteractableHighlight : MonoBehaviour
{
    [SerializeField]
    private SpriteRenderer target;
    [SerializeField]
    private Color color = Color.white;
    [SerializeField, Min(0.1f)]
    private float thickness = 3f;
    [SerializeField, Range(0f, 1f)]
    private float pulseAmount = 0.35f;
    [SerializeField, Min(0f)]
    private float pulseSpeed = 4f;

    private const string ShaderName = "Ariadne/SpriteSilhouette";

    private static readonly Vector2[] Offsets = {
        new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f),
        new(1f, 1f), new(1f, -1f), new(-1f, 1f), new(-1f, -1f)
    };

    private static Material _material;
    private static bool _missingShader;

    public bool IsHighlighted { get; private set; }

    private SpriteRenderer[] _outlines;

    private void Reset()
    {
        target = GetComponentInChildren<SpriteRenderer>();
    }

    private void Awake()
    {
        if (target == null)
            target = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnDisable() => SetHighlighted(false);

    public void SetHighlighted(bool highlighted)
    {
        if (highlighted == IsHighlighted)
            return;

        if (highlighted && !Build())
            return;

        IsHighlighted = highlighted;

        foreach (var outline in _outlines)
            outline.gameObject.SetActive(highlighted);

        if (highlighted)
            Sync();
    }

    private void LateUpdate()
    {
        if (IsHighlighted)
            Sync();
    }

    private void Sync()
    {
        if (target == null)
            return;

        var sprite = target.sprite;
        var visible = target.enabled && sprite != null;
        var step = sprite != null ? thickness / sprite.pixelsPerUnit : 0f;

        var tint = color;
        if (pulseAmount > 0f && pulseSpeed > 0f)
            tint.a *= 1f - pulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed));

        for (var i = 0; i < _outlines.Length; i++) {
            var outline = _outlines[i];
            outline.enabled = visible;
            if (!visible)
                continue;

            outline.sprite = sprite;
            outline.flipX = target.flipX;
            outline.flipY = target.flipY;
            outline.drawMode = target.drawMode;
            outline.size = target.size;
            outline.color = tint;
            outline.sortingLayerID = target.sortingLayerID;
            outline.sortingOrder = target.sortingOrder - 1;
            outline.transform.localPosition = Offsets[i] * step;
        }
    }

    private bool Build()
    {
        if (_outlines != null)
            return true;
        if (target == null || !LoadMaterial())
            return false;

        _outlines = new SpriteRenderer[Offsets.Length];

        for (var i = 0; i < Offsets.Length; i++) {
            var go = new GameObject("Highlight");
            go.transform.SetParent(target.transform, false);
            go.SetActive(false);

            var outline = go.AddComponent<SpriteRenderer>();
            outline.sharedMaterial = _material;
            outline.maskInteraction = target.maskInteraction;
            _outlines[i] = outline;
        }

        return true;
    }

    private static bool LoadMaterial()
    {
        if (_material != null)
            return true;
        if (_missingShader)
            return false;

        var shader = Shader.Find(ShaderName);
        if (shader == null) {
            _missingShader = true;
            Debug.LogWarning($"Shader '{ShaderName}' not found, interactable highlights disabled.");
            return false;
        }

        _material = new Material(shader) { name = "Interactable Highlight" };
        return true;
    }

    public static InteractableHighlight GetOrAdd(GameObject target)
    {
        var highlight = target.GetComponent<InteractableHighlight>();
        return highlight != null ? highlight : target.AddComponent<InteractableHighlight>();
    }
}
