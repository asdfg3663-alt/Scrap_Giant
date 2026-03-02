using UnityEngine;

public class ShipMovement : MonoBehaviour
{
    public float reverseMultiplier = 0.5f;
    public float rotationSpeed = 180f;

    ShipStats stats;
    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<ShipStats>();
    }

    void FixedUpdate()
    {
        if (stats == null) return;

        // 🔹 입력 (W/S 전진후진, A/D 회전)
        float thrustInput = Input.GetAxis("Vertical");   // W=1, S=-1
        float turnInput   = Input.GetAxis("Horizontal"); // D=1, A=-1

        float thrustForce = stats.totalThrust;

        // 전진
        if (thrustInput > 0f)
        {
            rb.AddForce(transform.up * thrustInput * thrustForce);
        }

        // 후진
        if (thrustInput < 0f)
        {
            rb.AddForce(-transform.up * (-thrustInput) * thrustForce * reverseMultiplier);
        }

        // 회전
        rb.MoveRotation(rb.rotation - turnInput * rotationSpeed * Time.fixedDeltaTime);
    }
}