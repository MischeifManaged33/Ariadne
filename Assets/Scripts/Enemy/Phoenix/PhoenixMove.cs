using UnityEngine;

// Logic for moves
public abstract class PhoenixMove : EnemyAbility
{
    [Header("Targets")]
    [SerializeField]
    protected LayerMask targetLayers;
    [SerializeField]
    protected LayerMask wallLayers;

    protected Phoenix Phoenix { get; private set; }

    protected virtual void Reset()
    {
        targetLayers = PlayerMask();
        wallLayers = LayerMask.GetMask("Walls");
    }

    protected virtual void Awake()
    {
        Phoenix = GetComponentInParent<Phoenix>();

        if (targetLayers.value == 0)
            targetLayers = PlayerMask();
        if (wallLayers.value == 0)
            wallLayers = LayerMask.GetMask("Walls");
    }

    public override bool CanUse(EnemyAI ai, float distanceToTarget)
    {
        return Phoenix != null && Phoenix.IsAlive && Phoenix.HasErupted && base.CanUse(ai, distanceToTarget);
    }

    protected void MoveTo(EnemyAI ai, Vector2 position)
    {
        if (Phoenix != null)
            Phoenix.SetGroundPosition(position);
        else
            ai.transform.position = new Vector3(position.x, position.y, ai.transform.position.z);
    }

    protected float ClearDistance(Vector2 origin, Vector2 groundDirection, float groundDistance, float radius,
        float yScale)
    {
        if (wallLayers.value == 0)
            return groundDistance;

        var screen = Isometric.ToScreen(groundDirection * groundDistance, yScale);
        var screenLength = screen.magnitude;

        if (screenLength <= 0.0001f)
            return groundDistance;

        var hit = Physics2D.CircleCast(origin, radius, screen / screenLength, screenLength, wallLayers);
        if (hit.collider == null)
            return groundDistance;

        return groundDistance * Mathf.Max(0f, hit.distance - 0.05f) / screenLength;
    }

    protected static Vector2 Rotate(Vector2 vector, float degrees)
    {
        var radians = degrees * Mathf.Deg2Rad;
        var cos = Mathf.Cos(radians);
        var sin = Mathf.Sin(radians);

        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
    }

    protected static T CreateIndicator<T>(string name, string sortingLayer = "Enemy") where T : Component
    {
        var host = new GameObject(name);
        var indicator = host.AddComponent<T>();

        host.GetComponent<MeshRenderer>().sortingLayerName = sortingLayer;

        return indicator;
    }

    public static bool Detonate(Vector2 centre, float radius, float damage, float yScale, LayerMask targets,
        AttackSounds sounds)
    {
        sounds?.PlaySwing(centre);

        var mask = targets.value != 0 ? targets.value : Physics2D.AllLayers;

        foreach (var candidate in Physics2D.OverlapCircleAll(centre, radius, mask)) {
            var health = candidate.GetComponentInParent<PlayerHealth>();
            if (health == null)
                continue;

            var contact = candidate.ClosestPoint(centre);
            if (Isometric.ToGround(contact - centre, yScale).sqrMagnitude > radius * radius)
                continue;

            health.TakeDamage(damage);
            sounds?.PlayImpact(contact);

            return true;
        }

        return false;
    }
}
