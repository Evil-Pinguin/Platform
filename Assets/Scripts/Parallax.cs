using UnityEngine;

// Слой, который едет медленнее камеры. Слой с factorX = 1 двигается как
// земля, factorX = 0 совсем стоит на месте, как прибитый к камере фон.
// Чем меньше коэффициент, тем дальше слой по глубине.
public class Parallax : MonoBehaviour
{
    [Tooltip("1 — едет как земля, 0 — стоит на месте")]
    public float factorX = 1f;

    [Tooltip("По вертикали. Обычно 1: небо не должно ездить вверх-вниз")]
    public float factorY = 1f;

    Transform cam;
    Vector3 camOrigin;

    void Start()
    {
        if (Camera.main != null)
            cam = Camera.main.transform;
        if (cam != null)
            camOrigin = cam.position;
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            if (Camera.main == null)
                return;
            cam = Camera.main.transform;
            camOrigin = cam.position;
            return;
        }

        // Камера уехала на d — сдвигаем слой на долю от d. Остальное
        // смещение как раз и даёт медленное движение.
        Vector3 d = cam.position - camOrigin;
        transform.position = new Vector3(d.x * (1f - factorX),
                                         d.y * (1f - factorY),
                                         0f);
    }
}
