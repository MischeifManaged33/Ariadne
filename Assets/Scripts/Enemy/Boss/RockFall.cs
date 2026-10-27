using System.Collections.Generic;
using UnityEngine;

// Generator for falling rocks
public class RockFall : MonoBehaviour
{
    [Header("Rocks")]
    [SerializeField, Min(1)]
    private int count = 4;
    [SerializeField]
    private bool firstOnTarget = true;
    [SerializeField, Min(0f)]
    private float scatterRadius = 3.5f;
    [SerializeField, Min(0f)]
    private float minSpacing = 1.4f;
    [SerializeField, Min(1)]
    private int maxGrounded = 6;
    [SerializeField]
    private BossRock.Settings rock = new BossRock.Settings();

    [Header("Timing")]
    [SerializeField, Min(0.1f)]
    private float warning = 1.1f;
    [SerializeField, Min(0f)]
    private float stagger = 0.25f;

    [Header("Placement")]
    [SerializeField]
    private LayerMask obstacleLayers;
    [SerializeField, Range(1, 32)]
    private int placementAttempts = 12;

    public bool HasRock {
        get {
            Prune();

            foreach (var candidate in _rocks) {
                if (candidate.CanPickUp)
                    return true;
            }

            return false;
        }
    }

    private readonly List<BossRock> _rocks = new();
    private readonly List<Vector2> _placed = new();

    private void Reset()
    {
        obstacleLayers = LayerMask.GetMask("Walls", "Default");
        rock.targetLayers = LayerMask.GetMask("Player");
    }

    private void Awake()
    {
        if (obstacleLayers.value == 0)
            obstacleLayers = LayerMask.GetMask("Walls", "Default");
        if (rock.targetLayers.value == 0)
            rock.targetLayers = LayerMask.GetMask("Player");
    }

    private void OnDestroy()
    {
        foreach (var candidate in _rocks) {
            if (candidate != null)
                Destroy(candidate.gameObject);
        }
    }

    public void Drop(Vector2 around, float yScale)
    {
        Prune();
        _placed.Clear();

        for (var i = 0; i < count; i++) {
            if (!PickPoint(around, i == 0 && firstOnTarget, yScale, out var point))
                continue;

            _placed.Add(point);

            var fallen = BossRock.Create(rock, point, yScale);
            fallen.Drop(warning + stagger * i);
            _rocks.Add(fallen);
        }

        Trim();
    }

    public BossRock Nearest(Vector2 from, float yScale)
    {
        Prune();

        BossRock nearest = null;
        var best = Mathf.Infinity;

        foreach (var candidate in _rocks) {
            if (!candidate.CanPickUp)
                continue;

            var distance = Isometric.GroundDistance(from, candidate.Position, yScale);
            if (distance >= best)
                continue;

            best = distance;
            nearest = candidate;
        }

        return nearest;
    }

    public bool Take(BossRock taken)
    {
        if (taken == null || !taken.CanPickUp || !_rocks.Remove(taken))
            return false;

        taken.PickUp();
        return true;
    }

    private bool PickPoint(Vector2 around, bool onTarget, float yScale, out Vector2 point)
    {
        if (onTarget && Clear(around, around, yScale)) {
            point = around;
            return true;
        }

        for (var attempt = 0; attempt < placementAttempts; attempt++) {
            var ground = Random.insideUnitCircle * scatterRadius;
            var candidate = around + Isometric.ToScreen(ground, yScale);

            if (!Clear(around, candidate, yScale))
                continue;

            point = candidate;
            return true;
        }

        point = around;
        return false;
    }

    private bool Clear(Vector2 around, Vector2 candidate, float yScale)
    {
        foreach (var other in _placed) {
            if (Isometric.GroundDistance(other, candidate, yScale) < minSpacing)
                return false;
        }

        foreach (var other in _rocks) {
            if (Isometric.GroundDistance(other.Position, candidate, yScale) < minSpacing)
                return false;
        }

        if (obstacleLayers.value == 0)
            return true;

        foreach (var hit in Physics2D.OverlapCircleAll(candidate, rock.radius * 0.5f, obstacleLayers)) {
            if (!hit.isTrigger)
                return false;
        }

        foreach (var hit in Physics2D.LinecastAll(around, candidate, obstacleLayers)) {
            if (!hit.collider.isTrigger)
                return false;
        }

        return true;
    }

    private void Trim()
    {
        var kept = 0;

        for (var i = _rocks.Count - 1; i >= 0; i--) {
            if (++kept <= maxGrounded)
                continue;

            _rocks[i].Discard();
            _rocks.RemoveAt(i);
        }
    }

    private void Prune() => _rocks.RemoveAll(candidate => candidate == null);
}
