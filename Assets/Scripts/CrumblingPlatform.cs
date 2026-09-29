using System.Collections;
using UnityEngine;

// При контакте с героиней платформа ненадолго исчезает и затем возвращается.
// Коллайдер НЕ триггерный: на нём можно стоять до обрушения.
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class CrumblingPlatform : MonoBehaviour
{
    [Tooltip("Время после касания до обрушения, секунды")]
    public float collapseDelay = 0.35f;

    [Tooltip("Время до восстановления платформы, секунды")]
    public float respawnDelay = 3f;

    SpriteRenderer visual;
    BoxCollider2D platformCollider;
    bool collapsing;

    void Awake()
    {
        visual = GetComponent<SpriteRenderer>();
        platformCollider = GetComponent<BoxCollider2D>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Не реагируем на декор и другие платформы, только на игрока.
        if (!collapsing && collision.collider.GetComponent<PlayerController>() != null)
            StartCoroutine(Collapse());
    }

    IEnumerator Collapse()
    {
        collapsing = true;
        yield return new WaitForSeconds(Mathf.Max(0f, collapseDelay));
        platformCollider.enabled = false;
        visual.enabled = false;

        yield return new WaitForSeconds(Mathf.Max(0f, respawnDelay));
        visual.enabled = true;
        platformCollider.enabled = true;
        collapsing = false;
    }
}
