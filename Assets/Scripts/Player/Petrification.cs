using System.Collections.Generic;
using UnityEngine;

// Turns the player to stone: no moving, attacking or interacting until whatever petrified them lets go.
// Added to the player at runtime by whoever petrifies them first
[DisallowMultipleComponent]
public class Petrification : MonoBehaviour
{
    [SerializeField]
    private Color stoneTint = new Color(0.55f, 0.57f, 0.62f, 1f);

    // A reference check, not Unity's null: a destroyed holder still counts until Update lets go
    public bool IsPetrified => _holder is not null;

    private readonly List<Behaviour> _locked = new();
    private readonly List<SpriteRenderer> _renderers = new();
    private readonly List<Color> _colors = new();
    private readonly List<Animator> _animators = new();
    private readonly List<float> _speeds = new();

    private Rigidbody2D _body;
    private RigidbodyConstraints2D _constraints;
    private MonoBehaviour _holder;
    private float _releaseAt;

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
    }

    // Fails if something else already holds the player, so two gazes can't stack
    public bool TryPetrify(MonoBehaviour holder, float maxDuration)
    {
        if (holder == null || IsPetrified)
            return false;

        _holder = holder;
        _releaseAt = Time.time + Mathf.Max(0.1f, maxDuration);

        Lock(GetComponentsInChildren<PlayerController>());
        Lock(GetComponentsInChildren<PlayerWeapon>());
        Lock(GetComponentsInChildren<PlayerInteractor>());

        if (_body != null) {
            _constraints = _body.constraints;
            _body.linearVelocity = Vector2.zero;
            _body.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        foreach (var animator in GetComponentsInChildren<Animator>()) {
            _animators.Add(animator);
            _speeds.Add(animator.speed);
            animator.speed = 0f;
        }

        foreach (var sprite in GetComponentsInChildren<SpriteRenderer>()) {
            _renderers.Add(sprite);
            _colors.Add(sprite.color);
        }

        return true;
    }

    public void Release(MonoBehaviour holder)
    {
        if (IsPetrified && holder == _holder)
            ReleaseNow();
    }

    private void Update()
    {
        // A holder that died or was disabled mid-hold can't release us, so let go ourselves
        if (!IsPetrified)
            return;

        if (_holder == null || !_holder.isActiveAndEnabled || Time.time >= _releaseAt)
            ReleaseNow();
    }

    // PlayerHealth puts the sprite colour back every Update, so the tint goes on after it
    private void LateUpdate()
    {
        if (!IsPetrified)
            return;

        for (var i = 0; i < _renderers.Count; i++) {
            if (_renderers[i] != null)
                _renderers[i].color = _colors[i] * stoneTint;
        }
    }

    private void OnDisable()
    {
        if (IsPetrified)
            ReleaseNow();
    }

    private void ReleaseNow()
    {
        _holder = null;

        foreach (var behaviour in _locked) {
            if (behaviour != null)
                behaviour.enabled = true;
        }

        if (_body != null)
            _body.constraints = _constraints;

        for (var i = 0; i < _animators.Count; i++) {
            if (_animators[i] != null)
                _animators[i].speed = _speeds[i];
        }

        for (var i = 0; i < _renderers.Count; i++) {
            if (_renderers[i] != null)
                _renderers[i].color = _colors[i];
        }

        _locked.Clear();
        _animators.Clear();
        _speeds.Clear();
        _renderers.Clear();
        _colors.Clear();
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

    public static Petrification GetOrAdd(GameObject target)
    {
        var stone = target.GetComponent<Petrification>();
        return stone != null ? stone : target.AddComponent<Petrification>();
    }
}
