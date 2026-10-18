using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(LineRenderer))]
public class AriadneThread : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform followPoint;

    [Tooltip("Player health component implementing IDamagable.")]
    private PlayerHealth health;
    [SerializeField]
    private MonoBehaviour healthSource;

    [Header("Spool")]
    [SerializeField, Min(0.1f)]
    private float threadLength = 40f;

    [SerializeField, Min(0.01f)]
    private float pointSpacing = 0.12f;

    [SerializeField, Min(0.01f)]
    private float windingSpeed = 8f;

    [SerializeField, Min(0f)]
    private float healthPerUnitRecovered = 0.5f;

    [Tooltip(
        "Disable when your existing interaction manager " +
        "calls Interact() instead."
    )]
    [SerializeField] private bool listenForE = true;

    [SerializeField, Min(0.1f)]
    private float teleportBreakDistance = 5f;

    [Header("Appearance")]
    [SerializeField] private Material threadMaterial;

    [SerializeField]
    private Color threadColor =
        new Color(1f, 0.15f, 0.15f, 1f);

    [SerializeField, Min(0.001f)]
    private float width = 0.045f;

    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 5;
    [SerializeField] private float visualZOffset = 0f;

    [SerializeField, Min(0f)]
    private float cornerRounding = 0.08f;

    [SerializeField, Range(1, 8)]
    private int cornerSamples = 3;

    [SerializeField, Min(0f)]
    private float flutterAmount = 0.02f;

    [SerializeField, Min(0.01f)]
    private float settlingTime = 0.5f;

    [SerializeField, Min(0f)]
    private float flutterSpeed = 8f;

    public float UsedThread => usedThread;

    public float RemainingThread =>
        Mathf.Max(0f, threadLength - usedThread);

    public float RemainingFraction =>
        threadLength > 0f
            ? RemainingThread / threadLength
            : 0f;

    public bool IsWinding { get; private set; }
    public bool IsOutOfThread => RemainingThread <= Epsilon;
    public bool TrailDetached => detached;
    public int ConnectionCount => knots.Count;

    public event Action<float, float> ThreadAmountChanged;

    private class ThreadKnot
    {
        public ThreadConnectionPoint point;
        public int vertexIndex;
        public float distanceFromStart;
    }

    private const float Epsilon = 0.00001f;

    private readonly List<ThreadKnot> knots =
        new List<ThreadKnot>();

    private readonly List<Vector3> points =
        new List<Vector3>();

    private readonly List<float> pointTimes =
        new List<float>();

    private readonly List<Vector3> relaxedPoints =
        new List<Vector3>();

    private readonly List<Vector3> renderPoints =
        new List<Vector3>();

    private Vector3[] renderBuffer = new Vector3[0];

    private LineRenderer line;

    private float usedThread;
    private bool detached;
    private bool hasLastFollowPosition;
    private Vector3 lastFollowPosition;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();

        if (followPoint == null)
            followPoint = transform;

        ResolveHealth();
        ConfigureRenderer();

        line.positionCount = 0;
    }

    private void ResolveHealth()
    {
        health = healthSource as PlayerHealth;

        if (health == null && followPoint != null)
            health = followPoint.GetComponentInParent<PlayerHealth>();

        if (health == null)
            health = PlayerHealth.Current;

        if (health != null)
        {
            healthSource = health;
        }
        else
        {
            Debug.LogWarning(
                "AriadneThread: assign the player's PlayerHealth " +
                "component to Health Source.",
                this
            );
        }
    }

    private void ConfigureRenderer()
    {
        line.useWorldSpace = true;
        line.loop = false;
        line.alignment = LineAlignment.View;

        line.widthCurve =
            AnimationCurve.Linear(0f, 1f, 1f, 1f);

        line.widthMultiplier = width;
        line.startColor = threadColor;
        line.endColor = threadColor;

        line.numCapVertices = 3;
        line.numCornerVertices = 2;

        line.sortingLayerName = sortingLayerName;
        line.sortingOrder = sortingOrder;

        line.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;

        line.receiveShadows = false;

        if (threadMaterial != null)
            line.sharedMaterial = threadMaterial;
    }

    private Vector3 FollowPosition()
    {
        Vector3 position = followPoint.position;
        position.z += visualZOffset;
        return position;
    }

    private void Update()
    {
        if (followPoint == null)
            return;

        Vector3 position = FollowPosition();

        if (hasLastFollowPosition &&
            Vector2.Distance(lastFollowPosition, position)
                > teleportBreakDistance)
        {
            if (usedThread > Epsilon)
            {
                detached = true;
            }
            else
            {
                points.Clear();
                pointTimes.Clear();
            }
        }

        lastFollowPosition = position;
        hasLastFollowPosition = true;

        if (listenForE)
        {
            var keyboard = Keyboard.current;

            if (keyboard != null &&
                keyboard.eKey.wasPressedThisFrame)
            {
                Interact();
            }

            // Stop immediately when E is no longer held.
            if (keyboard == null || !keyboard.eKey.isPressed)
            {
                StopWinding();
            }
        }

        if (Time.deltaTime > 0f)
        {
            if (IsWinding)
            {
                WindThread(windingSpeed * Time.deltaTime);
            }
            else if (!detached && !IsOutOfThread)
            {
                LayThread(position);
            }
        }

        RenderThread();
    }

    public void StopWinding()
    {
        IsWinding = false;
    }

    private void LayThread(Vector3 position)
    {
        if (points.Count == 0)
        {
            points.Add(position);
            pointTimes.Add(Time.time);
            return;
        }

        Vector3 last = points[points.Count - 1];
        position.z = last.z;

        float distance = Vector2.Distance(last, position);

        if (distance < pointSpacing)
            return;

        float length = Mathf.Min(
            distance,
            RemainingThread
        );

        if (length <= Epsilon)
            return;

        points.Add(Vector3.Lerp(
            last,
            position,
            length / distance
        ));

        pointTimes.Add(Time.time);

        usedThread = Mathf.Min(
            threadLength,
            usedThread + length
        );

        if (IsOutOfThread)
            detached = true;

        NotifyAmountChanged();
    }

    // Call this from your existing interaction manager if needed.
    public void Interact()
    {
        if (IsWinding)
            return;

        ThreadConnectionPoint point =
            ThreadConnectionPoint.FindNearestAvailable(
                FollowPosition()
            );

        if (point != null)
        {
            TryTieAt(point);
            return;
        }

        TryStartWinding();
    }

    public bool TryTieAt(ThreadConnectionPoint connection)
    {
        if (connection == null ||
            IsWinding ||
            connection.IsTied ||
            !connection.IsInRange(FollowPosition()))
        {
            return false;
        }

        if (detached)
        {
            Debug.Log(
                "Recover the detached/empty trail " +
                "before tying a new connection.",
                this
            );

            return false;
        }

        if (points.Count == 0)
        {
            points.Add(FollowPosition());
            pointTimes.Add(Time.time);
        }

        Vector3 last = points[points.Count - 1];

        Vector3 target = connection.Position;
        target.z = last.z;

        float length = Vector2.Distance(last, target);

        if (length > RemainingThread + Epsilon)
        {
            Debug.Log(
                "Not enough thread to reach this connection point. " +
                "Wind some thread back first.",
                this
            );

            return false;
        }

        if (!connection.TryAttach(this))
            return false;

        if (length > Epsilon)
        {
            points.Add(target);
            pointTimes.Add(Time.time);

            usedThread = Mathf.Min(
                threadLength,
                usedThread + length
            );
        }

        knots.Add(new ThreadKnot
        {
            point = connection,
            vertexIndex = points.Count - 1,
            distanceFromStart = usedThread
        });

        if (IsOutOfThread)
            detached = true;

        NotifyAmountChanged();

        return true;
    }

    public bool TryStartWinding()
    {
        if (IsWinding || usedThread <= Epsilon)
            return false;

        IsWinding = true;
        return true;
    }

    public void ForgetConnection(ThreadConnectionPoint point)
    {
        for (int i = knots.Count - 1; i >= 0; i--)
        {
            if (knots[i].point == point)
                knots.RemoveAt(i);
        }
    }

    private bool IsKnotVertex(int index)
    {
        foreach (ThreadKnot knot in knots)
        {
            if (knot.point != null &&
                knot.point.Owner == this &&
                knot.vertexIndex == index)
            {
                return true;
            }
        }

        return false;
    }

    private void ReleaseRewoundConnections()
    {
        for (int i = knots.Count - 1; i >= 0; i--)
        {
            ThreadKnot knot = knots[i];

            // Arriving at a knot keeps it connected.
            // Moving past its recorded distance releases it.
            if (knot.point != null &&
                usedThread >= knot.distanceFromStart - Epsilon)
            {
                continue;
            }

            knots.RemoveAt(i);

            if (knot.point != null)
                knot.point.Detach(this);
        }
    }

    private void BreakAllConnections()
    {
        while (knots.Count > 0)
        {
            ThreadKnot knot = knots[knots.Count - 1];
            knots.RemoveAt(knots.Count - 1);

            if (knot.point != null)
                knot.point.Detach(this);
        }
    }

    private void WindThread(float budget)
    {
        float recovered = 0f;
        int removedVertices = 0;

        // Recover the oldest end first, moving toward the player.
        while (budget > Epsilon && points.Count > 1)
        {
            float segment = Vector2.Distance(points[0], points[1]);

            if (segment <= Epsilon)
            {
                points.RemoveAt(0);
                pointTimes.RemoveAt(0);
                removedVertices++;
                continue;
            }

            float amount = Mathf.Min(budget, segment);

            if (amount >= segment)
            {
                points.RemoveAt(0);
                pointTimes.RemoveAt(0);
                removedVertices++;
            }
            else
            {
                points[0] = Vector3.Lerp(
                    points[0],
                    points[1],
                    amount / segment
                );
            }

            recovered += amount;
            budget -= amount;
        }

        recovered = Mathf.Min(recovered, usedThread);
        usedThread = Mathf.Max(0f, usedThread - recovered);

        ReleaseRewoundConnections(recovered, removedVertices);

        if (recovered > 0f)
        {
            if (health == null)
                ResolveHealth();

            if (health != null && health.CurrentHealth > 0f)
                health.Heal(recovered * healthPerUnitRecovered);

            NotifyAmountChanged();
        }

        if (points.Count <= 1 || usedThread <= Epsilon)
        {
            BreakAllConnections();

            points.Clear();
            pointTimes.Clear();

            usedThread = 0f;
            detached = false;
            IsWinding = false;

            NotifyAmountChanged();
        }
    }

    private void ReleaseRewoundConnections(
        float recovered,
        int removedVertices)
    {
        for (int i = knots.Count - 1; i >= 0; i--)
        {
            ThreadKnot knot = knots[i];

            // Update the knot relative to the remaining thread.
            knot.distanceFromStart -= recovered;
            knot.vertexIndex = Mathf.Max(
                0,
                knot.vertexIndex - removedVertices
            );

            // Keep the connection until winding passes its location.
            if (knot.point != null &&
                knot.distanceFromStart >= -Epsilon)
            {
                continue;
            }

            knots.RemoveAt(i);

            if (knot.point != null)
            {
                knot.point.Detach(this);
            }
        }
    }

    public void ResetSpool()
    {
        BreakAllConnections();

        points.Clear();
        pointTimes.Clear();

        usedThread = 0f;
        detached = false;
        IsWinding = false;
        hasLastFollowPosition = false;

        if (line != null)
            line.positionCount = 0;

        NotifyAmountChanged();
    }

    private void NotifyAmountChanged()
    {
        ThreadAmountChanged?.Invoke(
            RemainingThread,
            threadLength
        );
    }

    private void RenderThread()
    {
        if (points.Count < 2)
        {
            line.positionCount = 0;
            return;
        }

        relaxedPoints.Clear();

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 point = points[i];

            if (i > 0 &&
                i < points.Count - 1 &&
                !IsKnotVertex(i))
            {
                Vector2 tangent =
                    points[i + 1] - points[i - 1];

                if (tangent.sqrMagnitude > Epsilon)
                {
                    tangent.Normalize();

                    Vector3 sideways = new Vector3(
                        -tangent.y,
                        tangent.x,
                        0f
                    );

                    float age = Mathf.Max(
                        0f,
                        Time.time - pointTimes[i]
                    );

                    float motion =
                        Mathf.Sin(
                            Time.time * flutterSpeed +
                            pointTimes[i] * 11f
                        )
                        * flutterAmount
                        * Mathf.Exp(
                            -age / Mathf.Max(0.01f, settlingTime)
                        );

                    point += sideways * motion;
                }
            }

            relaxedPoints.Add(point);
        }

        renderPoints.Clear();
        renderPoints.Add(relaxedPoints[0]);

        for (int i = 1; i < relaxedPoints.Count - 1; i++)
        {
            Vector3 previous = relaxedPoints[i - 1];
            Vector3 point = relaxedPoints[i];
            Vector3 next = relaxedPoints[i + 1];

            float before =
                Vector3.Distance(previous, point);

            float after =
                Vector3.Distance(point, next);

            // A tied vertex must stay exactly on its connection.
            if (IsKnotVertex(i) ||
                cornerRounding <= 0f ||
                before <= Epsilon ||
                after <= Epsilon)
            {
                renderPoints.Add(point);
                continue;
            }

            Vector3 entry = Vector3.Lerp(
                point,
                previous,
                Mathf.Min(0.45f, cornerRounding / before)
            );

            Vector3 exit = Vector3.Lerp(
                point,
                next,
                Mathf.Min(0.45f, cornerRounding / after)
            );

            renderPoints.Add(entry);

            int samples = Mathf.Max(1, cornerSamples);

            for (int sample = 1; sample <= samples; sample++)
            {
                float t = (float)sample / samples;

                renderPoints.Add(
                    (1f - t) * (1f - t) * entry +
                    2f * (1f - t) * t * point +
                    t * t * exit
                );
            }
        }

        renderPoints.Add(
            relaxedPoints[relaxedPoints.Count - 1]
        );

        if (renderBuffer.Length < renderPoints.Count)
        {
            renderBuffer = new Vector3[
                Mathf.NextPowerOfTwo(renderPoints.Count)
            ];
        }

        renderPoints.CopyTo(renderBuffer);

        line.positionCount = renderPoints.Count;
        line.SetPositions(renderBuffer);
    }

    private void OnDestroy()
    {
        BreakAllConnections();
    }

    private void OnValidate()
    {
        threadLength = Mathf.Max(0.1f, threadLength);
        pointSpacing = Mathf.Max(0.01f, pointSpacing);
        windingSpeed = Mathf.Max(0.01f, windingSpeed);

        healthPerUnitRecovered =
            Mathf.Max(0f, healthPerUnitRecovered);
    }
}