using UnityEngine;

// Мелкая взвесь по всему уровню: пыль, пёрышки, снежная крупа. Медленно
// плывёт вправо и чуть проседает, поэтому воздух перестаёт быть пустым.
// Тоже целиком из кода: тот же материал и та же нарисованная текстура.
//
// Как и в WindFx: модуль частицы берём в локальную переменную и правим
// там. Присваивать обратно нельзя — свойство только для чтения, но внутри
// копии лежит указатель на нативный модуль, так что правки применяются.
public class WindAmbient : MonoBehaviour
{
    [Tooltip("Насколько широко насыпать, юниты")]
    public Vector2 area = new Vector2(84f, 15f);

    [Tooltip("Где по уровню стоит облако частиц, юниты")]
    public Vector2 centre = new Vector2(5f, 6f);

    [Tooltip("Сколько частиц держим в воздухе")]
    public int count = 150;

    [Tooltip("Куда дует, юниты в секунду")]
    public float drift = 0.9f;

    [Tooltip("Размер частиц, юниты")]
    public Vector2 size = new Vector2(0.04f, 0.15f);

    public Color tint = new Color(1f, 0.98f, 0.94f, 0.55f);

    void Awake()
    {
        var go = new GameObject("AmbientMotes");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(centre.x, centre.y, 0f);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 20f;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 22f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(drift * 0.5f, drift * 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = new ParticleSystem.MinMaxGradient(tint);
        main.gravityModifier = 0.06f;   // медленно оседает
        main.maxParticles = Mathf.Max(1, count + 40);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = count / 18f;   // держим ровно count штук в воздухе

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(area.x, area.y, 1f);

        // Медленно полошит по кругу, чтобы не летело строем
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(tint, 0f), new GradientColorKey(tint, 1f) },
            new[] { new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(tint.a, 0.18f),
                    new GradientAlphaKey(tint.a, 0.75f),
                    new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material = WindFx.SharedMaterial();
        r.sortingOrder = -9;      // позади всего, кроме фона
        r.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
    }
}
