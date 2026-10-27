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

    public void SetGroundPosition(Vector2 position)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);

        if (Body != null && Body.bodyType != RigidbodyType2D.Static)
            Body.position = position;
    }
}
