using UnityEngine;
using UnityEngine.UI;

// Здоровье героини и полоска внизу экрана — как в Genshin: тёмная скруглённая
// плашка во всю ширину с числом «текущее / максимум» справа; зелёная заливка
// оранжевеет ниже 30% и пульсирует красным ниже 15%. Число и полоска
// догоняют реальное здоровье с малой задержкой, как в оригинале.
//
// Полоска — на своём канвасе и гаснет вместе с HUD, пока открыто меню
// (GachaSystem.IsMenuOpen). Компонент вешается на героиню кодом,
// сцена не правится. Урон наносится через TakeDamage: правой кнопкой
// в защите не проходит — как у Damageable.
public class HeroHealth : MonoBehaviour
{
    [Tooltip("Максимум здоровья")]
    public int maxHp = 15000;

    int hp;
    float shown;                 // сколько здоровья показывает полоска (для плавности)
    PlayerController pc;
    Vector2 startPosition;
    Coroutine flashing;

    GameObject canvasGo;
    Image fill;
    Text numbers;
    Font font;

    // Развешивается само при старте сцены с героиней.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Attach()
    {
        var pc = FindObjectOfType<PlayerController>();
        if (pc == null) return;
        if (pc.GetComponent<HeroHealth>() == null)
            pc.gameObject.AddComponent<HeroHealth>();
    }

    void Awake()
    {
        pc = GetComponent<PlayerController>();
        var body = GetComponent<Rigidbody2D>();
        startPosition = body != null ? body.position : (Vector2)transform.position;
        hp = maxHp;
        shown = maxHp;
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    void Start()
    {
        BuildBar();
    }

    /// <summary>Полный урон по героини. from — точка, от которой пришёл удар
    /// (оттуда и отбрасывает). В защите урон не проходит.</summary>
    public void TakeDamage(int amount, Vector2 from)
    {
        if (amount <= 0 || hp <= 0) return;
        if (pc != null && pc.IsGuarding) return;

        hp = Mathf.Max(0, hp - amount);

        var body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            Vector2 dir = ((Vector2)transform.position - from).normalized;
            if (dir == Vector2.zero) dir = Vector2.right;
            body.velocity = new Vector2(dir.x * 5.5f, 3f);
        }

        if (flashing != null) StopCoroutine(flashing);
        flashing = StartCoroutine(Flash());

        if (hp <= 0) Die();
    }

    public int Current { get { return hp; } }

    void Die()
    {
        // Пока системы смерти нет — как в Genshin после боя: телепорт на
        // старт и полное восстановление.
        var body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = startPosition;
            body.velocity = Vector2.zero;
        }
        hp = maxHp;
        shown = maxHp;
    }

    System.Collections.IEnumerator Flash()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) yield break;
        Color baseColor = sr.color;
        // При получении урона — яркое покраснение с миганием (как в Genshin)
        Color red = new Color(1f, 0.12f, 0.08f, baseColor.a);
        for (int i = 0; i < 3; i++)
        {
            sr.color = red;
            yield return new WaitForSeconds(0.09f);
            if (i < 2)
            {
                sr.color = baseColor;
                yield return new WaitForSeconds(0.06f);
            }
        }
        if (sr != null) sr.color = baseColor;
        flashing = null;
    }

    void Update()
    {
        if (canvasGo == null) return;

        // с HUD: пока меню открыто — полоска спрятана
        bool menu = GachaSystem.IsMenuOpen;
        if (canvasGo.activeSelf == menu) canvasGo.SetActive(!menu);
        if (!canvasGo.activeSelf) return;

        // плавные полоска и числа
        shown = Mathf.Lerp(shown, hp, Mathf.Clamp01(Time.deltaTime * 10f));
        if (Mathf.Abs(shown - hp) < 0.5f) shown = hp;

        float t = maxHp > 0 ? shown / maxHp : 0f;
        fill.fillAmount = t;
        if (t <= 0.15f)
            fill.color = Color.Lerp(new Color(1f, 0.45f, 0.35f), new Color(1f, 0.2f, 0.15f),
                                    Mathf.PingPong(Time.time * 2.5f, 1f));
        else if (t <= 0.3f)
            fill.color = new Color(1f, 0.68f, 0.25f);
        else
            fill.color = new Color(0.42f, 0.86f, 0.47f);

        string txt = Mathf.RoundToInt(shown) + " / " + maxHp;
        if (numbers.text != txt) numbers.text = txt;
    }

    // =================== ПОЛОСКА ВНИЗУ ===================

    void BuildBar()
    {
        if (canvasGo != null) return;

        var cgo = new GameObject("HealthCanvas", typeof(Canvas), typeof(CanvasScaler));
        cgo.transform.SetParent(transform, false);
        canvasGo = cgo;
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // под игровым канвасом (100)
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // тёмная рамка
        var frame = Make(cgo.transform, "Frame", new Color(0.04f, 0.04f, 0.06f, 0.92f));
        var frt = frame.rectTransform;
        frt.anchorMin = frt.anchorMax = frt.pivot = new Vector2(0.5f, 0f);
        frt.anchoredPosition = new Vector2(0f, 30f);
        frt.sizeDelta = new Vector2(500f, 34f);
        frame.sprite = RoundedSprite(); frame.type = Image.Type.Sliced;

        // внутренний тёмный фон
        var bg = Make(frame.transform, "Bg", new Color(0.1f, 0.1f, 0.13f, 0.95f));
        bg.sprite = RoundedSprite(); bg.type = Image.Type.Sliced;
        Stretch(bg.rectTransform);
        bg.rectTransform.offsetMin = new Vector2(3f, 3f);
        bg.rectTransform.offsetMax = new Vector2(-3f, -3f);

        // зелёная заливка — обрезается по горизонтали
        fill = Make(bg.transform, "Fill", new Color(0.42f, 0.86f, 0.47f));
        fill.sprite = RoundedSprite(); fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 1f;
        fill.raycastTarget = false;
        Stretch(fill.rectTransform);
        fill.rectTransform.offsetMin = new Vector2(1f, 1f);
        fill.rectTransform.offsetMax = new Vector2(-1f, -1f);

        // числа справа, как в Genshin: «15432 / 15000»
        var tgo = new GameObject("Numbers", typeof(RectTransform), typeof(Text));
        tgo.transform.SetParent(frame.transform, false);
        numbers = tgo.GetComponent<Text>();
        numbers.font = font;
        numbers.fontSize = 17;
        numbers.alignment = TextAnchor.MiddleRight;
        numbers.color = Color.white;
        numbers.raycastTarget = false;
        numbers.horizontalOverflow = HorizontalWrapMode.Overflow;
        numbers.text = hp + " / " + maxHp;
        Stretch(numbers.rectTransform);
        numbers.rectTransform.offsetMin = new Vector2(14f, 0f);
        numbers.rectTransform.offsetMax = new Vector2(-16f, 0f);
        var outline = tgo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    static Image Make(Transform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // Скруглённый прямоугольник, нарисованный кодом — тот же приём,
    // что и в GachaSystem (внешние ассеты не нужны).
    static Sprite rounded;

    static Sprite RoundedSprite()
    {
        if (rounded != null) return rounded;
        int n = 64; float r = 20f;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cx = Mathf.Clamp(x, r, n - 1 - r), cy = Mathf.Clamp(y, r, n - 1 - r);
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                px[y * n + x] = new Color(1f, 1f, 1f, d <= r ? 1f : 0f);
            }
        tex.SetPixels(px);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        rounded = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100,
                                0, SpriteMeshType.FullRect, new Vector4(22, 22, 22, 22));
        return rounded;
    }
}
