using UnityEngine;

public class StarfieldDrift : MonoBehaviour
{
    public Rigidbody2D ship;          // PlayerShip의 Rigidbody2D 드래그
    public float driftFactor = 0.35f; // 별이 지나가는 체감 속도
    public Vector2 wrapSize = new Vector2(60f, 36f); // 별 필드 영역(대충 Shape보다 조금 크게)

    ParticleSystem ps;
    ParticleSystem.Particle[] parts;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        parts = new ParticleSystem.Particle[ps.main.maxParticles];
    }

    void LateUpdate()
    {
        if (!ship) return;

        int n = ps.GetParticles(parts);
        Vector3 delta = (Vector3)(-ship.linearVelocity * driftFactor * Time.deltaTime);

        Vector3 camPos = Camera.main.transform.position;

        float halfX = wrapSize.x * 0.5f;
        float halfY = wrapSize.y * 0.5f;

        for (int i = 0; i < n; i++)
        {
            Vector3 p = parts[i].position + delta;

            // 카메라 주변으로 래핑(무한 우주 느낌)
            float dx = p.x - camPos.x;
            float dy = p.y - camPos.y;

            if (dx > halfX) p.x -= wrapSize.x;
            else if (dx < -halfX) p.x += wrapSize.x;

            if (dy > halfY) p.y -= wrapSize.y;
            else if (dy < -halfY) p.y += wrapSize.y;

            parts[i].position = p;
        }

        ps.SetParticles(parts, n);
    }
}