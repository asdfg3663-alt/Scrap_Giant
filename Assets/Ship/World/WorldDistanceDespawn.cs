using UnityEngine;

[DisallowMultipleComponent]
public class WorldDistanceDespawn : MonoBehaviour
{
    public float axisLimit = 100f;
    public float checkInterval = 0.5f;

    float nextCheckTime;

    void Update()
    {
        if (Time.time < nextCheckTime)
            return;

        nextCheckTime = Time.time + Mathf.Max(0.1f, checkInterval);

        Transform player = WorldSpawnDirector.PlayerTransform;
        if (player == null)
            return;

        Vector3 delta = transform.position - player.position;
        if (Mathf.Abs(delta.x) > axisLimit || Mathf.Abs(delta.y) > axisLimit)
            Destroy(gameObject);
    }
}
