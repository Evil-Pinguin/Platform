using UnityEngine;

// Ветер вокруг героини. Всё собирается кодом прямо в Play: текстура
// частиц рисуется на лету, материал — встроенный спрайтовый. Никаких
// ассетов и никаких ручных правок в сцене: повесил скрипт — работает.
//
// Что выдаёт: порыв на рывке, след за лентой, взмах у ножа, пыль под
// ногами при приземлении, облачко на прыжке и всплеск в точке попадания.
public class WindFx : MonoBehaviour
{
    [Tooltip("Пыль под ногами при приземлении, частиц")]
    public int landBurst = 14;
    [Tooltip("Облачко на прыжке, частиц")]
    public int jumpBurst = 9;
    [Tooltip("Порыв на старте рывка, частиц")]
    public int dashBurst = 26;
    [Tooltip("Всплеск в точке попадания, частиц")]
    public int hitBurst = 16;
    [Tooltip("Всплеск при применении способности, частиц")]
    public int abilityBurst = 30;

    [Header("Цвета ветра")]
    public Color windColor = new Color(0.85f, 0.93f, 1f, 0.9f);
    public Color dustColor = new Color(0.82f, 0.78f, 0.7f, 0.8f);

    ParticleSystem gust;    // порывы вокруг неё
    ParticleSystem trail;   // то, что сыпется на пути рывка
    ParticleSystem burst;   // облачка у самой земли
    ParticleSystem hit;     // всплеск в точке попадания
    PlayerController pc;
    Material mat;

    static Texture2D dot;   // одна на всех, текстуры не плодим
    static Material shared;
    static bool warned;

    void Awake()
    {
        pc = GetComponentInParent<PlayerController>();
        mat = SharedMaterial();

        gust = Build("Gust", windColor, 0.22f, 0.22f, 3f);
        trail = Build("Trail", windColor, 0.15f, 0.45f, 2f);
        burst = Build("Burst", dustColor, 0.28f, 0.28f, 2f);
        hit = Build("Hit", windColor, 0.18f, 0.4f, 4f);

        var shape = hit.shape;                 // попадания расходятся сферой
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;
        hit.GetComponent<ParticleSystemRenderer>().sortingOrder = 4f;
    }

    void Update()
    {
        // След подмешивается к рывку: летит — сыпет, стоит — молчит.
        // Время шкалы подкручиваем назад, иначе после остановки сыплется
        // ещё целую секунду.
        float rate = pc != null && pc.DashActive ? 60f : 0f;
        trail.emission.rateOverTime = rate;
        if (rate > 0f && trail.time > 0.05f)
            trail.time = 0f;
    }

    // --- вызовы из контроллера -------------------------------------------------

    public void Dash(float direction)
    {
        gust.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        gust.transform.localRotation = Quaternion.Euler(0f, 0f, direction > 0f ? 0f : 180f);
        Emit(gust, dashBurst);
    }

    public void Jump()
    {
        burst.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        Emit(burst, jumpBurst);
    }

    public void Land()
    {
        burst.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        Emit(burst, landBurst);
    }

    public void Ability()
    {
        gust.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        gust.transform.localRotation = Quaternion.identity;
        Emit(gust, abilityBurst);
    }

    public void Hit(Vector2 point, float dir = 1f)
    {
        hit.transform.position = point;
        var vel = hit.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(
            AnimationCurve.EaseInOut(0f, dir * 2.5f, 1f, dir * 5.5f));
        vel.y = new ParticleSystem.MinMaxCurve(
            AnimationCurve.EaseInOut(0f, 1.2f, 1f, 3.2f));
        Emit(hit, hitBurst);
    }

    static void Emit(ParticleSystem ps, int count)
    {
        if (ps == null) return;
        ps.time = 0f;
        ps.Emit(count);
    }

    // --- сборка частиц ---------------------------------------------------------

    ParticleSystem Build(string name, Color colour, float size, float lifetime, float order)
    {
        var go = new GameObject("Fx_" + name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 4f;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.55f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.55f, size * 1.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(colour);
        main.gravityModifier = 0f;
        main.maxParticles = 220;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        shape.radius = 0.12f;

        // Прозракают по дуге: вспыхивают и тают
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(Fade(colour));

        // Растут по ходу жизни — порыв раздувается наружу
        var grow = ps.sizeOverLifetime;
        grow.enabled = true;
        grow.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.35f, 1f, 1.3f));

        var r = ps.GetComponent<ParticleSystemRenderer>();
        if (mat != null) r.material = mat;
        r.sortingOrder = order;
        r.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
        return ps;
    }

    static Gradient Fade(Color c)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(c.a, 0f),
                    new GradientAlphaKey(c.a * 0.8f, 0.25f),
                    new GradientAlphaKey(0f, 1f) });
        return g;
    }

    public static Material SharedMaterial()
    {
        if (shared != null) return shared;
        var sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("UI/Default");
        if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (sh == null)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogError("WindFx: не нашёл шейдер частиц, ветер не отобразится");
            }
            return null;
        }
        shared = new Material(sh) { name = "WindFx" };
        if (shared.HasProperty("_MainTex"))
            shared.mainTexture = Dot();
        return shared;
    }

    // Мягкая круглая точка, нарисованная кодом: края гаснут квадратом
    // расстояния, поэтому частица не выглядит квадратиком.
    static Texture2D Dot()
    {
        if (dot != null) return dot;
        int n = 64;
        dot = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color32[n * n];
        float c = (n - 1) / 2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                float a = Mathf.Clamp01(1f - d);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
            }
        dot.SetPixels32(px);
        dot.Apply();
        dot.wrapMode = TextureWrapMode.Clamp;
        return dot;
    }
}
