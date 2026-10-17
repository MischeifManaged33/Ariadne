using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(LineRenderer))]
public class AriadneThread : MonoBehaviour
{
    [Header("References")]
    [Tooltip(
        "Ground contact point on the player. " +
        "Defaults to this Transform."
    )]
    [SerializeField] private Transform followPoint;

    [Tooltip(
        "The player's health component. Must implement IDamagable."
    )]
    [SerializeField] private MonoBehaviour healthSource;

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
        "Turn off when your existing interaction script " +
        "calls TryStartWinding instead."
    )]
    [SerializeField] private bool listenForE = true;

    [Tooltip(
        "Large position jumps break the trail instead of " +
        "drawing a line across a teleport. This does not refill it."
    )]
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

    public bool IsOutOfThread =>
        RemainingThread <= Epsilon;

    public bool TrailDetached => detached;

    public event Action<float, float> ThreadAmountChanged;

    private const float Epsilon = 0.00001f;

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
    private IDamagable health;

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
        if (healthSource != null)
        {
            health = healthSource as IDamagable;

            if (health == null)
            {
                Debug.LogError(
                    "AriadneThread Health Source must implement " +
                    "IDamagable. Assign the player's actual " +
                    "health component.",
                    this
                );
            }

            return;
        }

        foreach (MonoBehaviour component in
                 followPoint.GetComponentsInParent<MonoBehaviour>(true))
        {
            if (component is IDamagable candidate)
            {
                healthSource = component;
                health = candidate;
                return;
            }
        }

        Debug.LogWarning(
            "Thread visuals will work, but healing needs a " +
            "player health component implementing IDamagable. " +
            "Assign Health Source.",
            this
        );
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
            // Avoid drawing a line across a teleport.
            // Teleports do not grant free thread or health.
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

        if (listenForE &&
            Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryStartWinding();
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

    private void LayThread(Vector3 position)
    {
        if (points.Count == 0)
        {
            points.Add(position);
            pointTimes.Add(Time.time);
            return;
        }

        Vector3 last = points[points.Count - 1];

        // Count ground movement, not visual depth.
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

        Vector3 endpoint = Vector3.Lerp(
            last,
            position,
            length / distance
        );

        points.Add(endpoint);
        pointTimes.Add(Time.time);

        usedThread = Mathf.Min(
            threadLength,
            usedThread + length
        );

        if (IsOutOfThread)
            detached = true;

        NotifyAmountChanged();
    }

    // Call from your existing interaction system if desired.
    // One press starts winding; holding E is not required.
    public bool TryStartWinding()
    {
        if (IsWinding || usedThread <= Epsilon)
            return false;

        IsWinding = true;
        return true;
    }

    private void WindThread(float budget)
    {
        float recovered = 0f;

        // Recall from the oldest end toward the newest end.
        while (budget > Epsilon && points.Count > 1)
        {
            float segment = Vector2.Distance(
                points[0],
                points[1]
            );

            if (segment <= Epsilon)
            {
                points.RemoveAt(0);
                pointTimes.RemoveAt(0);
                continue;
            }

            float amount = Mathf.Min(budget, segment);

            if (amount >= segment)
            {
                points.RemoveAt(0);
                pointTimes.RemoveAt(0);
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

        usedThread = Mathf.Max(
            0f,
            usedThread - recovered
        );

        if (recovered > 0f)
        {
            if (healthSource != null &&
                health != null &&
                health.IsAlive)
            {
                health.Heal(
                    recovered * healthPerUnitRecovered
                );
            }

            NotifyAmountChanged();
        }

        if (points.Count <= 1 || usedThread <= Epsilon)
        {
            points.Clear();
            pointTimes.Clear();

            usedThread = 0f;
            detached = false;
            IsWinding = false;

            NotifyAmountChanged();

            // Next frame begins a new trail at the player's feet.
        }
    }

    // Use when starting a new dungeon/run.
    // This refills thread without restoring health.
    public void ResetSpool()
    {
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

        // Recently laid points flutter slightly, then settle.
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 point = points[i];

            if (i > 0 && i < points.Count - 1)
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

        // Round sharp corners with small quadratic curves.
        for (int i = 1; i < relaxedPoints.Count - 1; i++)
        {
            Vector3 previous = relaxedPoints[i - 1];
            Vector3 point = relaxedPoints[i];
            Vector3 next = relaxedPoints[i + 1];

            float before =
                Vector3.Distance(previous, point);

            float after =
                Vector3.Distance(point, next);

            if (cornerRounding <= 0f ||
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

                Vector3 roundedPoint =
                    (1f - t) * (1f - t) * entry +
                    2f * (1f - t) * t * point +
                    t * t * exit;

                renderPoints.Add(roundedPoint);
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

    private void OnValidate()
    {
        threadLength = Mathf.Max(0.1f, threadLength);
        pointSpacing = Mathf.Max(0.01f, pointSpacing);
        windingSpeed = Mathf.Max(0.01f, windingSpeed);
        healthPerUnitRecovered =
            Mathf.Max(0f, healthPerUnitRecovered);
    }
}