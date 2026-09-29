using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Гача и галерея персонажей в стиле Genshin Impact.
// Интерфейс целиком строится из кода. Если на сцене нет объекта с этим компонентом,
// он создаётся автоматически при старте игры (с набором персонажей по умолчанию).
// Горячие клавиши (как в Genshin): F3 — молитвы, C — персонажи, Esc — закрыть.
public class GachaSystem : MonoBehaviour
{
    public enum Element { Пиро, Гидро, Анемо, Электро, Дендро, Крио, Гео }

    [Serializable]
    public class CharacterData
    {
        public string name;
        [Range(4, 5)] public int rarity = 4;
        public Element element;
        [Tooltip("Портрет (необязательно). Без него рисуется цветная заглушка.")]
        public Sprite portrait;
        [TextArea] public string description;
    }

    [Header("Персонажи")]
    [Tooltip("Персонаж баннера (5★, шанс 50/50, затем гарантия)")]
    public string featuredCharacter = "Лилия";
    [Tooltip("Включено: все 5★ персонажи выпадают с равным шансом (без 50/50). Выключено: ивент-баннер одного персонажа.")]
    public bool allCharactersEqual = true;
    public List<CharacterData> characters = new List<CharacterData>();

    [Header("Экономика")]
    [Tooltip("Сколько алмазов даётся на старте")]
    public int startPrimogems = 1000000;
    public int wishCost = 160;

    [Header("Награды")]
    [Tooltip("Сколько алмазов за уничтоженный объект (Damageable)")]
    public int destroyReward = 5;

    [Header("Шансы (как в Genshin)")]
    public float fiveStarBase = 0.006f;
    public int softPityStart = 74;
    public int hardPity = 90;
    public float fourStarBase = 0.051f;
    public int fourStarPity = 10;

    [Tooltip("Ставить игру на паузу, пока открыто меню")]
    public bool pauseWhileOpen = true;

    // ---------- сохранение ----------
    int primogems, pity5, pity4, totalWishes;
    bool guaranteed;
    Dictionary<string, int> owned = new Dictionary<string, int>(); // имя -> кол-во копий
    const string SaveKey = "GachaSave_v1";
    static GachaSystem instance; // для наград из Damageable
    bool hudGemIcon;             // в HUD счётчика нарисован спрайт алмаза

    [Serializable] class SaveData
    {
        public int primogems, pity5, pity4, totalWishes, version; public bool guaranteed;
        public List<string> names = new List<string>(); public List<int> counts = new List<int>();
    }

    // ---------- UI ----------
    Font font;
    Canvas canvas;
    GameObject hud, wishPanel, charPanel, resultPanel;
    Text primoHudText, primoWishText, pityText;
    RectTransform charListContent;
    Image bigPortrait; Text bigPortraitLetter;
    Text infoName, infoStars, infoElement, infoDesc, infoConst;
    Image[] constNodes = new Image[6];
    int selectedChar = -1;
    string charTab = "Атрибуты";
    readonly Dictionary<string, Image> tabButtons = new Dictionary<string, Image>();
    bool animating;
    public static bool IsMenuOpen { get; private set; }

    // ---------- играбельные персонажи ----------
    // Кадры лежат в Assets/Resources/Playable/<Имя>/ :
    // idle_front, walk_N, jump_N, attack_N, fall_N, guard_1.
    [Header("Играбельные")]
    [Tooltip("Основная героиня: доступна сразу, её кадры берутся из PlayerController в сцене")]
    public string mainHeroine = "Лилия";
    string activeCharacter;
    const string ActiveKey = "GachaActiveCharacter";
    class Skin { public Sprite idle, guard; public Sprite[] walk, jump, attack, fall; public float fps; }
    Skin defaultSkin;
    Button playButton; Text playButtonText;

    // ---------- быстрый выбор персонажа справа (как в Genshin) ----------
    // Столбец кружков на правом краю экрана: показывает всех играбельных
    // персонажей (сейчас их 3 — Лилия, Айыына, Күн Куо), кликом меняем персонажа.
    // Клавиши 1…5 заняты способностями (PlayerController), поэтому переключение мышью.
    List<CharacterData> party = new List<CharacterData>();
    Image[] partyCell, partyRing, partyGlow;

    static readonly Color Gold = new Color(1f, 0.78f, 0.35f);
    static readonly Color Purple = new Color(0.72f, 0.5f, 1f);
    static readonly Color Blue = new Color(0.45f, 0.7f, 1f);
    static readonly Color PanelDark = new Color(0.1f, 0.11f, 0.16f, 0.96f);
    static readonly Color Cream = new Color(0.93f, 0.89f, 0.8f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindObjectOfType<GachaSystem>() == null)
            new GameObject("GachaSystem").AddComponent<GachaSystem>();
    }

    void Awake()
    {
        instance = this;
        LoadCharactersFromResources();
        if (characters.Count == 0) FillDefaultRoster();
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        Load();
        BuildUI();
    }

    // Каждая картинка из папки Assets/Resources/Characters становится легендарным (5★)
    // персонажем. Имя берётся из имени файла: «Лилия.png» -> «Лилия».
    void LoadCharactersFromResources()
    {
        var textures = Resources.LoadAll<Texture2D>("Characters");
        if (textures.Length == 0) return;
        Array.Sort(textures, (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        var elements = (Element[])Enum.GetValues(typeof(Element));
        foreach (var tex in textures)
        {
            // Имя файла вида «Имя (Элемент).png» — элемент берётся из скобок.
            string charName = tex.name;
            Element element = elements[Mathf.Abs(tex.name.GetHashCode()) % elements.Length];
            int br = tex.name.IndexOf('(');
            if (br > 0 && tex.name.EndsWith(")"))
            {
                charName = tex.name.Substring(0, br).Trim();
                string el = tex.name.Substring(br + 1, tex.name.Length - br - 2).Trim();
                foreach (var e in elements) if (e.ToString() == el) element = e;
            }
            if (characters.Exists(c => c.name == charName)) continue;
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100);
            characters.Add(new CharacterData
            {
                name = charName,
                rarity = 5,
                element = element,
                portrait = sprite,
                description = "Легендарный персонаж."
            });
        }
        if (!characters.Exists(c => c.name == featuredCharacter && c.rarity == 5))
            featuredCharacter = characters.Find(c => c.rarity == 5).name;
    }

    void Start()
    {
        var pc = FindObjectOfType<PlayerController>();
        if (pc != null)
            defaultSkin = new Skin { idle = pc.idleSprite, guard = pc.guardSprite, walk = pc.walkFrames,
                                     jump = pc.jumpFrames, attack = pc.attackFrames, fall = pc.fallAttackFrames, fps = pc.walkFps };
        activeCharacter = PlayerPrefs.GetString(ActiveKey, mainHeroine);
        // достаточно быть играбельным (есть спрайты) — выбор из панели справа сохраняется
        if (!IsPlayable(activeCharacter)) activeCharacter = mainHeroine;
        ApplyActive();
        RefreshPartyBar();
    }

    bool IsPlayable(string name)
    {
        return name == mainHeroine || Resources.Load<Sprite>("Playable/" + name + "/idle_front") != null;
    }

    // Монстры не нападают на мирных персонажей — пока такая только Хаара
    public static bool MonstersIgnoreHero
    {
        get { return instance != null && instance.activeCharacter == "Хаара"; }
    }

    static Sprite[] LoadFrames(string name, string prefix)
    {
        var list = new List<Sprite>();
        for (int i = 1; i <= 16; i++)
        {
            var sp = Resources.Load<Sprite>("Playable/" + name + "/" + prefix + "_" + i);
            if (sp == null) break;
            list.Add(sp);
        }
        return list.ToArray();
    }

    void ApplyActive()
    {
        var pc = FindObjectOfType<PlayerController>();
        if (pc == null) return;
        if (activeCharacter == mainHeroine || !IsPlayable(activeCharacter))
        {
            if (defaultSkin != null)
                pc.ApplySkin(defaultSkin.idle, defaultSkin.walk, defaultSkin.jump, defaultSkin.attack, defaultSkin.fall, defaultSkin.guard, defaultSkin.fps);
            return;
        }
        string n = activeCharacter;
        pc.ApplySkin(Resources.Load<Sprite>("Playable/" + n + "/idle_front"), LoadFrames(n, "walk"), LoadFrames(n, "jump"),
                     LoadFrames(n, "attack"), LoadFrames(n, "fall"), Resources.Load<Sprite>("Playable/" + n + "/guard_1"), 8f,
                     // есть кадр fly — персонаж умеет летать (у Айыыны это раскрытые крылья)
                     Resources.Load<Sprite>("Playable/" + n + "/fly") != null, Resources.Load<Sprite>("Playable/" + n + "/fly"),
                     Resources.Load<Sprite>("Playable/" + n + "/glide"));
    }

    void SelectAsPlayer(string name)
    {
        activeCharacter = name;
        PlayerPrefs.SetString(ActiveKey, name);
        PlayerPrefs.Save();
        ApplyActive();
        RefreshCharacterInfo();
        RefreshPartyBar();
    }

    void FillDefaultRoster()
    {
        characters.Add(new CharacterData { name = "Лилия", rarity = 5, element = Element.Пиро, description = "Героиня этого мира. Алая лилия, что расцветает в пламени." });
        characters.Add(new CharacterData { name = "Айрис", rarity = 5, element = Element.Гидро, description = "Странница, умеющая говорить с дождём." });
        characters.Add(new CharacterData { name = "Борей", rarity = 5, element = Element.Крио, description = "Молчаливый страж северных перевалов." });
        characters.Add(new CharacterData { name = "Сайла", rarity = 5, element = Element.Анемо, description = "Бард, чьи песни несёт ветер." });
        characters.Add(new CharacterData { name = "Мира", rarity = 4, element = Element.Электро, description = "Изобретательница с искрой в глазах." });
        characters.Add(new CharacterData { name = "Тэо", rarity = 4, element = Element.Гео, description = "Каменотёс с добрым сердцем." });
        characters.Add(new CharacterData { name = "Нела", rarity = 4, element = Element.Дендро, description = "Травница из лесной деревни." });
        characters.Add(new CharacterData { name = "Кай", rarity = 4, element = Element.Пиро, description = "Вспыльчивый, но верный мечник." });
        characters.Add(new CharacterData { name = "Юна", rarity = 4, element = Element.Гидро, description = "Юная рыбачка из портового города." });
    }

    void Update()
    {
        if (animating) return;
        if (Input.GetKeyDown(KeyCode.F3)) Toggle(wishPanel);
        if (Input.GetKeyDown(KeyCode.C)) Toggle(charPanel);
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (resultPanel.activeSelf) resultPanel.SetActive(false);
            else CloseAll();
        }
    }

    // =================== ЛОГИКА ГАЧИ ===================

    CharacterData RollOne()
    {
        pity5++; pity4++; totalWishes++;
        float chance5 = fiveStarBase;
        if (pity5 >= softPityStart) chance5 += (pity5 - softPityStart + 1) * 0.06f;
        if (pity5 >= hardPity) chance5 = 1f;

        float r = UnityEngine.Random.value;
        if (r < chance5)
        {
            pity5 = 0; pity4 = 0;
            if (allCharactersEqual)
            {
                var all5 = characters.FindAll(c => c.rarity == 5);
                if (all5.Count > 0) return all5[UnityEngine.Random.Range(0, all5.Count)];
            }
            CharacterData featured = characters.Find(c => c.name == featuredCharacter && c.rarity == 5);
            bool win = guaranteed || UnityEngine.Random.value < 0.5f;
            if (featured != null && win) { guaranteed = false; return featured; }
            var pool = characters.FindAll(c => c.rarity == 5 && c != featured);
            if (pool.Count == 0) return featured;
            guaranteed = featured != null; // проиграл 50/50 — следующий гарант
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }
        if (pity4 >= fourStarPity || r < chance5 + fourStarBase)
        {
            pity4 = 0;
            var pool = characters.FindAll(c => c.rarity == 4);
            if (pool.Count > 0) return pool[UnityEngine.Random.Range(0, pool.Count)];
        }
        return null; // 3★ — «оружие»
    }

    void DoWish(int count)
    {
        if (animating) return;
        if (primogems < wishCost * count) { StartCoroutine(FlashText(primoWishText, Color.red)); return; }
        primogems -= wishCost * count;
        var results = new List<CharacterData>();
        for (int i = 0; i < count; i++)
        {
            var c = RollOne();
            results.Add(c);
            if (c != null) owned[c.name] = owned.TryGetValue(c.name, out int n) ? n + 1 : 1;
        }
        Save();
        RefreshTexts();
        StartCoroutine(WishAnimation(results));
    }

    // =================== НАГРАДА ЗА УНИЧТОЖЕНИЕ ОБЪЕКТОВ ===================

    // Вызывается Damageable, когда объект уничтожен: начисляет destroyReward
    // алмазов и показывает всплывающий значок в точке смерти объекта.
    public static void RewardDestroy(Vector2 worldPos)
    {
        if (instance == null) instance = FindObjectOfType<GachaSystem>();
        if (instance == null) return;
        instance.AddGems(instance.destroyReward, worldPos);
    }

    void AddGems(int amount, Vector2 worldPos)
    {
        if (amount <= 0) return;
        primogems += amount;
        Save();
        RefreshTexts();
        SpawnGemPopup(amount, worldPos);
    }

    // «+5» с неогранённым алмазом: всплывает вверх и гаснет
    void SpawnGemPopup(int amount, Vector2 worldPos)
    {
        if (canvas == null) return;
        var go = new GameObject("GemPopup", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        var cam = Camera.main;
        Vector2 sp = cam != null ? (Vector2)cam.WorldToScreenPoint(worldPos) : (Vector2)worldPos;
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform, sp, null, out local))
            rt.anchoredPosition = local;

        var gem = Resources.Load<Sprite>("UI/RoughDiamond");
        if (gem != null)
        {
            var icon = MakePanel(go.transform, "Gem", Color.white);
            icon.raycastTarget = false; icon.sprite = gem; icon.preserveAspect = true;
            CenterIn(icon.rectTransform);
            icon.rectTransform.sizeDelta = new Vector2(44, 44);
            icon.rectTransform.anchoredPosition = new Vector2(-32, 0);
        }
        var label = MakeText(go.transform, "+" + amount, 30, TextAnchor.MiddleLeft, Gold);
        CenterIn(label.rectTransform);
        label.rectTransform.sizeDelta = new Vector2(60, 44);
        label.rectTransform.anchoredPosition = new Vector2(24, 0);
        label.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);

        StartCoroutine(GemPopupAnim(go, rt));
    }

    IEnumerator GemPopupAnim(GameObject go, RectTransform rt)
    {
        var cg = go.AddComponent<CanvasGroup>();
        Vector2 start = rt.anchoredPosition;
        float t = 0;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime;
            float k = t;
            rt.anchoredPosition = start + new Vector2(0f, 90f * k);
            cg.alpha = k < 0.65f ? 1f : 1f - (k - 0.65f) / 0.35f;
            yield return null;
        }
        Destroy(go);
    }

    // =================== АНИМАЦИЯ МОЛИТВЫ ===================

    IEnumerator WishAnimation(List<CharacterData> results)
    {
        animating = true;
        int best = 3;
        foreach (var c in results) if (c != null) best = Mathf.Max(best, c.rarity);
        Color starColor = best == 5 ? Gold : best == 4 ? Purple : Blue;

        // Падающая звезда
        var overlay = MakePanel(canvas.transform, "WishFx", new Color(0.02f, 0.03f, 0.08f, 1f));
        Stretch(overlay.rectTransform);
        var star = MakePanel(overlay.transform, "Star", starColor);
        star.rectTransform.sizeDelta = new Vector2(40, 40);
        star.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        var trail = MakePanel(overlay.transform, "Trail", new Color(starColor.r, starColor.g, starColor.b, 0.35f));
        trail.rectTransform.sizeDelta = new Vector2(600, 8);
        trail.rectTransform.localRotation = Quaternion.Euler(0, 0, -30);

        float t = 0;
        while (t < 1.2f)
        {
            t += Time.unscaledDeltaTime;
            float k = t / 1.2f;
            Vector2 p = Vector2.Lerp(new Vector2(-900, 500), new Vector2(0, 0), k);
            star.rectTransform.anchoredPosition = p;
            trail.rectTransform.anchoredPosition = p + new Vector2(-260, 150);
            yield return null;
        }
        // Вспышка
        overlay.color = starColor; trail.gameObject.SetActive(false); star.gameObject.SetActive(false);
        t = 0;
        while (t < 0.5f) { t += Time.unscaledDeltaTime; overlay.color = Color.Lerp(starColor, Color.white, t / 0.5f); yield return null; }
        Destroy(overlay.gameObject);

        // Показ по одному
        foreach (var c in results)
        {
            if (results.Count > 1 && (c == null)) continue; // в 10-молитве 3★ показываем только в итоге
            yield return ShowSingle(c);
        }
        ShowResults(results);
        animating = false;
    }

    IEnumerator ShowSingle(CharacterData c)
    {
        var root = MakePanel(canvas.transform, "Reveal", new Color(0.95f, 0.93f, 0.88f, 1f));
        Stretch(root.rectTransform);
        Color rc = RarityColor(c);
        var art = MakeCharacterArt(root.transform, c, new Vector2(460, 620));
        art.anchoredPosition = new Vector2(180, 0);
        var name = MakeText(root.transform, c != null ? c.name : "Оружие ★★★", 64, TextAnchor.MiddleLeft, new Color(0.2f, 0.2f, 0.25f));
        name.rectTransform.anchoredPosition = new Vector2(-420, 40); name.rectTransform.sizeDelta = new Vector2(600, 90);
        var stars = MakeText(root.transform, Stars(c != null ? c.rarity : 3), 48, TextAnchor.MiddleLeft, rc);
        stars.rectTransform.anchoredPosition = new Vector2(-420, -30); stars.rectTransform.sizeDelta = new Vector2(600, 60);
        if (c != null)
        {
            var el = MakeText(root.transform, c.element.ToString() + (owned[c.name] > 1 ? "   •   Созвездие +1" : "   •   НОВЫЙ!"), 28, TextAnchor.MiddleLeft, ElementColor(c.element));
            el.rectTransform.anchoredPosition = new Vector2(-420, -85); el.rectTransform.sizeDelta = new Vector2(600, 40);
        }
        var hint = MakeText(root.transform, "Нажмите, чтобы продолжить", 22, TextAnchor.MiddleCenter, new Color(0.4f, 0.4f, 0.45f));
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0);
        hint.rectTransform.anchoredPosition = new Vector2(0, 50); hint.rectTransform.sizeDelta = new Vector2(600, 40);

        // выезд
        float t = 0;
        while (t < 0.35f) { t += Time.unscaledDeltaTime; art.anchoredPosition = new Vector2(Mathf.Lerp(700, 180, t / 0.35f), 0); yield return null; }
        yield return null;
        while (!Input.GetMouseButtonDown(0) && !Input.anyKeyDown) yield return null;
        Destroy(root.gameObject);
        yield return null;
    }

    void ShowResults(List<CharacterData> results)
    {
        foreach (Transform ch in resultPanel.transform) if (ch.name == "Cards") Destroy(ch.gameObject);
        var cards = new GameObject("Cards", typeof(RectTransform)).GetComponent<RectTransform>();
        cards.SetParent(resultPanel.transform, false);
        cards.sizeDelta = new Vector2(1400, 520);
        var hl = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 12; hl.childAlignment = TextAnchor.MiddleCenter; hl.childControlWidth = hl.childControlHeight = false;

        var sorted = new List<CharacterData>(results);
        sorted.Sort((a, b) => (b != null ? b.rarity : 3).CompareTo(a != null ? a.rarity : 3));
        foreach (var c in sorted)
        {
            var card = MakePanel(cards, "Card", RarityColor(c) * 0.55f + new Color(0, 0, 0, 0.45f));
            card.rectTransform.sizeDelta = new Vector2(120, 480);
            var art = MakeCharacterArt(card.transform, c, new Vector2(110, 360));
            art.anchoredPosition = new Vector2(0, 30);
            var st = MakeText(card.transform, Stars(c != null ? c.rarity : 3), 20, TextAnchor.MiddleCenter, RarityColor(c));
            st.rectTransform.anchoredPosition = new Vector2(0, -175); st.rectTransform.sizeDelta = new Vector2(120, 30);
            var nm = MakeText(card.transform, c != null ? c.name : "Оружие", 20, TextAnchor.MiddleCenter, Color.white);
            nm.rectTransform.anchoredPosition = new Vector2(0, -210); nm.rectTransform.sizeDelta = new Vector2(120, 30);
        }
        resultPanel.SetActive(true);
        resultPanel.transform.SetAsLastSibling();
    }

    // =================== ПОСТРОЕНИЕ UI ===================

    void BuildUI()
    {
        if (FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var cgo = new GameObject("GachaCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        BuildHud();
        BuildWishPanel();
        BuildCharacterPanel();
        BuildResultPanel();
        RefreshTexts();
    }

    // Круглые иконки в правом верхнем углу, как в Genshin
    void BuildHud()
    {
        hud = new GameObject("HUD", typeof(RectTransform));
        hud.transform.SetParent(canvas.transform, false);
        Stretch(hud.GetComponent<RectTransform>());

        var bar = MakePanel(hud.transform, "TopRight", new Color(0, 0, 0, 0));
        bar.raycastTarget = false;
        var rt = bar.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-30, -25);
        rt.sizeDelta = new Vector2(600, 110);
        var hl = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 18; hl.childAlignment = TextAnchor.UpperRight; hl.childControlWidth = hl.childControlHeight = false;

        MakeHudIcon(bar.transform, "✦", "Молитва", "F3", Gold, () => Toggle(wishPanel));
        MakeHudIcon(bar.transform, "♟", "Персонажи", "C", new Color(0.6f, 0.85f, 1f), () => Toggle(charPanel));

        // Счётчик примогемов
        var primo = MakePanel(hud.transform, "Primo", new Color(0, 0, 0, 0.45f));
        var prt = primo.rectTransform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1, 1);
        prt.anchoredPosition = new Vector2(-30, -140); prt.sizeDelta = new Vector2(220, 40);
        primoHudText = MakeText(primo.transform, "", 24, TextAnchor.MiddleCenter, Color.white);
        Stretch(primoHudText.rectTransform);

        // значок неогранённого алмаза слева от счётчика
        var gemSp = Resources.Load<Sprite>("UI/RoughDiamond");
        if (gemSp != null)
        {
            hudGemIcon = true;
            var icon = MakePanel(primo.transform, "Gem", Color.white);
            icon.raycastTarget = false; icon.sprite = gemSp; icon.preserveAspect = true;
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(5, 0); irt.sizeDelta = new Vector2(30, 30);
            var trt = primoHudText.rectTransform;
            trt.offsetMin = new Vector2(40, trt.offsetMin.y);
        }

        BuildPartyBar();
    }

    void MakeHudIcon(Transform parent, string glyph, string label, string key, Color accent, Action onClick)
    {
        var holder = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
        holder.SetParent(parent, false); holder.sizeDelta = new Vector2(90, 110);

        var circle = MakePanel(holder, "Circle", new Color(0.12f, 0.13f, 0.2f, 0.75f));
        circle.sprite = CircleSprite();
        circle.rectTransform.anchoredPosition = new Vector2(0, 12); circle.rectTransform.sizeDelta = new Vector2(76, 76);
        var ring = MakePanel(circle.transform, "Ring", new Color(accent.r, accent.g, accent.b, 0.8f));
        ring.sprite = RingSprite(); ring.raycastTarget = false; Stretch(ring.rectTransform);
        var g = MakeText(circle.transform, glyph, 38, TextAnchor.MiddleCenter, accent); Stretch(g.rectTransform);
        var k = MakeText(circle.transform, key, 14, TextAnchor.MiddleCenter, Color.white);
        k.rectTransform.anchoredPosition = new Vector2(28, -28); k.rectTransform.sizeDelta = new Vector2(30, 20);
        var l = MakeText(holder, label, 18, TextAnchor.MiddleCenter, Color.white);
        l.rectTransform.anchoredPosition = new Vector2(0, -45); l.rectTransform.sizeDelta = new Vector2(120, 24);
        l.gameObject.AddComponent<Shadow>();

        var btn = circle.gameObject.AddComponent<Button>();
        btn.targetGraphic = circle;
        var cb = btn.colors; cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f); btn.colors = cb;
        btn.onClick.AddListener(() => onClick());
    }

    // Столбец кружков-персонажей у правого края экрана — быстрый выбор партии, как в Genshin.
    void BuildPartyBar()
    {
        // все играбельные персонажи; основная героиня — первым слотом
        party.Clear();
        foreach (var c in characters) if (IsPlayable(c.name)) party.Add(c);
        var mh = party.Find(c => c.name == mainHeroine);
        if (mh != null) { party.Remove(mh); party.Insert(0, mh); }

        var bar = new GameObject("PartyBar", typeof(RectTransform)).GetComponent<RectTransform>();
        bar.SetParent(hud.transform, false);
        bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(1, 0.5f);
        bar.anchoredPosition = new Vector2(-40, -10);
        bar.sizeDelta = new Vector2(120, 340);
        var vl = bar.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 16; vl.childAlignment = TextAnchor.MiddleCenter;
        vl.childControlWidth = vl.childControlHeight = false;
        vl.childForceExpandWidth = vl.childForceExpandHeight = false;

        partyCell = new Image[party.Count];
        partyRing = new Image[party.Count];
        partyGlow = new Image[party.Count];

        for (int i = 0; i < party.Count; i++)
        {
            var c = party[i];
            int idx = i;

            // держатель ячейки (его задаёт layout, сам он картинку не рисует)
            var holder = new GameObject(c.name + "Slot", typeof(RectTransform));
            holder.transform.SetParent(bar, false);
            CenterIn(holder.GetComponent<RectTransform>());
            holder.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 100);

            // кружок с маской — портрет кадрируется по кругу, лицо по центру
            var cell = MakePanel(holder.transform, c.name, new Color(0.07f, 0.08f, 0.12f, 0.92f));
            cell.sprite = CircleSprite();
            CenterIn(cell.rectTransform);
            cell.rectTransform.sizeDelta = new Vector2(92, 92);
            cell.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            if (c.portrait != null) AddPartyPortrait(cell, c);
            else
            {
                cell.color = ElementColor(c.element) * 0.8f;
                var lt = MakeText(cell.transform, c.name.Substring(0, 1), 44, TextAnchor.MiddleCenter, Color.white);
                Stretch(lt.rectTransform);
            }

            // обводка: активный персонаж — золотая, остальные — цвета стихии
            var ring = MakePanel(cell.transform, "Ring", ElementColor(c.element));
            ring.sprite = RingSprite(); ring.raycastTarget = false; Stretch(ring.rectTransform);

            // золотое свечение активного персонажа (вне маски, поверх кружка)
            var glow = MakePanel(holder.transform, "Glow", new Color(1f, 0.85f, 0.5f, 0.95f));
            glow.sprite = RingSprite(); glow.raycastTarget = false;
            CenterIn(glow.rectTransform);
            glow.rectTransform.sizeDelta = new Vector2(106, 106);

            partyCell[i] = cell; partyRing[i] = ring; partyGlow[i] = glow;

            var btn = cell.gameObject.AddComponent<Button>();
            btn.targetGraphic = cell;
            btn.onClick.AddListener(() => SelectAsPlayer(party[idx].name));
        }
        RefreshPartyBar();
    }

    // Кадрирование портрета в кружке: квадрат 45% длинной стороны портрета,
    // центр — на лице (примерно 16% высоты от верха; для всех портретов в папке сходится).
    void AddPartyPortrait(Image cell, CharacterData c)
    {
        float nw = c.portrait.rect.width, nh = c.portrait.rect.height;
        float side = 0.45f * Mathf.Max(nw, nh);
        float cx = Mathf.Clamp(nw * 0.5f, side * 0.5f, nw - side * 0.5f);
        float cy = Mathf.Clamp(nh * 0.16f, side * 0.5f, nh - side * 0.5f);
        float k = cell.rectTransform.sizeDelta.x / side;
        var p = MakePanel(cell.transform, "Portrait", Color.white);
        p.raycastTarget = false; p.sprite = c.portrait; p.preserveAspect = false;
        var rt = p.rectTransform;
        CenterIn(rt);
        rt.sizeDelta = new Vector2(nw * k, nh * k);
        rt.anchoredPosition = new Vector2((nw * 0.5f - cx) * k, (cy - nh * 0.5f) * k);
    }

    void RefreshPartyBar()
    {
        if (partyCell == null) return;
        for (int i = 0; i < partyCell.Length && i < party.Count; i++)
        {
            bool active = !string.IsNullOrEmpty(activeCharacter) && party[i].name == activeCharacter;
            partyRing[i].color = active ? Gold : ElementColor(party[i].element);
            partyGlow[i].gameObject.SetActive(active);
        }
    }

    void BuildWishPanel()
    {
        var bg = MakePanel(canvas.transform, "WishPanel", new Color(0.05f, 0.07f, 0.15f, 0.97f));
        wishPanel = bg.gameObject; Stretch(bg.rectTransform);

        // Заголовок-вкладка баннера
        var title = MakeText(bg.transform, "✦  Молитва", 34, TextAnchor.MiddleLeft, Cream);
        TopLeft(title.rectTransform, new Vector2(60, -40), new Vector2(400, 50));

        var tab = MakePanel(bg.transform, "BannerTab", Gold);
        tab.rectTransform.anchorMin = tab.rectTransform.anchorMax = new Vector2(0.5f, 1);
        tab.rectTransform.anchoredPosition = new Vector2(0, -60); tab.rectTransform.sizeDelta = new Vector2(160, 70);
        var tt = MakeText(tab.transform, "★ Ивент", 22, TextAnchor.MiddleCenter, new Color(0.2f, 0.15f, 0.1f)); Stretch(tt.rectTransform);

        primoWishText = MakeText(bg.transform, "", 26, TextAnchor.MiddleRight, Color.white);
        var pr = primoWishText.rectTransform; pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(1, 1);
        pr.anchoredPosition = new Vector2(-130, -40); pr.sizeDelta = new Vector2(400, 50);
        MakeCloseButton(bg.transform, () => wishPanel.SetActive(false));

        // Баннер
        var banner = MakePanel(bg.transform, "Banner", new Color(0.98f, 0.94f, 0.86f));
        banner.rectTransform.anchoredPosition = new Vector2(0, 30); banner.rectTransform.sizeDelta = new Vector2(1300, 640);
        var stripe = MakePanel(banner.transform, "Stripe", new Color(0.85f, 0.35f, 0.25f, 0.9f));
        stripe.rectTransform.anchorMin = new Vector2(0, 0); stripe.rectTransform.anchorMax = new Vector2(0.42f, 1);
        stripe.rectTransform.offsetMin = stripe.rectTransform.offsetMax = Vector2.zero;

        var feat = characters.Find(c => c.name == featuredCharacter);
        var bt = MakeText(banner.transform, allCharactersEqual ? "Зов небес Олонхо" : "Цветение алой лилии", 46, TextAnchor.UpperLeft, Color.white);
        TopLeft(bt.rectTransform, new Vector2(40, -40), new Vector2(520, 130));
        bt.gameObject.AddComponent<Shadow>();
        var bd = MakeText(banner.transform, allCharactersEqual ?
            "Все легендарные персонажи выпадают\nс одинаковым шансом!\n\n" +
            "• Каждые 10 молитв — гарантированно 4★ или выше\n• Не более 90 молитв до 5★\n• Повторный персонаж открывает созвездие" :
            "Вероятность выпадения 5★ персонажа\n«" + featuredCharacter + "» значительно увеличена!\n\n" +
            "• Каждые 10 молитв — гарантированно 4★ или выше\n• Не более 90 молитв до 5★\n• Проиграли 50/50 — следующий 5★ гарантированно персонаж баннера",
            20, TextAnchor.UpperLeft, Color.white);
        TopLeft(bd.rectTransform, new Vector2(40, -180), new Vector2(480, 300));

        if (allCharactersEqual)
        {
            // все 5★ в ряд на правой части баннера
            var five = characters.FindAll(c => c.rarity == 5);
            float areaW = 740f, x0 = -95f; // правая часть баннера: от края красной полосы до правого края
            float w = five.Count > 0 ? areaW / five.Count : areaW;
            for (int i = 0; i < five.Count; i++)
            {
                var a = MakeCharacterArt(banner.transform, five[i], new Vector2(w - 8, 560));
                a.anchoredPosition = new Vector2(x0 + w * (i + 0.5f), 20);
                a.GetComponent<Image>().preserveAspect = five[i].portrait != null;
                var nm = MakeText(banner.transform, five[i].name, 22, TextAnchor.MiddleCenter, Gold);
                nm.rectTransform.anchoredPosition = new Vector2(x0 + w * (i + 0.5f), -290);
                nm.rectTransform.sizeDelta = new Vector2(w, 34);
                nm.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
            }
        }
        else
        {
        var art = MakeCharacterArt(banner.transform, feat, new Vector2(460, 600));
        art.anchoredPosition = new Vector2(280, 0);
        var fn = MakeText(banner.transform, featuredCharacter + "\n" + Stars(5), 36, TextAnchor.LowerRight, Gold);
        fn.rectTransform.anchoredPosition = new Vector2(460, -230); fn.rectTransform.sizeDelta = new Vector2(300, 110);
        fn.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
        }

        pityText = MakeText(bg.transform, "", 20, TextAnchor.MiddleLeft, new Color(0.8f, 0.8f, 0.85f));
        var pt = pityText.rectTransform; pt.anchorMin = pt.anchorMax = pt.pivot = new Vector2(0, 0);
        pt.anchoredPosition = new Vector2(60, 60); pt.sizeDelta = new Vector2(600, 60);

        MakeWishButton(bg.transform, "Молитва ×1", new Vector2(-400, 60), () => DoWish(1), 1);
        MakeWishButton(bg.transform, "Молитва ×10", new Vector2(-80, 60), () => DoWish(10), 10);

        wishPanel.SetActive(false);
    }

    void MakeWishButton(Transform parent, string label, Vector2 pos, Action onClick, int count)
    {
        var b = MakePanel(parent, label, Cream);
        b.sprite = RoundedSprite();  b.type = Image.Type.Sliced;
        var rt = b.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 0);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(290, 90);
        var t = MakeText(b.transform, label + "\n◆ " + (wishCost * count) + " алмазов", 24, TextAnchor.MiddleCenter, new Color(0.3f, 0.3f, 0.35f));
        Stretch(t.rectTransform);
        var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
        btn.onClick.AddListener(() => onClick());
    }

    void BuildCharacterPanel()
    {
        var bg = MakePanel(canvas.transform, "CharacterPanel", new Color(0.13f, 0.14f, 0.2f, 0.98f));
        charPanel = bg.gameObject; Stretch(bg.rectTransform);

        var title = MakeText(bg.transform, "♟  Персонажи", 34, TextAnchor.MiddleLeft, Cream);
        TopLeft(title.rectTransform, new Vector2(60, -40), new Vector2(400, 50));
        MakeCloseButton(bg.transform, () => charPanel.SetActive(false));

        // Верхняя полоса аватаров (как список отряда в Genshin)
        var strip = MakePanel(bg.transform, "Avatars", new Color(0, 0, 0, 0.25f));
        var sr = strip.rectTransform; sr.anchorMin = new Vector2(0, 1); sr.anchorMax = new Vector2(1, 1); sr.pivot = new Vector2(0.5f, 1);
        sr.anchoredPosition = new Vector2(0, -100); sr.sizeDelta = new Vector2(0, 120);
        var scroll = strip.gameObject.AddComponent<ScrollRect>();
        scroll.vertical = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        strip.gameObject.AddComponent<RectMask2D>();
        charListContent = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        charListContent.SetParent(strip.transform, false);
        charListContent.anchorMin = new Vector2(0, 0); charListContent.anchorMax = new Vector2(0, 1); charListContent.pivot = new Vector2(0, 0.5f);
        charListContent.anchoredPosition = new Vector2(60, 0);
        var hl = charListContent.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 14; hl.childAlignment = TextAnchor.MiddleLeft; hl.childControlWidth = hl.childControlHeight = false;
        charListContent.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = charListContent;

        // Левое меню вкладок (как в Genshin: Атрибуты / Созвездия / Профиль)
        var tabs = new GameObject("Tabs", typeof(RectTransform)).GetComponent<RectTransform>();
        tabs.SetParent(bg.transform, false);
        tabs.anchorMin = tabs.anchorMax = tabs.pivot = new Vector2(0, 0.5f);
        tabs.anchoredPosition = new Vector2(40, -60); tabs.sizeDelta = new Vector2(90, 300);
        var vl = tabs.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 16; vl.childControlWidth = vl.childControlHeight = false; vl.childAlignment = TextAnchor.MiddleCenter;
        MakeTab(tabs, "Атрибуты", "◈");
        MakeTab(tabs, "Созвездия", "✧");
        MakeTab(tabs, "Профиль", "☰");

        // Большой портрет по центру
        var portraitHolder = new GameObject("Portrait", typeof(RectTransform)).GetComponent<RectTransform>();
        portraitHolder.SetParent(bg.transform, false);
        portraitHolder.anchoredPosition = new Vector2(-200, -70); portraitHolder.sizeDelta = new Vector2(560, 780);
        bigPortrait = MakePanel(portraitHolder, "Art", Color.white); Stretch(bigPortrait.rectTransform);
        bigPortrait.preserveAspect = true;
        bigPortraitLetter = MakeText(portraitHolder, "", 260, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.85f));
        Stretch(bigPortraitLetter.rectTransform);

        // Правая панель информации
        var info = MakePanel(bg.transform, "Info", new Color(0, 0, 0, 0.35f));
        var ir = info.rectTransform; ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(1, 0.5f);
        ir.anchoredPosition = new Vector2(-60, -70); ir.sizeDelta = new Vector2(560, 760);
        infoName = MakeText(info.transform, "", 50, TextAnchor.MiddleLeft, Color.white);
        TopLeft(infoName.rectTransform, new Vector2(30, -30), new Vector2(500, 70));
        infoElement = MakeText(info.transform, "", 26, TextAnchor.MiddleLeft, Color.white);
        TopLeft(infoElement.rectTransform, new Vector2(30, -100), new Vector2(500, 40));
        infoStars = MakeText(info.transform, "", 34, TextAnchor.MiddleLeft, Gold);
        TopLeft(infoStars.rectTransform, new Vector2(30, -140), new Vector2(500, 46));
        infoDesc = MakeText(info.transform, "", 24, TextAnchor.UpperLeft, Cream);
        TopLeft(infoDesc.rectTransform, new Vector2(30, -210), new Vector2(500, 330));
        infoConst = MakeText(info.transform, "", 24, TextAnchor.MiddleLeft, Cream);
        TopLeft(infoConst.rectTransform, new Vector2(30, -560), new Vector2(500, 40));
        for (int i = 0; i < 6; i++)
        {
            var n = MakePanel(info.transform, "C" + (i + 1), Color.gray);
            n.sprite = CircleSprite();
            TopLeft(n.rectTransform, new Vector2(30 + i * 82, -620), new Vector2(64, 64));
            var nt = MakeText(n.transform, "C" + (i + 1), 20, TextAnchor.MiddleCenter, Color.white); Stretch(nt.rectTransform);
            constNodes[i] = n;
        }

        // Кнопка «Играть» — как «Сменить» в меню персонажа Genshin
        var pb = MakePanel(info.transform, "PlayButton", Gold);
        pb.sprite = RoundedSprite(); pb.type = Image.Type.Sliced;
        var pbr = pb.rectTransform; pbr.anchorMin = pbr.anchorMax = pbr.pivot = new Vector2(1, 0);
        pbr.anchoredPosition = new Vector2(-30, 30); pbr.sizeDelta = new Vector2(240, 70);
        playButtonText = MakeText(pb.transform, "Играть", 28, TextAnchor.MiddleCenter, new Color(0.25f, 0.2f, 0.1f));
        Stretch(playButtonText.rectTransform);
        playButton = pb.gameObject.AddComponent<Button>(); playButton.targetGraphic = pb;
        playButton.onClick.AddListener(() =>
        {
            var l = OwnedSorted();
            if (selectedChar >= 0 && selectedChar < l.Count && IsPlayable(l[selectedChar].name))
                SelectAsPlayer(l[selectedChar].name);
        });

        charPanel.SetActive(false);
    }

    void MakeTab(Transform parent, string name, string glyph)
    {
        var c = MakePanel(parent, name, new Color(1, 1, 1, 0.12f));
        c.sprite = CircleSprite(); c.rectTransform.sizeDelta = new Vector2(76, 76);
        var g = MakeText(c.transform, glyph, 34, TextAnchor.MiddleCenter, Color.white); Stretch(g.rectTransform);
        var btn = c.gameObject.AddComponent<Button>(); btn.targetGraphic = c;
        btn.onClick.AddListener(() => { charTab = name; RefreshCharacterInfo(); });
        tabButtons[name] = c;
    }

    void BuildResultPanel()
    {
        var bg = MakePanel(canvas.transform, "Results", new Color(0.05f, 0.06f, 0.12f, 0.98f));
        resultPanel = bg.gameObject; Stretch(bg.rectTransform);
        var hint = MakeText(bg.transform, "Нажмите ✕ или Esc, чтобы вернуться", 22, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.75f));
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0);
        hint.rectTransform.anchoredPosition = new Vector2(0, 60); hint.rectTransform.sizeDelta = new Vector2(800, 40);
        MakeCloseButton(bg.transform, () => resultPanel.SetActive(false));
        resultPanel.SetActive(false);
    }

    void MakeCloseButton(Transform parent, Action onClick)
    {
        var b = MakePanel(parent, "Close", Cream);
        b.sprite = CircleSprite();
        var rt = b.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-40, -35); rt.sizeDelta = new Vector2(60, 60);
        var t = MakeText(b.transform, "✕", 30, TextAnchor.MiddleCenter, new Color(0.25f, 0.25f, 0.3f)); Stretch(t.rectTransform);
        var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
        btn.onClick.AddListener(() => { onClick(); UpdatePause(); });
    }

    // =================== ОБНОВЛЕНИЕ ===================

    void Toggle(GameObject panel)
    {
        bool open = !panel.activeSelf;
        CloseAll();
        panel.SetActive(open);
        if (open && panel == charPanel) RebuildCharacterList();
        RefreshTexts();
        UpdatePause();
    }

    void CloseAll()
    {
        wishPanel.SetActive(false); charPanel.SetActive(false); resultPanel.SetActive(false);
        UpdatePause();
    }

    void UpdatePause()
    {
        bool any = wishPanel.activeSelf || charPanel.activeSelf || resultPanel.activeSelf || animating;
        IsMenuOpen = any;
        if (!pauseWhileOpen) return;
        Time.timeScale = any ? 0f : 1f;
        hud.SetActive(!any);
    }

    void RefreshTexts()
    {
        string p = primogems.ToString("N0");
        primoHudText.text = hudGemIcon ? p : "◆ " + p; // в HUD значок рисуется картинкой
        primoWishText.text = "Алмазы: ◆ " + p;
        pityText.text = "Молитв до гаранта 5★: " + (hardPity - pity5) + "    |    Всего молитв: " + totalWishes +
                        (guaranteed && !allCharactersEqual ? "\nСледующий 5★ — гарантированно " + featuredCharacter : "");
    }

    void RebuildCharacterList()
    {
        foreach (Transform ch in charListContent) Destroy(ch.gameObject);
        var list = OwnedSorted();
        if (list.Count == 0) { selectedChar = -1; RefreshCharacterInfo(); return; }
        if (selectedChar < 0 || selectedChar >= list.Count) selectedChar = 0;

        for (int i = 0; i < list.Count; i++)
        {
            int idx = i; var c = list[i];
            var frame = MakePanel(charListContent, c.name, idx == selectedChar ? Cream : RarityColor(c) * 0.7f);
            frame.sprite = CircleSprite(); frame.rectTransform.sizeDelta = new Vector2(96, 96);
            var inner = MakePanel(frame.transform, "Inner", ElementColor(c.element) * 0.8f);
            inner.sprite = CircleSprite(); inner.raycastTarget = false;
            inner.rectTransform.sizeDelta = new Vector2(84, 84);
            if (c.portrait != null) { inner.sprite = c.portrait; inner.color = Color.white; inner.preserveAspect = true; }
            else { var l = MakeText(inner.transform, c.name.Substring(0, 1), 40, TextAnchor.MiddleCenter, Color.white); Stretch(l.rectTransform); }
            var btn = frame.gameObject.AddComponent<Button>(); btn.targetGraphic = frame;
            btn.onClick.AddListener(() => { selectedChar = idx; RebuildCharacterList(); });
        }
        RefreshCharacterInfo();
    }

    void RefreshCharacterInfo()
    {
        foreach (var kv in tabButtons) kv.Value.color = kv.Key == charTab ? Cream : new Color(1, 1, 1, 0.12f);
        var list = OwnedSorted();
        bool has = selectedChar >= 0 && selectedChar < list.Count;
        foreach (var n in constNodes) n.gameObject.SetActive(has && charTab == "Созвездия");
        infoConst.gameObject.SetActive(has && charTab == "Созвездия");
        if (playButton != null) playButton.gameObject.SetActive(false);
        if (!has)
        {
            infoName.text = "Пока пусто"; infoElement.text = ""; infoStars.text = "";
            infoDesc.text = "Совершите молитву (F3), чтобы получить первых персонажей.";
            bigPortrait.color = new Color(1, 1, 1, 0.05f); bigPortrait.sprite = null; bigPortraitLetter.text = "?";
            return;
        }
        var c = list[selectedChar];
        int copies = owned[c.name]; int cons = Mathf.Min(copies - 1, 6);
        infoName.text = c.name;
        if (playButton != null)
        {
            playButton.gameObject.SetActive(true);
            bool playable = IsPlayable(c.name), active = c.name == activeCharacter;
            playButton.interactable = playable && !active;
            playButtonText.text = active ? "✓ В игре" : playable ? "Играть" : "Скоро";
        }
        infoElement.text = "◆ " + c.element; infoElement.color = ElementColor(c.element);
        infoStars.text = Stars(c.rarity); infoStars.color = RarityColor(c);
        if (charTab == "Атрибуты")
            infoDesc.text = "Уровень  90/90\n\nСозвездие:  C" + cons + "\nКопий получено:  " + copies + "\nРедкость:  " + c.rarity + "★";
        else if (charTab == "Созвездия")
            infoDesc.text = "Каждая повторная копия персонажа открывает новое созвездие (максимум C6).";
        else
            infoDesc.text = string.IsNullOrEmpty(c.description) ? "Нет описания." : c.description;
        infoConst.text = "Открыто созвездий: " + cons + " / 6";
        for (int i = 0; i < 6; i++) constNodes[i].color = i < cons ? ElementColor(c.element) : new Color(0.3f, 0.3f, 0.35f);

        if (c.portrait != null) { bigPortrait.sprite = c.portrait; bigPortrait.color = Color.white; bigPortraitLetter.text = ""; }
        else { bigPortrait.sprite = RoundedSprite(); bigPortrait.type = Image.Type.Sliced; bigPortrait.color = ElementColor(c.element) * 0.7f + new Color(0, 0, 0, 0.3f); bigPortraitLetter.text = c.name.Substring(0, 1); }
    }

    List<CharacterData> OwnedSorted()
    {
        var list = characters.FindAll(c => owned.ContainsKey(c.name));
        list.Sort((a, b) => a.rarity != b.rarity ? b.rarity.CompareTo(a.rarity) : string.Compare(a.name, b.name, StringComparison.Ordinal));
        return list;
    }

    IEnumerator FlashText(Text t, Color c)
    {
        Color orig = t.color; t.color = c;
        yield return new WaitForSecondsRealtime(0.4f);
        t.color = orig;
    }

    // =================== СОХРАНЕНИЕ ===================

    void Save()
    {
        var d = new SaveData { primogems = primogems, pity5 = pity5, pity4 = pity4, totalWishes = totalWishes, guaranteed = guaranteed, version = 2 };
        foreach (var kv in owned) { d.names.Add(kv.Key); d.counts.Add(kv.Value); }
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(d));
        PlayerPrefs.Save();
    }

    void Load()
    {
        owned.Clear();
        owned[mainHeroine] = 1; // основная героиня есть всегда, как Путешественник в Genshin
        if (!PlayerPrefs.HasKey(SaveKey)) { primogems = startPrimogems; return; }
        var d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
        primogems = d.primogems; pity5 = d.pity5; pity4 = d.pity4; totalWishes = d.totalWishes; guaranteed = d.guaranteed;
        // старое сохранение (до алмазов) — выдаём стартовый запас
        if (d.version < 2) { primogems += startPrimogems; Save(); }
        for (int i = 0; i < d.names.Count && i < d.counts.Count; i++) owned[d.names[i]] = d.counts[i];
        if (!owned.ContainsKey(mainHeroine)) owned[mainHeroine] = 1;
    }

    [ContextMenu("Сбросить сохранение гачи")]
    public void ResetSave()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        Load(); RefreshTexts();
    }

    [ContextMenu("Добавить 1 000 000 алмазов")]
    public void AddPrimogems() { primogems += 1000000; Save(); RefreshTexts(); }

    // =================== ХЕЛПЕРЫ UI ===================

    Image MakePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.color = color;
        return img;
    }

    Text MakeText(Transform parent, string text, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font; t.text = text; t.fontSize = size; t.alignment = anchor; t.color = color;
        t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    RectTransform MakeCharacterArt(Transform parent, CharacterData c, Vector2 size)
    {
        var img = MakePanel(parent, "Art", Color.white);
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = size;
        if (c != null && c.portrait != null) { img.sprite = c.portrait; img.preserveAspect = true; }
        else
        {
            img.sprite = RoundedSprite(); img.type = Image.Type.Sliced;
            img.color = c != null ? ElementColor(c.element) * 0.75f + new Color(0, 0, 0, 0.25f) : new Color(0.45f, 0.55f, 0.7f);
            var l = MakeText(img.transform, c != null ? c.name.Substring(0, 1) : "⚔", (int)(size.x * 0.45f), TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.9f));
            Stretch(l.rectTransform);
        }
        return img.rectTransform;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // Якоря в центре родителя, без смещения — для элементов внутри layout-контейнеров
    static void CenterIn(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    static void TopLeft(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static string Stars(int n) { return new string('★', n); }

    static Color RarityColor(CharacterData c)
    {
        int r = c != null ? c.rarity : 3;
        return r == 5 ? Gold : r == 4 ? Purple : Blue;
    }

    static Color ElementColor(Element e)
    {
        switch (e)
        {
            case Element.Пиро: return new Color(1f, 0.45f, 0.3f);
            case Element.Гидро: return new Color(0.25f, 0.65f, 1f);
            case Element.Анемо: return new Color(0.45f, 0.9f, 0.75f);
            case Element.Электро: return new Color(0.75f, 0.5f, 1f);
            case Element.Дендро: return new Color(0.6f, 0.85f, 0.2f);
            case Element.Крио: return new Color(0.6f, 0.9f, 1f);
            default: return new Color(0.95f, 0.75f, 0.3f);
        }
    }

    // Процедурные спрайты (без внешних ассетов)
    static Sprite circle, ring, rounded;

    static Sprite CircleSprite()
    {
        if (circle == null) circle = ProceduralSprite(128, (x, y, s) => Vector2.Distance(new Vector2(x, y), new Vector2(s / 2f, s / 2f)) <= s / 2f - 1 ? 1f : 0f, 0);
        return circle;
    }

    static Sprite RingSprite()
    {
        if (ring == null) ring = ProceduralSprite(128, (x, y, s) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(s / 2f, s / 2f));
            return d <= s / 2f - 1 && d >= s / 2f - 6 ? 1f : 0f;
        }, 0);
        return ring;
    }

    static Sprite RoundedSprite()
    {
        if (rounded == null) rounded = ProceduralSprite(64, (x, y, s) =>
        {
            float r = 20f;
            float cx = Mathf.Clamp(x, r, s - r), cy = Mathf.Clamp(y, r, s - r);
            return Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) <= r ? 1f : 0f;
        }, 22);
        return rounded;
    }

    static Sprite ProceduralSprite(int size, Func<int, int, int, float> alpha, int border)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color(1, 1, 1, alpha(x, y, size));
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }
}
