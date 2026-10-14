using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyAI))]
public class Phoenix : Enemy
{
    [Header("Mimic")]
    [SerializeField]
    private SpriteRenderer visual;
    [SerializeField]
    private Sprite perchedSprite;
    [SerializeField]
    private Sprite flyingSprite;
    [SerializeField]
    private bool artFacesLeft = true;

    [Header("Death")]
    [SerializeField, Min(0f)]
    private float deathLinger = 0.4f;

    public bool IsRevealed { get; private set; }
    public bool HasErupted { get; private set; }
    public bool IsDying { get; private set; }

    public EnemyAI AI { get; private set; }

    private bool _artFlipped;

    protected override void Awake()
    {
        base.Awake();

        AI = GetComponent<EnemyAI>();

        if (visual == null)
            visual = GetComponentInChildren<SpriteRenderer>();
        if (visual != null) {
            _artFlipped = visual.flipX;

            if (perchedSprite != null)
                visual.sprite = perchedSprite;
        }
    }

    private void Update()
    {
        // Holds the torch pose while disguised
        if (!IsRevealed || visual == null || Mathf.Abs(AI.Facing.x) < 0.1f)
            return;

        var turned = artFacesLeft ? AI.Facing.x > 0f : AI.Facing.x < 0f;
        visual.flipX = turned != _artFlipped;
    }

    public void Reveal()
    {
        if (IsRevealed)
            return;

        IsRevealed = true;

        if (visual != null && flyingSprite != null)
            visual.sprite = flyingSprite;
    }

    public void MarkErupted()
    {
        Reveal();
        HasErupted = true;
    }

    protected override void Die()
    {
        if (IsDying)
            return;

        var eruption = GetComponentInChildren<PhoenixEruption>(true);
        if (eruption == null || !isActiveAndEnabled) {
            base.Die();
            return;
        }

        StartCoroutine(DeathRoutine(eruption));
    }

    private IEnumerator DeathRoutine(PhoenixEruption eruption)
    {
        IsDying = true;

        AI.enabled = false;
        AI.Mover.Stop();
        CancelKnockback();

        Reveal();

        yield return eruption.Erupt(AI);

        if (deathLinger > 0f)
            yield return new WaitForSeconds(deathLinger);

        base.Die();
    }

    public void SetGroundPosition(Vector2 position)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);

        if (Body != null && Body.bodyType != RigidbodyType2D.Static)
            Body.position = position;
    }
}
