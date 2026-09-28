using UnityEngine;

// Фон-небо, прибитый к камере: куда бы ни уехала камера, за кадром
// всегда тот же закат, а не голый цвет фона. Раньше небо было просто
// спрайтом в мире, и пройдя чуть вправо от старта, оказывался за его
// краем. Позиция ставится абсолютно (камера + смещение), поэтому порядок
// выполнения LateUpdate относительно CameraFollow на результат не влияет.
public class SkyBackground : MonoBehaviour
{
    [Tooltip("Смещение фона от камеры, мировые координаты")]
    public Vector3 offset = new Vector3(0f, 0f, 10f);

    Transform cam;

    void Awake()
    {
        if (Camera.main != null) cam = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            if (Camera.main == null) return;
            cam = Camera.main.transform;
        }
        transform.position = cam.position + offset;
    }
}
