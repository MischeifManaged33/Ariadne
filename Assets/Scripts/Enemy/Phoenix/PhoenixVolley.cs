using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Trishot fireball
public class PhoenixVolley : PhoenixMove
{
    [Header("Volley")]
    [SerializeField, Range(1, 9)]
    private int count = 3;
    [SerializeField, Range(0f, 90f)]
    private float spread = 30f;
    [SerializeField, Min(0.5f)]
    private float range = 6f;

    [Header("Fireball")]
    [SerializeField, Min(0.1f)]
    private float speed = 8f;
    [SerializeField, Min(0.05f)]
    private float hitRadius = 0.25f;
    [SerializeField, Min(0.1f)]
    private float blastRadius = 1.2f;
    [SerializeField, Min(0f)]
    private float damage = 15f;
    [SerializeField]
    private Color color = new Color(1f, 0.55f, 0.1f, 1f);
    [SerializeField, Min(0.05f)]
    private float size = 0.35f;

    [Header("Timing")]
    [SerializeField, Min(0.05f)]
    private float windup = 0.8f;
    [SerializeField, Range(0f, 1f)]
    private float trackFraction = 0.6f;
    [SerializeField, Min(0f)]
    private float recovery = 0.6f;

    [Header("Telegraph")]
    [SerializeField]
    private LineIndicator[] indicators;

    [Header("Audio")]
    [SerializeField]
    private AttackSounds sounds = new AttackSounds();

    private LineIndicator[] _lines;
    private readonly List<LineIndicator> _runtime = new();

    private Vector2[] _directions;
    private float[] _lengths;

    protected override void Reset()
    {
        base.Reset();

        MinRange = 0f;
        MaxRange = 9f;
        Cooldown = 1.5f;
    }

    protected override void Awake()
    {
        base.Awake();

        _directions = new Vector2[count];
        _lengths = new float[count];
        _lines = new LineIndicator[count];

        for (var i = 0; i < count; i++) {
            var assigned = indicators != null && i < indicators.Length ? indicators[i] : null;

            if (assigned == null) {
                assigned = CreateIndicator<LineIndicator>("Phoenix Volley Indicator");
                _runtime.Add(assigned);
            }

            _lines[i] = assigned;
        }
    }

    private void OnDestroy()
    {
        foreach (var line in _runtime) {
            if (line != null)
                Destroy(line.gameObject);
        }
    }

    public override IEnumerator Execute(EnemyAI ai)
    {
        var aim = Aim(ai);
        Spread(ai, aim);

        sounds.PlayWindup(ai.Position);

        for (var elapsed = 0f; elapsed < windup; elapsed += Time.deltaTime) {
            if (elapsed < windup * trackFraction) {
                aim = Aim(ai);
                Spread(ai, aim);
            }

            ai.FaceTowards(aim);
            Telegraph(ai, true);

            yield return null;
        }

        Telegraph(ai, false);

        var origin = ai.Position;

        for (var i = 0; i < count; i++) {
            _lines[i].Flash();

            var end = origin + Isometric.ToScreen(_directions[i] * _lengths[i], ai.YScale);

            PhoenixFireball.Launch(origin, end, speed, hitRadius, blastRadius, damage, ai.YScale, targetLayers,
                sounds, color, size);
        }

        yield return new WaitForSeconds(recovery);
    }

    public override void Cancel()
    {
        if (_lines == null)
            return;

        foreach (var line in _lines) {
            if (line != null)
                line.SetVisible(false);
        }
    }

    private static Vector2 Aim(EnemyAI ai)
    {
        var direction = ai.DirectionToTarget;
        return direction != Vector2.zero ? direction : ai.Facing;
    }

    private void Spread(EnemyAI ai, Vector2 aim)
    {
        var middle = (count - 1) * 0.5f;

        for (var i = 0; i < count; i++) {
            _directions[i] = Rotate(aim, (i - middle) * spread).normalized;
            _lengths[i] = ClearDistance(ai.Position, _directions[i], range, hitRadius, ai.YScale);
        }
    }

    private void Telegraph(EnemyAI ai, bool visible)
    {
        for (var i = 0; i < count; i++) {
            _lines[i].Aim(ai.Position, _directions[i], _lengths[i], ai.YScale);
            _lines[i].SetVisible(visible);
        }
    }
}
