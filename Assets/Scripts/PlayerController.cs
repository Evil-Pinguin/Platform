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

    [Header("Атака")]
    [Tooltip("Кадры удара по порядку (нарисованы лицом вправо)")]
    public Sprite[] attackFrames;
    [Tooltip("Сколько длится весь удар, секунды")]
    public float attackDuration = 0.32f;
    [Tooltip("Сколько секунд нельзя бить снова после удара")]
    public float attackCooldown = 0.1f;
    [Tooltip("Насколько удар сковывает бег, доля от moveSpeed")]
    [Range(0f, 1f)]
    public float attackMoveScale = 0.35f;
    [Tooltip("Сколько урона наносит один удар")]
    public int attackDamage = 1;
    [Tooltip("Центр хитбокса относительно героини по горизонтали, юниты")]
    public float hitboxCenterX = 0.85f;
    [Tooltip("Центр хитбокса по вертикали, юниты")]
    public float hitboxCenterY = 0.95f;
    [Tooltip("Размер хитбокса, юниты")]
    public Vector2 hitboxSize = new Vector2(1.3f, 1.2f);
    [Tooltip("С какой доли удара и до какой хитбокс живой, 0…1")]
    public Vector2 hitWindow = new Vector2(0.3f, 0.7f);

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
    float attackStartedAt = -99f;
    bool attacking;
    readonly System.Collections.Generic.HashSet<Damageable> hitThisSwing =
        new System.Collections.Generic.HashSet<Damageable>();

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
        ReadAttackInput();

        float speed = attacking ? moveSpeed * attackMoveScale : moveSpeed;
        float vx = body.velocity.x;
        body.velocity = new Vector2(
            Mathf.MoveTowards(vx, move * speed, acceleration * Time.deltaTime),
            body.velocity.y);

        Animate(move);
        UpdateAttackHit();
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

    // Удар: левая кнопка мыши, J или K. Повторно нельзя, пока не
    // отыграл удар и не прошёл откат.
    void ReadAttackInput()
    {
        if (attacking)
        {
            if (Time.time - attackStartedAt >= attackDuration + attackCooldown)
            {
                attacking = false;
                animTimer = 0f;
                shownFrame = -1;
            }
            return;
        }

        if (attackFrames == null || attackFrames.Length == 0)
            return;

        bool pressed = Input.GetMouseButtonDown(0)
                    || Input.GetKeyDown(KeyCode.J)
                    || Input.GetKeyDown(KeyCode.K);
        if (!pressed)
            return;

        attacking = true;
        attackStartedAt = Time.time;
        hitThisSwing.Clear();
        animTimer = 0f;
        shownFrame = -1;
    }

    // Хитбокс живёт только в окне удара. Проверяем оверлапом, а не
    // триггером: так не нужно заводить отдельный объект с коллайдером и
    // переключать его enabled. Проверка идёт каждый кадр окна, чтобы можно
    // было задеть цель, которая подошла уже в середине удара; за защиту от
    // повторного попадания отвечает hitThisSwing.
    void UpdateAttackHit()
    {
        if (!attacking)
            return;

        float t = (Time.time - attackStartedAt) / attackDuration;
        if (t < hitWindow.x || t > hitWindow.y)
            return;

        Vector2 centre = (Vector2)transform.position
                       + new Vector2(hitboxCenterX * face, hitboxCenterY);
        Collider2D[] hits = Physics2D.OverlapBoxAll(centre, hitboxSize, 0f);
        foreach (Collider2D h in hits)
        {
            Damageable target = h.GetComponent<Damageable>()
                             ?? h.GetComponentInParent<Damageable>();
            if (target == null || target == this || !target.IsAlive)
                continue;
            if (hitThisSwing.Add(target))
                target.TakeHit(attackDamage, transform.position);
        }
    }

    void Animate(float move)
    {
        // Удар важнее ходьбы: пока он играется, кадры шага не показываем
        if (attacking && attackFrames != null && attackFrames.Length > 0)
        {
            animTimer += Time.deltaTime;
            int f = (int)(animTimer / attackDuration * attackFrames.Length);
            if (f >= attackFrames.Length)
                f = attackFrames.Length - 1;
            if (f != shownFrame)
            {
                shownFrame = f;
                spriteRenderer.sprite = attackFrames[f];
            }
            spriteRenderer.flipX = face < 0f;
            return;
        }

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
