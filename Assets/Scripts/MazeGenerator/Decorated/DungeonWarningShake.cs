using UnityEngine;

public class DungeonWarningShake : MonoBehaviour
{
    [SerializeField, Min(0.01f)]
    private float duration = 0.6f;

    [SerializeField, Min(0f)]
    private float strength = 0.04f;

    [SerializeField, Min(0.1f)]
    private float frequency = 18f;

    private Vector3 restingPosition;
    private float remaining;
    private float noiseSeed;

    private void Awake()
    {
        restingPosition = transform.localPosition;
        noiseSeed = Random.Range(0f, 1000f);
    }

    public void PlayShake()
    {
        remaining = duration;
    }

    private void LateUpdate()
    {
        if (remaining <= 0f)
            return;

        remaining = Mathf.Max(0f, remaining - Time.deltaTime);

        float fade = remaining / duration;
        float time = Time.time * frequency;

        float x =
            Mathf.PerlinNoise(noiseSeed, time) * 2f - 1f;

        float y =
            Mathf.PerlinNoise(noiseSeed + 100f, time) * 2f - 1f;

        transform.localPosition =
            restingPosition +
            new Vector3(x, y, 0f) * strength * fade;
    }

    private void OnDisable()
    {
        remaining = 0f;
        transform.localPosition = restingPosition;
    }
}