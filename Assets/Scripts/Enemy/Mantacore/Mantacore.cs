using UnityEngine;

[RequireComponent(typeof(EnemyAI))]
public class Mantacore : Enemy
{
    [Header("Visual")]
    [SerializeField]
    private SpriteRenderer visual;
    [SerializeField]
    private SpriteRenderer shadow;
    [SerializeField]
    private bool artFacesLeft = true;

    private EnemyAI _ai;
    private bool _artFlipped;

    protected override void Awake()
    {
        base.Awake();

        _ai = GetComponent<EnemyAI>();

        if (visual == null)
            visual = GetComponent<SpriteRenderer>();
        if (visual != null)
            _artFlipped = visual.flipX;

    }

    private void Update()
    {
        if (_ai == null || Mathf.Abs(_ai.Facing.x) < 0.1f)
            return;

        var turned = artFacesLeft ? _ai.Facing.x > 0f : _ai.Facing.x < 0f;

        if (visual != null)
            visual.flipX = turned != _artFlipped;

    }
}
