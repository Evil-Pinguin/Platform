using UnityEngine;

// Камера плавно едет за героиней по горизонтали.
public class CameraFollow : MonoBehaviour
{
    [Tooltip("За кем следить")]
    public Transform target;
    [Tooltip("Плавность: меньше — резче, больше — мягче")]
    public float smoothTime = 0.2f;
    [Tooltip("Сдвиг камеры относительно героини")]
    public Vector2 offset = Vector2.zero;
    [Tooltip("Следить ли ещё и по вертикали")]
    public bool followY = false;

    Vector3 velocity;
    float startY;

    void Start()
    {
        startY = transform.position.y;
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        float y = followY ? target.position.y + offset.y : startY;
        Vector3 goal = new Vector3(target.position.x + offset.x, y, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
    }
}
