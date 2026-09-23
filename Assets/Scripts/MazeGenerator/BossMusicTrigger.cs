using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BossMusicTrigger : MonoBehaviour
{
    [SerializeField]
    private bool returnToMazeMusicOnExit;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        SoundManager.Instance?.PlayBossMusic();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!returnToMazeMusicOnExit)
            return;

        if (!other.CompareTag("Player"))
            return;

        SoundManager.Instance?.PlayMazeMusic();
    }
}