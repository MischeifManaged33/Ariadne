using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class Hurtbox : MonoBehaviour
{
    public const string ChildName = "Hurtbox";

    [SerializeField]
    private SpriteRenderer sprite;

    private static readonly List<Collider2D> Colliders = new();

    private Collider2D _collider;
    private Vector3 _homePosition;
    private Vector3 _homeScale;
    private bool _authoredFlip;
    private bool _mirrored;

    public Collider2D Collider => _collider != null ? _collider : _collider = GetComponent<Collider2D>();

    private void Awake()
    {
        if (sprite == null && transform.parent != null)
            sprite = transform.parent.GetComponentInParent<SpriteRenderer>();

        _homePosition = transform.localPosition;
        _homeScale = transform.localScale;
        _authoredFlip = sprite != null && sprite.flipX;
    }

    private void LateUpdate()
    {
        if (sprite == null)
            return;

        var mirrored = sprite.flipX != _authoredFlip;
        if (mirrored != _mirrored)
            SetMirrored(mirrored);
    }

    private void SetMirrored(bool mirrored)
    {
        _mirrored = mirrored;

        var position = _homePosition;
        var scale = _homeScale;

        if (mirrored) {
            var pivot = transform.parent != null
                ? transform.parent.InverseTransformPoint(sprite.transform.position).x
                : sprite.transform.position.x;

            position.x = 2f * pivot - position.x;
            scale.x = -scale.x;
        }

        transform.localPosition = position;
        transform.localScale = scale;
    }

    public static Hurtbox Find(Component owner)
    {
        if (owner == null)
            return null;

        var marked = owner.GetComponentInChildren<Hurtbox>(true);
        if (marked != null)
            return marked;

        owner.GetComponentsInChildren(true, Colliders);

        foreach (var candidate in Colliders) {
            if (candidate.transform != owner.transform && candidate.name == ChildName) {
                Colliders.Clear();
                return candidate.gameObject.AddComponent<Hurtbox>();
            }
        }

        Colliders.Clear();
        return null;
    }

    public static bool Accepts(IDamagable target, Collider2D hit)
    {
        var hurtbox = Find(target as Component);
        return hurtbox == null || hurtbox.Collider == hit;
    }
}
