using UnityEngine;

// Платформер: ходьба с разгоном, прыжок, удар ножом и пять способностей.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [Header("Движение")]
    [Tooltip("Скорость ходьбы, единиц в секунду")]
    public float moveSpeed = 5f;
    [Tooltip("Насколько быстро она разгоняется и тормозит")]
    public float acceleration = 70f;

    [Header("Спринт со стаминой")]
    [Tooltip("Во сколько раз спринт быстрее ходьбы")]
    public float sprintMultiplier = 1.7f;
    [Tooltip("Сколько держит полная стамина, секунды")]
    public float sprintDuration = 3.2f;
    [Tooltip("Сколько стоит полная стамина при отдыхе, секунды")]
    public float staminaRecoverTime = 6f;
    [Tooltip("Сколько разгон при спринте быстрее разгона при ходьбе")]
    public float sprintAccelerationBonus = 1.6f;
    [Tooltip("Доля стамины, до которой надо дождаться, чтобы снова побежать")]
    [Range(0f, 1f)]
    public float sprintRestartAt = 0.3f;
    [Tooltip("Насколько сбивается прицел при спринте, 0 — ровно")]
    public float sprintRecoil = 0.25f;
    [Tooltip("Сколько секунд нельзя начинать спринт после приземления с рывком")]
    public float sprintLockAfterLand = 0.18f;

    [Header("Прыжок")]
    [Tooltip("На какую высоту прыгает от пола, в юнитах")]
    public float jumpHeight = 2.2f;
    [Tooltip("Сколько можно удерживать кнопку, чтобы прыгнуть ниже")]
    public float jumpHoldTime = 0.16f;
    [Tooltip("Сколько ещё можно нажать прыжок после края — coyote time")]
    public float coyoteTime = 0.1f;
    [Tooltip("Сколько нажатие прыжка ждёт приземления — буфер ввода")]
    public float jumpBuffer = 0.12f;

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

    [Header("Способности")]
    [Tooltip("Какая способность выбрана сейчас: 0 рывок, 1 вихрь, 2 волна, 3 рывок с ударом, 4 двойной прыжок")]
    public int selectedAbility = 0;
    [Tooltip("Перезарядка каждой способности, секунды")]
    public float[] abilityCooldowns = { 0.9f, 1.6f, 3.0f, 2.0f, 0.6f };
    [Tooltip("Остаток перезарядки — заполняется в игре, не править")]
    public float[] abilityReady = { 0f, 0f, 0f, 0f, 0f };
    [Tooltip("Радиус вихря, юниты")]
    public float spinRadius = 1.7f;
    [Tooltip("Дальность волны по земле, юниты")]
    public float shockRange = 3.5f;
    [Tooltip("Скорость рывка, юниты в секунду")]
    public float dashSpeed = 14f;
    [Tooltip("Сколько длится рывок, секунды")]
    public float dashTime = 0.18f;
    [Tooltip("Урон рывка с ударом")]
    public int dashDamage = 2;
    [Tooltip("Рывок висит в воздухе, гравитация не тянет вниз")]
    public bool dashFloat = true;
    [Tooltip("Насколько бледнеет героиня на рывке — «растворяется в ветре»")]
    [Range(0f, 1f)]
    public float dashFade = 0.35f;
    [Tooltip("Лента ветра за спиной. Если пусто — просто гаснет")]
    public TrailRenderer trail;
    [Tooltip("Частицы: порывы, пыль, всплески. Если пусто — просто не видно")]
    public WindFx fx;
    [Tooltip("Высота двойного прыжка, юниты")]
    public float doubleJumpHeight = 1.5f;

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
    bool airJumpUsed;
    bool slamPending;
    float sprintBlockUntil = -99f;
    bool sprintExhausted;
    public float stamina = 1f;
    public bool sprinting { get; private set; }

    float dashTimer;
    float dashDir = 1f;
    int dashHit;
    bool dashFloating = true;
    float lastGroundedTime = -99f;
    float jumpPressedAt = -99f;
    float jumpStartedAt = -99f;
    float attackStartedAt = -99f;
    bool attacking;
    readonly System.Collections.Generic.HashSet<Damageable> hitThisSwing =
        new System.Collections.Generic.HashSet<Damageable>();
    readonly System.Collections.Generic.HashSet<Damageable> hitThisDash =
        new System.Collections.Generic.HashSet<Damageable>();

    // Идёт ли сейчас рывок: на этом держится и лента, и след из частиц
    public bool DashActive
    {
        get { return dashTimer > 0f; }
    }

    // Способности для HUD: название, клавича выбора, урон
    public static readonly string[] AbilityNames =
        { "Рывок ветра", "Вихрь", "Волна", "Рывок с ударом", "Двойной прыжок" };

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
        ReadAbilityInput();

        bool wantsSprint = ReadSprint(move, attacking);
        float speed = attacking
                    ? moveSpeed * attackMoveScale
                    : moveSpeed * (wantsSprint ? sprintMultiplier : 1f);
        float vx = body.velocity.x;

        float vy = body.velocity.y;
        if (dashTimer > 0f)
        {
            // рывок не даёт разгону мешать: скорость жёстко задана
            dashTimer -= Time.deltaTime;
            vx = dashDir * dashSpeed;
            if (dashFloating)
                vy = 0f;              // несёт ветром, вниз не тянет
            DashHit();
        }
        else
        {
            float accel = acceleration;
            if (wantsSprint)
                accel *= sprintAccelerationBonus;
            vx = Mathf.MoveTowards(vx, move * speed, accel * Time.deltaTime);
        }

        if (sprinting && fx != null)
            fx.SprintDust(Time.deltaTime);

        // короткий отскок назад в момент старта, чтобы спринт читался
        if (wantsSprint && !sprinting)
            vx -= Mathf.Sign(vx) * speed * sprintRecoil;
        sprinting = wantsSprint;

        body.velocity = new Vector2(vx, vy);

        // «растворяется в ветре»: на рывке фигура бледнеет и тянет ленту
        Color tint = spriteRenderer.color;
        float want = dashTimer > 0f ? dashFade : 1f;
        if (!Mathf.Approximately(tint.a, want))
        {
            tint.a = want;
            spriteRenderer.color = tint;
        }
        if (trail != null)
            trail.emitting = dashTimer > 0f;

        if (slamPending && grounded)
        {
            slamPending = false;
            Shockwave();
        }

        Animate(move);
        UpdateAttackHit();
        RespawnIfFallen();
    }

    // Спринт со стаминой. Держится, пока есть запас и игрок реально
    // жмёт в сторону; в воздухе и во время удара не работает. Кончился —
    // ждём, пока наберётся sprintRestartAt, иначе можно было бы
    // дёргать спринт на кадр и бежать вечно.
    bool ReadSprint(float move, bool isAttacking)
    {
        bool held = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool onGround = grounded && !isAttacking && Mathf.Abs(move) > 0.01f
                     && dashTimer <= 0f && Time.time >= sprintBlockUntil;

        if (stamina >= sprintRestartAt)
            sprintExhausted = false;
        else
            sprintExhausted = true;

        if (onGround && held && !sprintExhausted)
        {
            stamina = Mathf.Max(0f, stamina - Time.deltaTime / Mathf.Max(0.01f, sprintDuration));
            return true;
        }

        stamina = Mathf.Min(1f, stamina + Time.deltaTime / Mathf.Max(0.01f, staminaRecoverTime));
        return false;
    }

    // Луч вниз из под центра: стоим ли мы на чём-то сейчас
    void UpdateGrounded()
    {
        Vector2 from = (Vector2)transform.position + Vector2.up * 0.15f;
        bool nowGrounded = Physics2D.Raycast(from, Vector2.down, 0.3f);
        if (nowGrounded && !grounded)
        {
            lastGroundedTime = Time.time;
            airJumpUsed = false;      // двойной прыжок снова доступен
            if (fx != null) fx.Land();
        }
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
            Launch(jumpHeight);
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

    void Launch(float height)
    {
        float g = Mathf.Abs(Physics2D.gravity.y) * body.gravityScale;
        body.velocity = new Vector2(body.velocity.x, Mathf.Sqrt(2f * g * height));
        if (fx != null) fx.Jump();
    }

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

    void ReadAbilityInput()
    {
        for (int i = 0; i < abilityReady.Length; i++)
            if (abilityReady[i] > 0f)
                abilityReady[i] = Mathf.Max(0f, abilityReady[i] - Time.deltaTime);

        // выбор способности: клавиши 1…5
        for (int i = 0; i < 5; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                selectedAbility = i;

        // применение: K или F
        if (Input.GetKeyDown(KeyCode.F))
            UseAbility();
    }

    void UseAbility()
    {
        int s = Mathf.Clamp(selectedAbility, 0, abilityReady.Length - 1);
        if (abilityReady[s] > 0f)
            return;

        switch (s)
        {
            case 0:     // рывок ветра
                dashDir = face;
                dashTimer = dashTime;
                dashHit = 0;
                dashFloating = dashFloat;
                hitThisDash.Clear();
                if (fx != null) fx.Dash(dashDir);
                break;

            case 1:     // вихрь
                HitAround(transform.position, spinRadius, 1);
                break;

            case 2:     // удар о землю
                slamPending = true;
                body.velocity = new Vector2(body.velocity.x * 0.3f, 3.5f);
                break;

            case 3:     // рывок с ударом — прижата к земле, не парит
                dashDir = face;
                dashTimer = dashTime * 1.4f;
                dashHit = dashDamage;
                dashFloating = false;
                hitThisDash.Clear();
                break;

            case 4:     // двойной прыжок — только в воздухе
                if (grounded || airJumpUsed)
                    return;
                airJumpUsed = true;
                Launch(doubleJumpHeight);
                break;
        }

        if (fx != null) fx.Ability();
        abilityReady[s] = abilityCooldowns[s];
    }

    // Из всего, что попало в зону, оставляем только цели. Свои же
    // коллайдеры пропускаем: иначе удар задевает героиню саму.
    Damageable TargetIn(Collider2D h)
    {
        if (h.attachedRigidbody == body)
            return null;
        Damageable target = h.GetComponent<Damageable>()
                         ?? h.GetComponentInParent<Damageable>();
        return target != null && target.IsAlive ? target : null;
    }

    void HitAround(Vector2 centre, float radius, int damage)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(centre, radius);
        foreach (Collider2D h in hits)
        {
            Damageable target = TargetIn(h);
            if (target != null)
                target.TakeHit(damage, centre);
        }
    }

    // Волна ползёт по земле в обе стороны, как от удара оземь
    void Shockwave()
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            transform.position, new Vector2(shockRange * 2f, 0.8f), 0f);
        foreach (Collider2D h in hits)
        {
            Damageable target = TargetIn(h);
            if (target != null)
                target.TakeHit(1, transform.position);
        }
    }

    // Всё, что оказалось на пути рывка, получает урон по одному разу
    void DashHit()
    {
        if (dashHit <= 0)
            return;
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            transform.position + new Vector3(0f, 0.9f, 0f),
            new Vector2(1.8f, 1.6f), 0f);
        foreach (Collider2D h in hits)
        {
            Damageable target = TargetIn(h);
            if (target != null && hitThisDash.Add(target))
            {
                target.TakeHit(dashHit, transform.position);
                if (fx != null) fx.Hit(target.transform.position, dashDir);
            }
        }
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
            Damageable target = TargetIn(h);
            if (target != null && hitThisSwing.Add(target))
            {
                target.TakeHit(attackDamage, transform.position);
                if (fx != null) fx.Hit(centre, face);
            }
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
        dashTimer = 0f;
        slamPending = false;
        stamina = 0f;
        sprintExhausted = true;
        sprintBlockUntil = Time.time + 1.5f;
    }
}
