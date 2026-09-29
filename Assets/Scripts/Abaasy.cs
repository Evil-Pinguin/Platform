using System.Collections;
using UnityEngine;

// Абааһы (абасы) — чудовище якутского эпоса олонхо и поверий: великан-богатырь
// абаасы, противник племени айыы, «как правило многоголовый» и с огненным
// дыханием (по олонхо: «богатыри абаасы изображаются чудовищами с огненным
// дыханием, выпускали огонь изо рта», ходили в железе — отсюда ошейник).
//
// Монстр стоит в мире сам по себе: сторожит, преследует героиню и дышит
// огнём. Его можно бить как обычную цель (Damageable) — при уничтожении
// срабатывают общие правила награды: +5 алмазов (GachaSystem.destroyReward).
//
// Спавн — кодом, без правок сцены: в TestLevel рядом с героиней, на земле.
public class Abaasy : MonoBehaviour
{
    [Tooltip("Скорость преследования, юниты/с")]
    public float chaseSpeed = 2.2f;
    [Tooltip("Дальность, с которой начинает преследовать")]
    public float aggroRange = 8f;
    [Tooltip("Дальность огненного дыхания, юниты")]
    public float breathRange = 4.5f;
    [Tooltip("Перезарядка дыхания, секунды")]
    public float breathCooldown = 3.5f;
    [Tooltip("Сколько языков пламени вылетает за раз")]
    public int breathFlames = 5;
    [Tooltip("Скорость пламени, юниты/с")]
    public float flameSpeed = 7f;
    [Tooltip("Урон пламени целям с Damageable")]
    public int flameDamage = 1;
    [Tooltip("Урон пламени по здоровью героини (HeroHealth)")]
    public int breathPlayerDamage = 1200;
    [Tooltip("Ниже этой высоты — упал в пропасть, исчезает без награды")]
    public float killY = -25f;

    Rigidbody2D body;
    SpriteRenderer rend;
    BoxCollider2D box;
    PlayerController target;
    float cooldown;

    // ---------- появление в мире ----------

    // Два монстра в TestLevel: на рантайме, сцена не правится.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void SpawnInTestLevel()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "TestLevel")
            return;
        var pc = FindObjectOfType<PlayerController>();
        if (pc == null) return;
        var sp = Resources.Load<Sprite>("Monsters/Abaasy");
        if (sp == null) return;
        SpawnNear(pc.transform.position, 5f, sp);
        SpawnNear(pc.transform.position, 9.5f, sp);
    }

    // Ищем землю под точкой: от заданного смещения сдвигаемся к героини,
    // пока луч вниз не упрётся в грунт (над пропастью не встанем).
    static void SpawnNear(Vector2 playerPos, float dx, Sprite sp)
    {
        for (float step = 0f; step <= 6f; step += 1f)
        {
            float x = playerPos.x + dx - step;
            Vector2 from = new Vector2(x, playerPos.y + 6f);
            float groundY = float.MinValue;
            foreach (var h in Physics2D.RaycastAll(from, Vector2.down, 14f))
            {
                if (h.collider != null && h.collider.GetComponentInParent<PlayerController>() != null)
                    continue; // это героиня, а не земля
                if (h.point.y > groundY) groundY = h.point.y;
            }
            if (groundY > float.MinValue && Mathf.Abs(groundY - playerPos.y) < 6f)
            {
                Create(new Vector2(x, groundY + 0.05f), sp);
                return;
            }
        }
    }

    static void Create(Vector2 feet, Sprite sp)
    {
        var go = new GameObject("Абааһы");
        go.transform.position = new Vector3(feet.x, feet.y, 0f);

        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sp;
        r.sortingOrder = 5;

        var b = go.AddComponent<Rigidbody2D>();
        b.gravityScale = 2.5f;
        b.freezeRotation = true;
        b.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(2.4f, 2.0f);
        box.offset = new Vector2(0f, 1.05f);

        var dmg = go.AddComponent<Damageable>();
        dmg.health = 6;
        dmg.knockback = 3f;
        dmg.vanishOnDeath = true;

        var ab = go.AddComponent<Abaasy>();
        // Awake уже нашёл цель; если игрока вовсе нет — бессмысленно стоять
        if (ab.target == null) Destroy(go);
    }

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        rend = GetComponent<SpriteRenderer>();
        box = GetComponent<BoxCollider2D>();
        if (target == null && !GachaSystem.MonstersIgnoreHero)
            target = FindObjectOfType<PlayerController>();
    }

    void Update()
    {
        if (cooldown > 0f) cooldown -= Time.deltaTime;
        if (transform.position.y < killY) Destroy(gameObject); // сорвался в пропасть — без награды
    }

    void FixedUpdate()
    {
        // Мирный персонаж (Хаара) — монстр теряет цель: стоит на месте и не дышит огнём
        if (GachaSystem.MonstersIgnoreHero) { target = null; return; }
        if (body == null) return;
        if (target == null) target = FindObjectOfType<PlayerController>();
        if (target == null) return;

        Vector2 self = transform.position;
        Vector2 pl = target.transform.position;
        float dx = pl.x - self.x;
        float adx = Mathf.Abs(dx);
        float dy = Mathf.Abs(pl.y - self.y);
        float dir = dx < 0f ? -1f : 1f;

        // лицом — к героини; спрайт нарисован лицом вправо
        if (rend != null) rend.flipX = dir < 0f;

        bool chasing = adx <= aggroRange && dy <= 3.5f;
        if (!chasing)
        {
            body.velocity = new Vector2(0f, body.velocity.y);
            return;
        }

        // в воздухе (отброшена ударом) — ИИ не перехватывает горизонталь,
        // иначе отброс от удара мгновенно гаснет
        if (body.velocity.y > 0.4f) return;

        // идёт, пока впереди есть куда идти (к пропасти не подходит)
        bool ahead = GroundAhead(dir);
        if (adx > 1.8f && ahead)
            body.velocity = new Vector2(dir * chaseSpeed, body.velocity.y);
        else
            body.velocity = new Vector2(0f, body.velocity.y);

        // огненное дыхание, как у богатырей-абаасы в олонхо
        if (adx <= breathRange && dy <= 2.8f && cooldown <= 0f)
            Breath(dir);
    }

    // Есть ли земля чуть впереди — чтобы не шагнуть в пустоту
    bool GroundAhead(float dir)
    {
        Vector2 from = (Vector2)transform.position + new Vector2(dir * 1.1f, 0.4f);
        foreach (var h in Physics2D.RaycastAll(from, Vector2.down, 4f))
        {
            if (h.collider == box) continue;                      // собственный хитбокс
            if (h.collider.GetComponentInParent<PlayerController>() != null) continue; // героиня
            return true;
        }
        return false;
    }

    void Breath(float dir)
    {
        cooldown = breathCooldown;
        Vector2 mouth = (Vector2)transform.position + new Vector2(dir * 1.7f, 1.6f);
        for (int i = 0; i < breathFlames; i++)
        {
            float spread = (i - (breathFlames - 1) * 0.5f) * 0.14f;
            AbaasyFlame.Spawn(mouth, new Vector2(dir, spread).normalized * flameSpeed,
                              transform, flameDamage, breathPlayerDamage);
        }
    }
}

// Язык огненного дыхания: летит по заданной скорости, гаснет, по дороге
// жжёт цели. Героиню (без Damageable) отбрасывает и коротко красит.
public class AbaasyFlame : MonoBehaviour
{
    Vector2 speed;
    Transform owner;
    int damage;        // по целям с Damageable (мишени, монстры)
    int playerDamage;  // по здоровью героини
    float life = 1.1f;
    SpriteRenderer rend;

    public static void Spawn(Vector2 pos, Vector2 speed, Transform owner, int damage, int playerDamage)
    {
        var go = new GameObject("AbaasyFlame");
        go.transform.position = pos;
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = FlameSprite();
        r.color = new Color(1f, 0.62f, 0.18f, 1f);
        r.sortingOrder = 6;
        var f = go.AddComponent<AbaasyFlame>();
        f.speed = speed;
        f.owner = owner;
        f.damage = damage;
        f.playerDamage = playerDamage;
        f.rend = r;
    }

    void Update()
    {
        transform.position += (Vector3)(speed * Time.deltaTime);
        life -= Time.deltaTime;
        if (rend != null)
            rend.color = Color.Lerp(new Color(1f, 0.62f, 0.18f, 1f),
                                    new Color(1f, 0.15f, 0.05f, 0f), 1f - Mathf.Clamp01(life / 1.1f));
        transform.localScale = Vector3.one * Mathf.Lerp(1.35f, 0.6f, 1f - Mathf.Clamp01(life / 1.1f));
        if (life <= 0f) { Destroy(gameObject); return; }

        // героиня рядом — отбросить и вспышка (защита правой кнопкой спасает)
        if (HitPlayer()) { Destroy(gameObject); return; }

        // обычные цели ( Damageable ): жгутся огнём
        foreach (var c in Physics2D.OverlapCircleAll(transform.position, 0.3f))
        {
            if (c == null) continue;
            var d = c.GetComponent<Damageable>() ?? c.GetComponentInParent<Damageable>();
            if (d == null) continue;
            if (owner != null && d.transform == owner) continue;          // не бьём того, кто дышит
            if (owner != null && d.transform.IsChildOf(owner)) continue;
            d.TakeHit(damage, (Vector2)transform.position - speed.normalized);
            Destroy(gameObject);
            return;
        }
    }

    bool HitPlayer()
    {
        if (owner == null) return false;
        var pc = FindTarget();
        if (pc == null) return false;
        if (((Vector2)pc.transform.position - (Vector2)transform.position).sqrMagnitude > 0.55f * 0.55f)
            return false;

        // правая кнопка (защита) — пламя гаснет, урона нет
        if (pc.IsGuarding) return true;

        var hp = pc.GetComponent<HeroHealth>();
        if (hp != null)
        {
            // урон по полоске здоровья + отброс и вспышка — внутри TakeDamage
            hp.TakeDamage(playerDamage, (Vector2)transform.position - speed.normalized);
        }
        else
        {
            var rb = pc.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.velocity = new Vector2(Mathf.Sign(speed.x != 0f ? speed.x : 1f) * 5.5f, 3f);
            pc.StartCoroutine(Flash(pc));
        }
        return true;
    }

    // Красная вспышка героини от ожога — на её же объекте, переживёт Flame
    static IEnumerator Flash(PlayerController pc)
    {
        var sr = pc.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;
        Color baseColor = sr.color;
        sr.color = new Color(1f, 0.45f, 0.4f, baseColor.a);
        yield return new WaitForSeconds(0.15f);
        if (sr != null) sr.color = baseColor;
    }

    static PlayerController cachedTarget;
    static PlayerController FindTarget()
    {
        // Мирный персонаж (Хаара) — монстры её не замечают:
        // нет цели → нет погони и дыхания, а уже летящее пламя её не задевает
        if (GachaSystem.MonstersIgnoreHero) return null;
        if (cachedTarget == null) cachedTarget = FindObjectOfType<PlayerController>();
        return cachedTarget;
    }

    // Мягкий огненный шар, нарисованный кодом — как и весь FX в этом проекте
    static Sprite flame;
    static Sprite FlameSprite()
    {
        if (flame != null) return flame;
        int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color[n * n];
        float c = (n - 1) / 2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                float a = Mathf.Clamp01(1f - d);
                px[y * n + x] = Color.Lerp(new Color(1f, 1f, 0.85f), new Color(1f, 0.4f, 0.1f), Mathf.Clamp01(d * 1.4f));
                px[y * n + x].a = a * a;
            }
        tex.SetPixels(px);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        flame = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64);
        return flame;
    }
}
