using UnityEngine;

/// <summary>
/// Keeps the spawned character in view while staying within the test platform.
/// </summary>
public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float minimumX = -3.2f;
    [SerializeField] private float maximumX = 3.2f;
    [SerializeField] private float followSpeed = 8f;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        float targetX = Mathf.Clamp(target.position.x, minimumX, maximumX);
        float smoothedX = Mathf.Lerp(transform.position.x, targetX, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        transform.position = new Vector3(smoothedX, transform.position.y, transform.position.z);
    }
}
