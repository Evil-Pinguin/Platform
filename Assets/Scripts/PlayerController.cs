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

    [Header("Удар при падении")]
    [Tooltip("Кадры удара в падении: замах в воздухе, удар ножом вниз")]
    public Sprite[] fallAttackFrames;
    [Tooltip("Сколько длится удар при падении, секунды")]
    public float fallAttackDuration = 0.34f;
    [Tooltip("Урон удара при падении. Обычный удар бьёт слабее")]
    public int fallAttackDamage = 2;
    [Tooltip("Центр хитбокса удара при падении по горизонтали, юниты")]
    public float fallHitboxCenterX = 0.5f;
    [Tooltip("Центр хитбокса удара при падении по вертикали, юниты")]
    public float fallHitboxCenterY = 0.35f;
    [Tooltip("Размер хитбокса удара при падении, юниты")]
    public Vector2 fallHitboxSize = new Vector2(1.1f, 1.6f);
    [Tooltip("Окно удара при падении, с какой доли и до какой, 0…1")]
    public Vector2 fallHitWindow = new Vector2(0.25f, 0.8f);

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

    [Header("Полёт на крыльях (у персонажей с крыльями, напр. Айыына)")]
    [Tooltip("Включается автоматически, когда выбран крылатый персонаж")]
    public bool canFly = false;
    [Tooltip("Скорость взлёта, пока зажат пробел")]
    public float flyUpSpeed = 7f;
    [Tooltip("Сколько секунд можно набирать высоту за один полёт")]
    public float flyRiseTime = 1.1f;
    [Tooltip("Скорость медленного падения с раскрытыми крыльями")]
    public float glideFallSpeed = 1.2f;
    [Tooltip("Кадр с раскрытыми крыльями (если пусто — второй кадр прыжка)")]
    public Sprite flySprite;
    [Tooltip("Кадр падения/планирования: крылья раскрыты во всю ширину (если пусто — кадр полёта)")]
    public Sprite glideSprite;
    public bool IsFlying { get; private set; }
    float flyRiseLeft;

    [Header("Анимация")]
    [Tooltip("Кадры ходьбы по порядку (нарисованы лицом вправо)")]
    public Sprite[] walkFrames;
    [Tooltip("Сколько кадров ходьбы показывать в секунду")]
    public float walkFps = 12f;
    [Tooltip("Спрайт, когда героиня стоит на месте")]
    public Sprite idleSprite;
    [Tooltip("Кадры прыжка по порядку: взлёт, падение (нарисованы лицом вправо)")]
    public Sprite[] jumpFrames;
    [Tooltip("С какой вертикальной скорости показывается падающий кадр, юниты/с")]
    public float jumpFallSpeed = 0.1f;
    [Header("Защита")]
    [Tooltip("Поза прикрытия. Правая кнопка мыши")]
    public Sprite guardSprite;
    [Tooltip("Насколько медленно она идёт в прикрытии")]
    public float guardMoveScale = 0.3f;

    /// <summary>Правда, пока держится прикрытие. Ею пользуется Damageable:
    /// удар в закрытую защиту не проходит.</summary>
    public bool IsGuarding { get; private set; }

    [Header("Если упала с края")]
    [Tooltip("Ниже этой высоты героиня возвращается в точку старта")]
    public float respawnBelowY = -12f;

    Rigidbody2D body;
    SpriteRenderer spriteRenderer;
    Vector2 startPosition;
    float face = 1f;
    float animTimer;
    int shownFrame = -1;
    readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
    bool grounded;
    bool airJumpUsed;
    bool slamPending;
    float dashTimer;
    float dashDir = 1f;
    int dashHit;
    bool dashFloating = true;
    float lastGroundedTime = -99f;
    float jumpPressedAt = -99f;
    float jumpStartedAt = -99f;
    float attackStartedAt = -99f;
    bool attacking;
    bool fallingStrike;      // текущий удар — в падении, ножом вниз
    Sprite[] currentAttackFrames;
    Sprite[][] comboAttackSets;
    int nextComboStep;
    int activeComboStep = -1;
    int queuedComboAttacks;
    float comboExpiresAt;
    const float ComboQueueWindow = 0.35f;
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

    // Смена облика героини (персонаж из гачи). Отсутствующие кадры сохраняют
    // прежний набор; явно переданный пустой набор прыжка его очищает, чтобы
    // кадры предыдущего персонажа не просачивались в новую анимацию.
    public void ApplySkin(Sprite idle, Sprite[] walk, Sprite[] jump,
                          Sprite[] attack, Sprite[] fall, Sprite guard, float walkFramesPerSecond,
                          bool flying = false, Sprite fly = null, Sprite glide = null,
                          Sprite[][] comboAttacks = null)
    {
        canFly = flying;
        comboAttackSets = comboAttacks != null && comboAttacks.Length >= 4 ? comboAttacks : null;
        nextComboStep = 0;
        activeComboStep = -1;
        queuedComboAttacks = 0;
        comboExpiresAt = 0f;
        attacking = false;
        fallingStrike = false;
        currentAttackFrames = null;
        flySprite = fly;
        glideSprite = glide;
        if (idle != null) idleSprite = idle;
        if (walk != null && walk.Length > 0) walkFrames = walk;
        // A supplied empty jump array clears stale frames from the previous skin.
        if (jump != null) jumpFrames = jump;
        if (attack != null && attack.Length > 0) attackFrames = attack;
        if (fall != null && fall.Length > 0) fallAttackFrames = fall;
        if (guard != null) guardSprite = guard;
        if (walkFramesPerSecond > 0f) walkFps = walkFramesPerSecond;
        shownFrame = int.MinValue;
        if (spriteRenderer != null && idleSprite != null)
            spriteRenderer.sprite = idleSprite;
    }

    void Update()
    {
        // Открыто меню гачи/персонажей — героиня не реагирует на ввод.
        if (GachaSystem.IsMenuOpen)
            return;

        float move = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(move) > 0.01f)
            face = move > 0f ? 1f : -1f;

        UpdateGrounded();
        ReadJumpInput();
        ReadGuardInput();
        ReadAttackInput();
        ReadAbilityInput();

        float speed = moveSpeed;
        if (attacking)
            speed *= attackMoveScale;
        else if (IsGuarding)
            speed *= guardMoveScale;
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
            vx = Mathf.MoveTowards(vx, move * speed, acceleration * Time.deltaTime);
        }

        // Полёт: зажат пробел в воздухе — сначала взлёт, потом плавное планирование
        IsFlying = false;
        if (canFly && !grounded && dashTimer <= 0f && FlyHeld())
        {
            IsFlying = true;
            if (flyRiseLeft > 0f)
            {
                flyRiseLeft -= Time.deltaTime;
                vy = Mathf.Max(vy, flyUpSpeed);
            }
            else if (vy < -glideFallSpeed)
            {
                vy = -glideFallSpeed;
            }
        }
        if (grounded)
            flyRiseLeft = flyRiseTime;   // на земле запас взлёта восстанавливается

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

    // Луч вниз из под центра: стоим ли мы на чём-то сейчас
    void UpdateGrounded()
    {
        // Луч начинается внутри собственной капсулы героини, а в настройках
        // физики включено «Queries Start In Colliders» — поэтому свой коллайдер
        // и триггеры пропускаем, иначе героиня «стоит на земле» даже в воздухе
        // (не было кадров прыжка/падения и не работал полёт).
        Vector2 from = (Vector2)transform.position + Vector2.up * 0.15f;
        int n = Physics2D.RaycastNonAlloc(from, Vector2.down, groundHits, 0.3f);
        bool nowGrounded = false;
        for (int i = 0; i < n; i++)
        {
            Collider2D c = groundHits[i].collider;
            if (c == null || c.isTrigger || c.attachedRigidbody == body)
                continue;
            nowGrounded = true;
            break;
        }
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
        if (!held && !IsFlying && Time.time - jumpStartedAt < jumpHoldTime && body.velocity.y > 0f)
        {
            body.velocity = new Vector2(body.velocity.x, body.velocity.y * 0.45f);
            jumpStartedAt = -99f;
        }
    }

    static bool FlyHeld()
    {
        return Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
    }

    void Launch(float height)
    {
        float g = Mathf.Abs(Physics2D.gravity.y) * body.gravityScale;
        body.velocity = new Vector2(body.velocity.x, Mathf.Sqrt(2f * g * height));
        if (fx != null) fx.Jump();
    }

    void ReadGuardInput()
    {
        // Правая кнопка мыши. В прикрытии можно и стоять, и идти, но только
        // на земле: в воздухе прикрытие не держать, там кадры прыжка.
        bool held = Input.GetMouseButton(1) || Input.GetKey(KeyCode.L);
        IsGuarding = held && grounded && !attacking;
    }

    bool AttackPressed()
    {
        bool clickOnUi = UnityEngine.EventSystems.EventSystem.current != null
                         && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        return (Input.GetMouseButtonDown(0) && !clickOnUi)
            || Input.GetKeyDown(KeyCode.J)
            || Input.GetKeyDown(KeyCode.K);
    }

    void ReadAttackInput()
    {
        bool pressed = AttackPressed();
        if (attacking)
        {
            float dur = fallingStrike ? fallAttackDuration : attackDuration;
            bool comboCanContinue = !fallingStrike && activeComboStep >= 0
                                    && comboAttackSets != null && grounded;
            if (pressed && comboCanContinue)
            {
                int remainingHits = Mathf.Max(0, 3 - activeComboStep - queuedComboAttacks);
                if (remainingHits > 0)
                    queuedComboAttacks++;
            }

            if (Time.time - attackStartedAt >= dur + attackCooldown)
            {
                bool continueCombo = comboCanContinue && queuedComboAttacks > 0;
                attacking = false;
                fallingStrike = false;
                currentAttackFrames = null;
                animTimer = 0f;
                shownFrame = -1;

                if (continueCombo)
                {
                    queuedComboAttacks--;
                    BeginAttack();
                }
                else
                {
                    queuedComboAttacks = 0;
                    activeComboStep = -1;
                }
            }
            return;
        }

        if (!pressed || (attackFrames == null && comboAttackSets == null))
            return;

        BeginAttack();
    }

    void BeginAttack()
    {
        // В воздухе — всегда удар сверху, и на подъёме, и на падении.
        fallingStrike = !grounded
                     && fallAttackFrames != null && fallAttackFrames.Length > 0;
        activeComboStep = -1;

        if (fallingStrike)
        {
            currentAttackFrames = fallAttackFrames;
        }
        else if (comboAttackSets != null && comboAttackSets.Length >= 4)
        {
            if (Time.time > comboExpiresAt)
                nextComboStep = 0;
            activeComboStep = nextComboStep;
            nextComboStep = (nextComboStep + 1) % 4;
            comboExpiresAt = Time.time + attackDuration + attackCooldown + ComboQueueWindow;
            currentAttackFrames = comboAttackSets[activeComboStep];
            if (currentAttackFrames == null || currentAttackFrames.Length == 0)
                currentAttackFrames = attackFrames;
        }
        else
        {
            currentAttackFrames = attackFrames;
        }

        if (currentAttackFrames == null || currentAttackFrames.Length == 0)
        {
            fallingStrike = false;
            activeComboStep = -1;
            return;
        }

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

        float dur = fallingStrike ? fallAttackDuration : attackDuration;
        Vector2 win = fallingStrike ? fallHitWindow : hitWindow;
        float t = (Time.time - attackStartedAt) / dur;
        if (t < win.x || t > win.y)
            return;

        // Удар при падении достаёт вниз и вперёд, поэтому хитбокс ниже,
        // уже и вытянут по вертикали.
        float centreX = fallingStrike ? fallHitboxCenterX : hitboxCenterX;
        float centreY = fallingStrike ? fallHitboxCenterY : hitboxCenterY;
        Vector2 size = fallingStrike ? fallHitboxSize : hitboxSize;
        int damage = fallingStrike ? fallAttackDamage : attackDamage;

        // У четырёх ударов Күн Куо разные области действия; четвёртый — усиленный финал.
        if (!fallingStrike)
        {
            switch (activeComboStep)
            {
                case 0: centreX = 0.95f; centreY = 0.95f; size = new Vector2(1.35f, 1.2f); break;
                case 1: centreX = 1.0f; centreY = 0.75f; size = new Vector2(1.8f, 1.3f); break;
                case 2: centreX = 0f; centreY = 1.0f; size = new Vector2(2.2f, 2.0f); break;
                case 3:
                    centreX = 0.85f; centreY = 1.15f; size = new Vector2(1.8f, 2.2f);
                    damage = attackDamage + 1;
                    break;
            }
        }

        Vector2 centre = (Vector2)transform.position + new Vector2(centreX * face, centreY);

        Collider2D[] hits = Physics2D.OverlapBoxAll(centre, size, 0f);
        foreach (Collider2D h in hits)
        {
            Damageable target = TargetIn(h);
            if (target != null && hitThisSwing.Add(target))
            {
                target.TakeHit(damage, transform.position);
                if (fx != null) fx.Hit(centre, face);
            }
        }
    }

    void Animate(float move)
    {
        // Удар при падении — раньше всего остального: в воздухе он важнее и
        // кадров прыжка, и ходьбы, и прикрытия
        if (attacking && fallingStrike
            && fallAttackFrames != null && fallAttackFrames.Length > 0)
        {
            animTimer += Time.deltaTime;
            int ff = (int)(animTimer / fallAttackDuration * fallAttackFrames.Length);
            if (ff >= fallAttackFrames.Length)
                ff = fallAttackFrames.Length - 1;
            if (ff != shownFrame)
            {
                shownFrame = ff;
                spriteRenderer.sprite = fallAttackFrames[ff];
            }
            spriteRenderer.flipX = face < 0f;
            return;
        }

        // Удар важнее ходьбы: у Күн Куо каждый шаг комбо со своими кадрами.
        if (attacking && currentAttackFrames != null && currentAttackFrames.Length > 0)
        {
            animTimer += Time.deltaTime;
            int f = (int)(animTimer / attackDuration * currentAttackFrames.Length);
            if (f >= currentAttackFrames.Length)
                f = currentAttackFrames.Length - 1;
            if (f != shownFrame)
            {
                shownFrame = f;
                spriteRenderer.sprite = currentAttackFrames[f];
            }
            spriteRenderer.flipX = face < 0f;
            return;
        }

        // Прикрытие важнее ходьбы и покоя, но не важнее удара и прыжка:
        // в воздухе защиту не держать, там остаются кадры прыжка.
        if (IsGuarding && grounded && guardSprite != null)
        {
            animTimer = 0f;
            if (shownFrame != -2)
            {
                shownFrame = -2;
                spriteRenderer.sprite = guardSprite;
            }
            spriteRenderer.flipX = face < 0f;
            return;
        }

        // В воздухе — кадры прыжка: взлёт, пока ещё летит вверх, и падение,
        // как только начала опускаться. Кадры взяты в полный рост, поэтому
        // переход ходьба -> прыжок не меняет размер фигуры.
        // Крылатая героиня: в полёте и при любом падении крылья раскрыты
        if (IsFlying || (canFly && !grounded && body.velocity.y <= jumpFallSpeed))
        {
            bool falling = body.velocity.y <= jumpFallSpeed;
            Sprite wings = falling && glideSprite != null ? glideSprite
                         : flySprite != null ? flySprite
                         : (jumpFrames != null && jumpFrames.Length > 0 ? jumpFrames[jumpFrames.Length - 1] : null);
            int id = wings == glideSprite ? -4 : -3;
            if (wings != null && shownFrame != id)
            {
                shownFrame = id;
                spriteRenderer.sprite = wings;
            }
            spriteRenderer.flipX = face < 0f;
            return;
        }

        if (!grounded && jumpFrames != null && jumpFrames.Length > 0)
        {
            int frame = body.velocity.y > jumpFallSpeed ? 0 : jumpFrames.Length - 1;
            if (frame != shownFrame)
            {
                shownFrame = frame;
                spriteRenderer.sprite = jumpFrames[frame];
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
            // Стоим на месте — поза покоя
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
        // Без сброса флаг удара при падении пережил бы возрождение: мы
        // появились бы на земле, а считали бы себя в воздухе.
        attacking = false;
        fallingStrike = false;
        currentAttackFrames = null;
        activeComboStep = -1;
        queuedComboAttacks = 0;
        nextComboStep = 0;
        comboExpiresAt = 0f;
    }
}
