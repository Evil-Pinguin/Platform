using UnityEngine;

// Платформер: ходьба с разгоном, прыжок, анимация шага и респавн при падении.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [Header("Движение")]
    [Tooltip("Скорость ходьбы, единиц в секунду")]
    public float moveSpeed = 5f;
    [Tooltip("Насколько быстро она разгоняется и тормозит")]
    public float acceleration = 70f;

    [Header("Прыжок")]
    [Tooltip("На какую высоту прыгает от пола, в юнитах")]
    public float jumpHeight = 2.2f;
    [Tooltip("Сколько можно удерживать кнопку, чтобы прыгнуть ниже")]
    public float jumpHoldTime = 0.16f;
    [Tooltip("Сколько ещё можно нажать прыжок после края — coyote time")]
    public float coyoteTime = 0.1f;
    [Tooltip("Сколько нажатие прыжка ждёт приземления — буфер ввода")]
    public float jumpBuffer = 0.12f;

    [Header("Анимация")]
    [Tooltip("Кадры ходьбы по порядку (нарисованы лицом вправо)")]
    public Sprite[] walkFrames;
    [Tooltip("Сколько кадров ходьбы показывать в секунду")]
    public float walkFps = 12f;
    [Tooltip("Спрайт, когда героиня стоит на месте")]
    public Sprite idleSprite;

    [Header("Если упала с края")]
    [Tooltip("Ниже этой высоты героиня возвращается в точку старта")]
    public float respawnBelowY = -12f;

    Rigidbody2D body;
    SpriteRenderer spriteRenderer;
    Vector2 startPosition;
    float face = 1f;
    float animTimer;
    int shownFrame = -1;
    bool grounded;
    float lastGroundedTime = -99f;
    float jumpPressedAt = -99f;
    float jumpStartedAt = -99f;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        startPosition = body.position;
    }

    void Update()
    {
        float move = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(move) > 0.01f)
            face = move > 0f ? 1f : -1f;

        UpdateGrounded();
        ReadJumpInput();

        float vx = body.velocity.x;
        body.velocity = new Vector2(
            Mathf.MoveTowards(vx, move * moveSpeed, acceleration * Time.deltaTime),
            body.velocity.y);

        Animate(move);
        RespawnIfFallen();
    }

    // Луч вниз из под центра: стоим ли мы на чём-то сейчас
    void UpdateGrounded()
    {
        Vector2 from = (Vector2)transform.position + Vector2.up * 0.15f;
        bool nowGrounded = Physics2D.Raycast(from, Vector2.down, 0.3f);
        if (nowGrounded && !grounded)
            lastGroundedTime = Time.time;
        grounded = nowGrounded;
    }

    void ReadJumpInput()
    {
        bool pressed = Input.GetKeyDown(KeyCode.Space)
                    || Input.GetKeyDown(KeyCode.W)
                    || Input.GetKeyDown(KeyCode.UpArrow);
        if (pressed)
            jumpPressedAt = Time.time;

        bool held = Input.GetKey(KeyCode.Space)
                 || Input.GetKey(KeyCode.W)
                 || Input.GetKey(KeyCode.UpArrow);

        // Есть свежее нажатие, мы на земле (или только что были) и не летим вверх
        bool wantsJump = Time.time - jumpPressedAt <= jumpBuffer;
        bool canJump = grounded || Time.time - lastGroundedTime <= coyoteTime;
        if (wantsJump && canJump && body.velocity.y <= 0.01f)
        {
            float g = Mathf.Abs(Physics2D.gravity.y) * body.gravityScale;
            body.velocity = new Vector2(body.velocity.x, Mathf.Sqrt(2f * g * jumpHeight));
            jumpPressedAt = -99f;
            jumpStartedAt = Time.time;
            lastGroundedTime = -99f;   // coyote сгорел, второй раз не прыгнуть
        }

        // Отпустили кнопку в начале подъёма — срезаем высоту
        if (!held && Time.time - jumpStartedAt < jumpHoldTime && body.velocity.y > 0f)
        {
            body.velocity = new Vector2(body.velocity.x, body.velocity.y * 0.45f);
            jumpStartedAt = -99f;
        }
    }

    void Animate(float move)
    {
        // Шагаем, только если реально идём по земле
        bool walking = grounded
                    && Mathf.Abs(move) > 0.01f
                    && Mathf.Abs(body.velocity.x) > 0.05f
                    && walkFrames != null && walkFrames.Length > 0;

        if (walking)
        {
            animTimer += Time.deltaTime;
            int frame = (int)(animTimer * walkFps) % walkFrames.Length;
            if (frame != shownFrame)
            {
                shownFrame = frame;
                spriteRenderer.sprite = walkFrames[frame];
            }
        }
        else
        {
            animTimer = 0f;
            shownFrame = -1;
            // В воздухе держим последний кадр, на земле — позу покоя
            if (grounded && idleSprite != null)
                spriteRenderer.sprite = idleSprite;
        }

        spriteRenderer.flipX = face < 0f;
    }

    void RespawnIfFallen()
    {
        if (body.position.y >= respawnBelowY)
            return;

        body.position = startPosition;
        body.velocity = Vector2.zero;
        animTimer = 0f;
        shownFrame = -1;
    }
}
