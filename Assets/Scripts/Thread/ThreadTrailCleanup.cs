using System;
using System.Collections.Generic;
using UnityEngine;

// Rendering only: AriadneThread continues to own length, knots and recovery.
[DisallowMultipleComponent]
public class ThreadTrailCleanup : MonoBehaviour
{
    [Header("Calmer Thread")]
    [SerializeField, Range(0f, 1f)] private float flutterMultiplier = 0.15f;
    [SerializeField, Min(0f)] private float simplifyTolerance = 0.025f;

    [Header("Overlapping Passes")]
    [SerializeField, Min(0f)] private float mergeDistance = 0.04f;
    [SerializeField, Range(0f, 45f)] private float mergeAngle = 18f;
    [Tooltip("Keep the strand visible immediately around each tied knot.")]
    [SerializeField, Min(0f)] private float knotKeepRadius = 0.10f;

    [Header("Shortcut Checks")]
    [SerializeField] private DesignedDungeonGenerator dungeon;
    [Tooltip("Wall/collision layers that a simplified or rounded strand may not cross. " +
        "Automatically uses Walls when this is empty. Do not include the Player layer.")]
    [SerializeField] private LayerMask blockingLayers;

    public float FlutterMultiplier => flutterMultiplier;
    private const float Small = 0.00001f;

    private struct Span
    {
        public float start, end;
        public Span(float a, float b) { start = a; end = b; }
    }
    private struct IndexRange
    {
        public int start, end;
        public IndexRange(int a, int b) { start = a; end = b; }
    }
    private struct Segment
    {
        public Vector3 a, b;
        public Segment(Vector3 start, Vector3 end) { a = start; b = end; }
    }

    private readonly List<Vector3> simplified = new List<Vector3>();
    private readonly List<Vector3> smooth = new List<Vector3>();
    private readonly List<Vector3> anchors = new List<Vector3>();
    private readonly List<Vector3> stroke = new List<Vector3>();
    private readonly List<LineRenderer> renderers = new List<LineRenderer>();
    private readonly List<Segment> canonical = new List<Segment>();
    private readonly Dictionary<Vector2Int, List<int>> grid = new Dictionary<Vector2Int, List<int>>();
    private readonly Stack<List<int>> bucketPool = new Stack<List<int>>();
    private readonly HashSet<int> nearby = new HashSet<int>();
    private readonly HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
    private readonly List<Span> coverage = new List<Span>();
    private readonly Stack<IndexRange> ranges = new Stack<IndexRange>();
    private readonly List<Vector3> rounded = new List<Vector3>();
    private bool[] keep = new bool[0];
    private bool[] pinned = new bool[0];
    private Vector3[] buffer = new Vector3[0];
    private LineRenderer primary;
    private int strokeCount;
    private float cellSize;

    private void Awake()
    {
        if (dungeon == null) dungeon = FindAnyObjectByType<DesignedDungeonGenerator>();
        if (blockingLayers.value == 0) blockingLayers = LayerMask.GetMask("Walls");
    }

    public void Clear()
    {
        if (primary != null) primary.positionCount = 0;
        foreach (LineRenderer renderer in renderers)
            if (renderer != null) renderer.enabled = false;
    }

    public void Render(List<Vector3> path, List<int> knotIndices, LineRenderer source,
        float rounding, int cornerSamples)
    {
        primary = source;
        strokeCount = 0;
        stroke.Clear();
        if (path.Count < 2 || !source.enabled) { Clear(); return; }
        anchors.Clear();
        if (keep.Length < path.Count)
        {
            int size = Mathf.NextPowerOfTwo(path.Count);
            keep = new bool[size];
            pinned = new bool[size];
        }
        Array.Clear(keep, 0, path.Count);
        Array.Clear(pinned, 0, path.Count);
        pinned[0] = pinned[path.Count - 1] = true;
        foreach (int index in knotIndices)
        {
            if (index < 0 || index >= path.Count) continue;
            pinned[index] = true;
            anchors.Add(path[index]);
        }
        int start = 0;
        for (int end = 1; end < path.Count; end++)
        {
            if (!pinned[end]) continue;
            Simplify(path, start, end);
            start = end;
        }
        simplified.Clear();
        // Store protected positions separately, so subsequent corner rounding
        // and overlap clipping cannot erase an actual tied vertex.
        for (int i = 0; i < path.Count; i++)
            if (keep[i]) simplified.Add(path[i]);
        RoundCorners(rounding, cornerSamples);

        canonical.Clear();
        foreach (List<int> bucket in grid.Values) { bucket.Clear(); bucketPool.Push(bucket); }
        grid.Clear();
        cellSize = Mathf.Max(0.05f, mergeDistance * 2f);
        for (int i = 1; i < smooth.Count; i++) DrawSegment(smooth[i - 1], smooth[i]);
        Flush();
        if (strokeCount == 0) primary.positionCount = 0;
        for (int i = Mathf.Max(0, strokeCount - 1); i < renderers.Count; i++)
            if (renderers[i] != null) renderers[i].enabled = false;
    }

    private void Simplify(List<Vector3> path, int start, int end)
    {
        ranges.Clear();
        ranges.Push(new IndexRange(start, end));
        while (ranges.Count > 0)
        {
            IndexRange range = ranges.Pop();
            keep[range.start] = keep[range.end] = true;
            if (range.end <= range.start + 1) continue;
            float worst = -1f;
            int split = range.start + 1;
            for (int i = range.start + 1; i < range.end; i++)
            {
                float error = DistanceSquared(path[i], path[range.start], path[range.end]);
                if (error > worst) { worst = error; split = i; }
            }
            // Exact collinear removal follows the original covered segment.
            // Every actual shortcut also needs geometry checks.
            if (worst <= simplifyTolerance * simplifyTolerance &&
                (worst < Small * Small || SafeSegment(path[range.start], path[range.end]))) continue;
            if (worst < Small * Small) split = (range.start + range.end) / 2;
            ranges.Push(new IndexRange(range.start, split));
            ranges.Push(new IndexRange(split, range.end));
        }
    }

    private bool SafeSegment(Vector3 a, Vector3 b)
    {
        // Without a ready floor source, retain the original route instead of
        // inventing a straight shortcut through unseen geometry.
        if (dungeon == null || !dungeon.WalkableFloorReady) return false;
        if (dungeon.GetWalkableMoveFraction(a, b) < 0.99999f) return false;
        return blockingLayers.value == 0 || Physics2D.Linecast(a, b, blockingLayers).collider == null;
    }

    private bool IsAnchor(Vector3 point)
    {
        foreach (Vector3 anchor in anchors)
            if ((point - anchor).sqrMagnitude < Small * Small) return true;
        return false;
    }

    private void RoundCorners(float amount, int samples)
    {
        smooth.Clear();
        smooth.Add(simplified[0]);
        for (int i = 1; i < simplified.Count - 1; i++)
        {
            Vector3 p = simplified[i], previous = simplified[i - 1], next = simplified[i + 1];
            float before = Vector3.Distance(previous, p), after = Vector3.Distance(p, next);
            if (amount <= 0f || IsAnchor(p) || before < Small || after < Small)
            { smooth.Add(p); continue; }
            Vector3 entry = Vector3.Lerp(p, previous, Mathf.Min(0.45f, amount / before));
            Vector3 exit = Vector3.Lerp(p, next, Mathf.Min(0.45f, amount / after));
            rounded.Clear();
            rounded.Add(entry);
            for (int j = 1; j <= Mathf.Max(1, samples); j++)
            {
                float t = (float)j / Mathf.Max(1, samples);
                rounded.Add((1f - t) * (1f - t) * entry + 2f * (1f - t) * t * p + t * t * exit);
            }
            bool safe = SafeSegment(smooth[smooth.Count - 1], entry);
            for (int j = 1; safe && j < rounded.Count; j++)
                safe = SafeSegment(rounded[j - 1], rounded[j]);
            // Also check the outgoing piece; rounding alone must not create
            // a tiny bridge across a room corner or a gap in the floor.
            if (safe) safe = SafeSegment(exit, next);
            if (safe) smooth.AddRange(rounded);
            else smooth.Add(p);
        }
        smooth.Add(simplified[simplified.Count - 1]);
    }

    private void DrawSegment(Vector3 a, Vector3 b)
    {
        float length = Vector2.Distance(a, b);
        if (length <= Small) return;
        Vector3 direction = (b - a) / length;
        coverage.Clear();
        if (mergeDistance > 0f)
        {
            Gather(a, b, false, -1);
            float alignment = Mathf.Cos(mergeAngle * Mathf.Deg2Rad);
            foreach (int index in nearby)
            {
                Segment old = canonical[index];
                Vector2 oldDirection = (Vector2)(old.b - old.a);
                float oldLength = oldDirection.magnitude;
                if (oldLength <= Small) continue;
                oldDirection /= oldLength;
                if (Mathf.Abs(Vector2.Dot(direction, oldDirection)) < alignment) continue;
                Vector2 relative = a - old.a;
                float lo = 0f, hi = length;
                if (!Clip(Vector2.Dot(relative, oldDirection), Vector2.Dot(direction, oldDirection),
                    0f, oldLength, ref lo, ref hi)) continue;
                if (!Clip(Cross(relative, oldDirection), Cross(direction, oldDirection),
                    -mergeDistance, mergeDistance, ref lo, ref hi)) continue;
                if (hi - lo > Small) coverage.Add(new Span(lo, hi));
            }
        }
        // Preserve a small neighbourhood of every knot, even if its path was
        // already represented by an earlier pass through this corridor.
        if (knotKeepRadius > 0f)
        foreach (Vector3 anchor in anchors)
        {
            float centre = Vector2.Dot(anchor - a, direction);
            Vector3 projected = a + direction * centre;
            float distance = ((Vector2)(projected - anchor)).sqrMagnitude;
            float radiusSquared = knotKeepRadius * knotKeepRadius;
            if (distance > radiusSquared) continue;
            float half = Mathf.Sqrt(radiusSquared - distance);
            SubtractCoverage(centre - half, centre + half);
        }
        coverage.Sort((x, y) => x.start.CompareTo(y.start));
        float cursor = 0f;
        foreach (Span covered in coverage)
        {
            if (covered.end <= cursor + Small) continue;
            if (covered.start > cursor + Small)
                Visible(a + direction * cursor, a + direction * covered.start);
            Flush(); // A hidden overlap must never become a connecting chord.
            cursor = Mathf.Max(cursor, covered.end);
        }
        if (cursor < length - Small) Visible(a + direction * cursor, b);
    }

    private void SubtractCoverage(float start, float end)
    {
        for (int i = coverage.Count - 1; i >= 0; i--)
        {
            Span span = coverage[i];
            if (end <= span.start || start >= span.end) continue;
            coverage.RemoveAt(i);
            if (start > span.start) coverage.Add(new Span(span.start, start));
            if (end < span.end) coverage.Add(new Span(end, span.end));
        }
    }

    private void Visible(Vector3 a, Vector3 b)
    {
        if ((b - a).sqrMagnitude < Small * Small) return;
        if (stroke.Count > 0 && (stroke[stroke.Count - 1] - a).sqrMagnitude > Small * Small) Flush();
        if (stroke.Count == 0) stroke.Add(a);
        stroke.Add(b);
        int index = canonical.Count;
        canonical.Add(new Segment(a, b));
        Gather(a, b, true, index);
    }

    private void Gather(Vector3 a, Vector3 b, bool add, int index)
    {
        if (!add) nearby.Clear();
        visited.Clear();
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / (cellSize * 0.5f)));
        for (int i = 0; i <= steps; i++)
        {
            Vector3 p = Vector3.Lerp(a, b, (float)i / steps);
            Vector2Int cell = new Vector2Int(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.y / cellSize));
            if (!visited.Add(cell)) continue;
            if (add)
            {
                if (!grid.TryGetValue(cell, out List<int> bucket))
                {
                    bucket = bucketPool.Count > 0 ? bucketPool.Pop() : new List<int>();
                    grid.Add(cell, bucket);
                }
                bucket.Add(index);
            }
            else
            {
                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    if (grid.TryGetValue(cell + new Vector2Int(x, y), out List<int> bucket))
                        foreach (int candidate in bucket) nearby.Add(candidate);
            }
        }
    }

    private void Flush()
    {
        if (stroke.Count < 2) { stroke.Clear(); return; }
        LineRenderer renderer;
        if (strokeCount == 0) renderer = primary;
        else
        {
            int childIndex = strokeCount - 1;
            if (childIndex >= renderers.Count)
            {
                GameObject go = new GameObject("Thread visible section " + strokeCount);
                go.transform.SetParent(transform, false);
                go.layer = primary.gameObject.layer;
                renderers.Add(go.AddComponent<LineRenderer>());
            }
            renderer = renderers[childIndex];
            CopyStyle(primary, renderer);
            renderer.enabled = true;
        }
        if (buffer.Length < stroke.Count) buffer = new Vector3[Mathf.NextPowerOfTwo(stroke.Count)];
        stroke.CopyTo(buffer);
        renderer.positionCount = stroke.Count;
        renderer.SetPositions(buffer);
        strokeCount++;
        stroke.Clear();
    }

    private static void CopyStyle(LineRenderer source, LineRenderer target)
    {
        target.useWorldSpace = true;
        target.loop = false;
        target.alignment = source.alignment;
        target.sharedMaterials = source.sharedMaterials;
        target.widthCurve = source.widthCurve;
        target.widthMultiplier = source.widthMultiplier;
        target.colorGradient = source.colorGradient;
        target.numCapVertices = source.numCapVertices;
        target.numCornerVertices = source.numCornerVertices;
        target.textureMode = source.textureMode;
        target.textureScale = source.textureScale;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
        target.shadowCastingMode = source.shadowCastingMode;
        target.receiveShadows = source.receiveShadows;
        target.generateLightingData = source.generateLightingData;
        target.maskInteraction = source.maskInteraction;
        target.shadowBias = source.shadowBias;
    }

    private static bool Clip(float origin, float rate, float min, float max, ref float lo, ref float hi)
    {
        if (Mathf.Abs(rate) <= Small) return origin >= min && origin <= max;
        float a = (min - origin) / rate, b = (max - origin) / rate;
        lo = Mathf.Max(lo, Mathf.Min(a, b));
        hi = Mathf.Min(hi, Mathf.Max(a, b));
        return hi >= lo;
    }
    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    private static float DistanceSquared(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        if (ab.sqrMagnitude < Small * Small) return (p - a).sqrMagnitude;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).sqrMagnitude;
    }

    private void OnDisable() { Clear(); }
}
