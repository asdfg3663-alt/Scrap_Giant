using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    public Transform target;
    public Vector2 offset = Vector2.zero;

    [Header("Follow Feel")]
    public float smoothTime = 0.18f;  // 작을수록 더 딱 붙음
    public float maxSpeed = 999f;     // 카메라 최고 추적 속도 제한(원하면)

    Vector3 vel;

    void LateUpdate()
    {
        if (!target) return;

        Vector3 desired = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z
        );

        transform.position = Vector3.SmoothDamp(
            transform.position, desired, ref vel, smoothTime, maxSpeed, Time.deltaTime
        );
    }
}