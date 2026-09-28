using UnityEngine;

// Панель способностей. Рисуется через OnGUI — так не нужно заводить
// канвас в сцене, а встроенный шрифт Unity берётся сам.
public class AbilityHud : MonoBehaviour
{
    [Tooltip("Сколько масштабировать панель относительно экрана")]
    public float scale = 1f;

    PlayerController pc;
    GUIStyle box;
    GUIStyle name;
    GUIStyle hint;

    void Start()
    {
        pc = FindObjectOfType<PlayerController>();
    }

    void OnGUI()
    {
        if (pc == null)
            return;
        if (box == null)
            MakeStyles();

        float w = 190f * scale;
        float h = 26f * scale;
        float x = 16f * scale;
        float y = Screen.height - h * 5f - 24f * scale;

        GUI.Label(new Rect(x, y - 26f * scale, 420f * scale, 22f * scale),
                  "1–5 способность, F применить, J/мышь атака (Күн Куо: комбо ×4)",
                  hint);

        for (int i = 0; i < 5; i++)
        {
            var rect = new Rect(x, y + i * (h + 4f * scale), w, h);
            bool selected = i == pc.selectedAbility;
            float ready = i < pc.abilityReady.Length ? pc.abilityReady[i] : 0f;
            bool spent = ready > 0f;

            GUI.Box(rect, GUIContent.none, box);

            // заливка полосы: чем темнее, тем дольше ждать
            if (spent)
            {
                float total = i < pc.abilityCooldowns.Length
                    ? Mathf.Max(0.01f, pc.abilityCooldowns[i]) : 1f;
                float fill = 1f - ready / total;
                var old = GUI.color;
                GUI.color = new Color(1f, 0.45f, 0.35f, 0.35f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * fill, rect.height),
                                Texture2D.whiteTexture);
                GUI.color = old;
            }

            var style = selected ? name : hint;
            GUI.Label(new Rect(rect.x + 8f * scale, rect.y + 4f * scale,
                               rect.width, rect.height),
                      (i + 1) + "  " + PlayerController.AbilityNames[i]
                      + (spent ? "  " + ready.ToString("0.0") + "с" : ""),
                      style);
        }
    }

    void MakeStyles()
    {
        box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleLeft };
        name = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(14f * scale),
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.92f, 0.65f) }
        };
        hint = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(13f * scale),
            normal = { textColor = Color.white }
        };
        hint.padding = new RectOffset(0, 0, 0, 0);
    }
}
