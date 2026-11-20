using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class DialogueStop
{
    public Transform point;
    public DialogueData dialogue;
}

public class TutorialNPC : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField]
    private List<DialogueStop> stops = new();
    private DialogueUI dialogueUI;

    [Header("Trigger")]
    [SerializeField]
    private string playerTag = "Player";
    private float triggerRadius = 1.5f;

    [Header("While Talking")]
    [SerializeField]
    private bool lockPlayer = true;
    [SerializeField]
    private bool freezeWorld;

    [Header("Done")]
    private bool destroyWhenDone = true;
    [SerializeField]
    private UnityEvent onAllDialogueFinished = new();

    public bool IsTalking { get; private set; }

    public int CurrentStop => _index;

    private int _index;
    private bool _finished;
    private readonly List<Behaviour> _locked = new();
    private readonly List<Animator> _pausedAnimators = new();
    private readonly List<float> _animatorSpeeds = new();

    private void Awake() => EnsureTrigger();

    private void Start()
    {
        if (stops.Count == 0) {
            Finish();
            return;
        }

        MoveTo(_index);
    }

    private void OnDisable()
    {
        if (IsTalking)
            ReleasePlayer();
    }

    private void OnTriggerEnter2D(Collider2D other) => TryTalk(other);

    private void OnTriggerStay2D(Collider2D other) => TryTalk(other);

    private void TryTalk(Collider2D other)
    {
        if (IsTalking || _finished || _index >= stops.Count || !IsPlayer(other))
            return;

        var ui = dialogueUI != null ? dialogueUI : DialogueUI.Find();
        if (ui == null) {
            Debug.LogWarning($"{name}: no DialogueUI in the scene, add one to Canvas/DialogueBox.", this);
            return;
        }

        dialogueUI = ui;
        IsTalking = true;
        LockPlayer(other);

        ui.Play(stops[_index].dialogue, OnDialogueFinished);
    }

    private void OnDialogueFinished()
    {
        StartCoroutine(AdvanceNextFrame());
    }

    private IEnumerator AdvanceNextFrame()
    {
        yield return null;

        ReleasePlayer();
        IsTalking = false;
        _index++;

        if (_index >= stops.Count)
            Finish();
        else
            MoveTo(_index);
    }

    private void MoveTo(int index)
    {
        var point = stops[index].point;
        if (point != null)
            transform.position = point.position;
    }

    private void Finish()
    {
        _finished = true;
        onAllDialogueFinished?.Invoke();

        if (destroyWhenDone)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    private bool IsPlayer(Collider2D other)
    {
        var body = other.attachedRigidbody;
        if (body != null && body.GetComponent<Player>() != null)
            return true;

        if (other.GetComponentInParent<Player>() != null)
            return true;

        if (string.IsNullOrEmpty(playerTag))
            return false;

        return other.CompareTag(playerTag) || (body != null && body.CompareTag(playerTag));
    }

    private void EnsureTrigger()
    {
        foreach (var col in GetComponents<Collider2D>()) {
            if (col.isTrigger)
                return;
        }

        var trigger = gameObject.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = triggerRadius;
    }

    private void LockPlayer(Collider2D other)
    {
        if (freezeWorld)
            Time.timeScale = 0f;

        if (!lockPlayer)
            return;

        var player = other.GetComponentInParent<Player>();
        if (player == null)
            player = FindAnyObjectByType<Player>();
        if (player == null)
            return;

        Lock(player.GetComponentsInChildren<PlayerController>());
        Lock(player.GetComponentsInChildren<PlayerDash>());
        Lock(player.GetComponentsInChildren<PlayerWeapon>());
        Lock(player.GetComponentsInChildren<PlayerInteractor>());

        if (player.TryGetComponent<Rigidbody2D>(out var body))
            body.linearVelocity = Vector2.zero;

        PauseAnimators(player);
    }

    private void Lock(IEnumerable<Behaviour> behaviours)
    {
        foreach (var behaviour in behaviours) {
            if (!behaviour.enabled)
                continue;

            behaviour.enabled = false;
            _locked.Add(behaviour);
        }
    }

    private void ReleasePlayer()
    {
        if (freezeWorld)
            Time.timeScale = 1f;

        foreach (var behaviour in _locked) {
            if (behaviour != null)
                behaviour.enabled = true;
        }

        _locked.Clear();

        ResumeAnimators();
    }

    private void PauseAnimators(Player player)
    {
        foreach (var animator in player.GetComponentsInChildren<Animator>()) {
            if (animator.speed == 0f)
                continue;

            _pausedAnimators.Add(animator);
            _animatorSpeeds.Add(animator.speed);
            animator.speed = 0f;
        }
    }

    private void ResumeAnimators()
    {
        for (var i = 0; i < _pausedAnimators.Count; i++) {
            var animator = _pausedAnimators[i];

            if (animator != null && animator.speed == 0f)
                animator.speed = _animatorSpeeds[i];
        }

        _pausedAnimators.Clear();
        _animatorSpeeds.Clear();
    }

    private void OnValidate()
    {
        foreach (var stop in stops) {
            if (stop != null && stop.point != null && stop.point.IsChildOf(transform))
                Debug.LogWarning($"{name}: stop point '{stop.point.name}' is a child of the NPC", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        var previous = transform.position;

        foreach (var stop in stops) {
            if (stop == null || stop.point == null)
                continue;

            Gizmos.DrawLine(previous, stop.point.position);
            Gizmos.DrawWireSphere(stop.point.position, 0.3f);
            previous = stop.point.position;
        }

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}
