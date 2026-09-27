using UnityEngine;

// Управление героиней: ходьба влево/вправо (стрелки или A/D) и анимация шага.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [Header("Движение")]
    [Tooltip("Скорость ходьбы, единиц в секунду")]
    public float moveSpeed = 2.5f;

    [Header("Анимация")]
    [Tooltip("Кадры ходьбы по порядку (нарисованы лицом вправо)")]
    public Sprite[] walkFrames;
    [Tooltip("Сколько кадров ходьбы показывать в секунду")]
    public float walkFps = 12f;
    [Tooltip("Спрайт, когда героиня стоит на месте")]
    public Sprite idleSprite;
    [Tooltip("Спрайт, когда героиня стоит и смотрит прямо в камеру. Если пусто — берётся idleSprite")]
    public Sprite idleFrontSprite;

    [Header("Если упала с края")]
    [Tooltip("Ниже этой высоты героиня возвращается в точку старта")]
    public float respawnBelowY = -15f;

    Rigidbody2D body;
    SpriteRenderer spriteRenderer;
    float moveInput;
    float animTimer;
    Vector2 startPosition;

    // Спрайт для позы покоя: сперва пробуем фронтальный (смотрит в камеру).
    Sprite RestSprite => idleFrontSprite != null ? idleFrontSprite : idleSprite;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        startPosition = body.position;
    }

    void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        // Поворот в сторону движения: кадры нарисованы лицом вправо, влево просто отражаем.
        if (moveInput > 0.01f)
            spriteRenderer.flipX = false;
        else if (moveInput < -0.01f)
            spriteRenderer.flipX = true;

        // Шагаем, только если реально движемся (упёрлась в блок — стоит).
        bool isWalking = Mathf.Abs(moveInput) > 0.01f && Mathf.Abs(body.velocity.x) > 0.05f;

        if (isWalking && walkFrames != null && walkFrames.Length > 0)
        {
            animTimer += Time.deltaTime;
            int frame = (int)(animTimer * walkFps) % walkFrames.Length;
            spriteRenderer.sprite = walkFrames[frame];
        }
        else
        {
            animTimer = 0f;

            // Стоит — разворачиваемся лицом к камере (фронтальный спрайт не отражаем).
            Sprite rest = RestSprite;
            if (rest != null)
            {
                spriteRenderer.sprite = rest;
                if (idleFrontSprite != null)
                    spriteRenderer.flipX = false;
            }
        }

        if (body.position.y < respawnBelowY)
        {
            body.position = startPosition;
            body.velocity = Vector2.zero;
        }
    }

    void FixedUpdate()
    {
        body.velocity = new Vector2(moveInput * moveSpeed, body.velocity.y);
    }
}
