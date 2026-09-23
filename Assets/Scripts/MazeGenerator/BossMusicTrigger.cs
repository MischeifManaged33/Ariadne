using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BossMusicTrigger : MonoBehaviour
{
    [SerializeField] private GameObject bossHealthBar;

    private bool activated;

    private void Awake()
    {
        if (bossHealthBar != null)
            bossHealthBar.SetActive(false);
    }

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated || !other.CompareTag("Player"))
            return;

        activated = true;

        SoundManager.Instance?.PlayBossMusic();

        if (bossHealthBar != null)
            bossHealthBar.SetActive(true);
    }
}