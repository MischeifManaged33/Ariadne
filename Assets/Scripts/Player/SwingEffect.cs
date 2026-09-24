using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SwingEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private PlayerWeapon weapon;
    [SerializeField]
    private SpriteRenderer playerRenderer;
    [SerializeField]
    private SpriteRenderer spriteRenderer;
    [SerializeField]
    private Animator animator;

    [Header("Placement")]
    [SerializeField, Min(0f)]
    private float distance = 0.45f;
    [SerializeField]
    private float height = 0.15f;
    [SerializeField, Range(0.1f, 1f)]
    private float isometricYScale = Isometric.DefaultYScale;
    [SerializeField]
    private bool rotateToSwing = true;
    [SerializeField]
    private bool flipWhenSwingingLeft = true;

    [Header("Sorting")]
    [SerializeField]
    private int frontOffset = 1;
    [SerializeField]
    private int behindOffset = -1;
    private float behindThreshold = 0.25f;

    [Header("Playback")]
    [SerializeField]
    private string stateName = "SwingEffect";
    [SerializeField, Min(0f)]
    private float duration;

    private int _stateHash;
    private float _remaining;

    private void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        weapon = GetComponentInParent<PlayerWeapon>();

        var player = GetComponentInParent<Player>();
        if (player != null)
            playerRenderer = player.GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (weapon == null)
            weapon = GetComponentInParent<PlayerWeapon>();

        _stateHash = Animator.StringToHash(stateName);

        spriteRenderer.enabled = false;
    }

    private void OnEnable()
    {
        if (weapon != null)
            weapon.Attacked += OnAttacked;
    }

    private void OnDisable()
    {
        if (weapon != null)
            weapon.Attacked -= OnAttacked;

        _remaining = 0f;
        spriteRenderer.enabled = false;
    }

    private void Update()
    {
        if (_remaining <= 0f)
            return;

        _remaining -= Time.deltaTime;

        if (_remaining <= 0f)
            spriteRenderer.enabled = false;
    }

    public void Play(Vector2 groundDirection)
    {
        if (groundDirection.sqrMagnitude <= 0.0001f)
            return;

        groundDirection = groundDirection.normalized;

        var screen = Isometric.ToScreen(groundDirection * distance, isometricYScale);

        transform.localPosition = new Vector3(screen.x, screen.y + height, 0f);

        if (rotateToSwing) {
            
            var degrees = Mathf.Atan2(screen.y, screen.x) * Mathf.Rad2Deg;
            transform.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        if (flipWhenSwingingLeft)
            spriteRenderer.flipY = rotateToSwing && screen.x < 0f;

        ApplySorting(groundDirection);

        spriteRenderer.enabled = true;

        if (animator == null) {
            _remaining = Mathf.Max(0.01f, duration);
            return;
        }

        animator.Play(_stateHash, 0, 0f);

        animator.Update(0f);

        _remaining = duration > 0f ? duration : animator.GetCurrentAnimatorStateInfo(0).length;
    }

   
    private void ApplySorting(Vector2 groundDirection)
    {
        if (playerRenderer == null)
            return;

        var behind = groundDirection.y > behindThreshold;

        spriteRenderer.sortingLayerID = playerRenderer.sortingLayerID;
        spriteRenderer.sortingOrder = playerRenderer.sortingOrder + (behind ? behindOffset : frontOffset);
    }

    private void OnAttacked(Vector2 groundDirection) => Play(groundDirection);
}
