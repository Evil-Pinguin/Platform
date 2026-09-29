using UnityEngine;

// Кинематическая платформа: плавно движется между стартом и start + offset.
// MovePosition в FixedUpdate двигает и коллайдер, а не только картинку.
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class MovingPlatform : MonoBehaviour
{
    [Tooltip("Смещение второй точки от стартовой позиции в юнитах")]
    public Vector2 offset = new Vector2(0f, 0.9f);

    [Tooltip("Время движения от одной точки до другой, секунды")]
    public float travelTime = 1.6f;

    Rigidbody2D body;
    Vector2 start;
    float elapsed;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        start = body.position;
    }

    void FixedUpdate()
    {
        elapsed += Time.fixedDeltaTime;
        float phase = Mathf.PingPong(elapsed / Mathf.Max(0.01f, travelTime), 1f);
        // Смягчаем разворот на концах, чтобы героиню не дёргало.
        float t = Mathf.SmoothStep(0f, 1f, phase);
        body.MovePosition(Vector2.Lerp(start, start + offset, t));
    }
}
