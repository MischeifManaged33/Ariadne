using System;
using System.Collections;
using UnityEngine;

// Regrowing hydra
[RequireComponent(typeof(EnemyAI))]
public class Hydra : Enemy
{
    [Serializable]
    public class Stage
    {
        public Sprite sprite;
        [Min(1)]
        public int lunges = 1;
        [Range(0f, 1f)]
        public float regrowChance;
    }

    [Header("Stages")]
    [SerializeField]
    private Stage[] stages = {
        new Stage { lunges = 1, regrowChance = 0.7f },
        new Stage { lunges = 2, regrowChance = 0.1f },
        new Stage { lunges = 5, regrowChance = 0f }
    };

    [Header("Regrow")]
    [SerializeField, Min(0f)]
    private float regrowDuration = 1f;
    [SerializeField, Min(0f)]
    private float regrowPopScale = 1.25f;
    [SerializeField]
    private AttackSounds regrowSounds = new AttackSounds();

    [Header("Visual")]
    [SerializeField]
    private SpriteRenderer visual;
    [SerializeField]
    private bool artFacesLeft = true;

    public event Action<int> StageChanged;

    public int StageIndex { get; private set; }
    public int Lunges => CurrentStage != null ? Mathf.Max(1, CurrentStage.lunges) : 1;
    public bool IsRegrowing { get; private set; }

    private Stage CurrentStage => stages != null && stages.Length > 0 ? stages[StageIndex] : null;

    private EnemyAI _ai;
    private EnemyMover _mover;
    private bool _artFlipped;
    private Vector3 _visualScale = Vector3.one;

    protected override void Awake()
    {
        base.Awake();

        _ai = GetComponent<EnemyAI>();
        _mover = GetComponent<EnemyMover>();

        if (visual == null)
            visual = GetComponentInChildren<SpriteRenderer>();
        if (visual != null) {
            _artFlipped = visual.flipX;
            _visualScale = visual.transform.localScale;
        }

        ApplyStage();
    }

    private void Update()
    {
        if (visual == null || _ai == null || Mathf.Abs(_ai.Facing.x) < 0.1f)
            return;

        var turned = artFacesLeft ? _ai.Facing.x > 0f : _ai.Facing.x < 0f;
        visual.flipX = turned != _artFlipped;
    }

    public override float TakeDamage(float amount)
    {
        return IsRegrowing ? 0f : base.TakeDamage(amount);
    }

    public override void ApplyKnockback(Vector2 direction, float strength = 1f)
    {
        if (!IsRegrowing)
            base.ApplyKnockback(direction, strength);
    }

    protected override void Die()
    {
        var stage = CurrentStage;
        var hasNext = stages != null && StageIndex + 1 < stages.Length;

        if (stage != null && hasNext && UnityEngine.Random.value < stage.regrowChance) {
            StartCoroutine(Regrow());
            return;
        }

        base.Die();
    }

    private IEnumerator Regrow()
    {
        IsRegrowing = true;

        ResetHealth();
        CancelKnockback();

        _ai.enabled = false;
        if (_mover != null)
            _mover.Stop();

        StageIndex++;
        ApplyStage();
        StageChanged?.Invoke(StageIndex);

        regrowSounds.PlayWindup(transform.position);

        for (var elapsed = 0f; elapsed < regrowDuration; elapsed += Time.deltaTime) {
            if (visual != null) {
                var t = elapsed / regrowDuration;
                var pop = 1f + (regrowPopScale - 1f) * Mathf.Sin(t * Mathf.PI);
                visual.transform.localScale = _visualScale * pop;
            }

            yield return null;
        }

        if (visual != null)
            visual.transform.localScale = _visualScale;

        regrowSounds.PlayImpact(transform.position);

        IsRegrowing = false;
        _ai.enabled = true;
        _ai.Alert();
    }

    private void ApplyStage()
    {
        var stage = CurrentStage;

        if (stage != null && stage.sprite != null && visual != null)
            visual.sprite = stage.sprite;
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        if (stages != null && stages.Length > 0)
            StageIndex = Mathf.Clamp(StageIndex, 0, stages.Length - 1);
    }
}
