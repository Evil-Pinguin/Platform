using System.Collections;
using UnityEngine;

// То, что можно ударить. Пока простейшая мишень: считает урон, краснеет
// от попадания и исчезает, когда здоровье кончилось. Кинетического
// отброса нет — ��тобы он появился, нужен Rigidbody2D, и тогда отброс
// применяется к нему же.
public class Damageable : MonoBehaviour
{
    [Tooltip("Сколько ударов выдерживает")]
    public int health = 5;

    [Tooltip("Насколько отлетает от удара, если есть Rigidbody2D")]
    public float knockback = 2.5f;

    [Tooltip("Длительность красной вспышки, секунды")]
    public float flashTime = 0.15f;

    [Tooltip("Скрыть объект, когда здоровье кончилось")]
    public bool vanishOnDeath = true;

    SpriteRenderer rend;
    Rigidbody2D body;
    Color baseColor;
    Coroutine flashing;

    void Awake()
    {
        rend = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        if (rend != null)
            baseColor = rend.color;
    }

    public bool IsAlive
    {
        get { return health > 0; }
    }

    public void TakeHit(int amount, Vector2 from)
    {
        if (health <= 0)
            return;

        // Прикрытая героиня удар не принимает. Проверка здесь, а не в
        // контроллере, чтобы любой, кто бьёт, об этом не забывал.
        PlayerController guard = GetComponent<PlayerController>();
        if (guard == null)
            guard = GetComponentInParent<PlayerController>();
        if (guard != null && guard.IsGuarding)
            return;

        health -= amount;

        if (rend != null)
        {
            rend.color = new Color(1f, 0.4f, 0.4f, baseColor.a);
            if (flashing != null)
                StopCoroutine(flashing);
            flashing = StartCoroutine(RecoverColor());
        }

        if (body != null)
        {
            Vector2 dir = ((Vector2)transform.position - from).normalized;
            if (dir == Vector2.zero)
                dir = Vector2.right;
            body.velocity = dir * knockback;
        }

        if (health <= 0 && vanishOnDeath)
            Destroy(gameObject);
    }

    IEnumerator RecoverColor()
    {
        yield return new WaitForSeconds(flashTime);
        if (rend != null)
            rend.color = baseColor;
        flashing = null;
    }
}
