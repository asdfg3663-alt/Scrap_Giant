using UnityEngine;

public class TutorialSpawner : MonoBehaviour
{
    public Transform shipRoot;
    public GameObject enginePrefab;
    public GameObject laserPrefab;
    public GameObject fuelTankPrefab;

    public Vector2 engineOffset = new Vector2(3f, 0f);
    public Vector2 laserOffset = new Vector2(3f, 2f);
    public Vector2 fuelTankOffset = new Vector2(0f, -1f);

    void Start()
    {
        if (!shipRoot)
            shipRoot = GameObject.Find("PlayerShip")?.transform;

        if (enginePrefab && shipRoot)
        {
            var engine = Instantiate(enginePrefab, (Vector2)shipRoot.position + engineOffset, Quaternion.identity);
            AddWorldDespawn(engine);
        }

        if (laserPrefab && shipRoot)
        {
            var laser = Instantiate(laserPrefab, (Vector2)shipRoot.position + laserOffset, Quaternion.identity);
            AddWorldDespawn(laser);
        }

        if (fuelTankPrefab && shipRoot)
        {
            var fuelTank = Instantiate(fuelTankPrefab, shipRoot.position, shipRoot.rotation);
            AttachStartingFuelTank(fuelTank.transform);
        }
    }

    void AttachStartingFuelTank(Transform moduleTf)
    {
        if (!moduleTf || !shipRoot)
            return;

        moduleTf.SetParent(shipRoot, false);
        moduleTf.localPosition = new Vector3(fuelTankOffset.x, fuelTankOffset.y, 0f);
        moduleTf.localRotation = Quaternion.identity;

        var rb = moduleTf.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
            rb.Sleep();
        }

        var attachment = moduleTf.GetComponent<ModuleAttachment>();
        if (attachment == null)
            attachment = moduleTf.gameObject.AddComponent<ModuleAttachment>();

        attachment.shipRoot = shipRoot;
        attachment.gridPos = new Vector2Int(Mathf.RoundToInt(fuelTankOffset.x), Mathf.RoundToInt(fuelTankOffset.y));
        attachment.rot90 = 0;

        IgnoreCollisionsWithShip(moduleTf);

        var shipStats = shipRoot.GetComponent<ShipStats>();
        if (shipStats != null)
            shipStats.Rebuild();
    }

    void IgnoreCollisionsWithShip(Transform moduleTf)
    {
        var myColliders = moduleTf.GetComponentsInChildren<Collider2D>(true);
        var shipColliders = shipRoot.GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < myColliders.Length; i++)
        {
            var a = myColliders[i];
            if (!a)
                continue;

            for (int j = 0; j < shipColliders.Length; j++)
            {
                var b = shipColliders[j];
                if (!b || a == b)
                    continue;

                if (b.transform.IsChildOf(moduleTf))
                    continue;

                Physics2D.IgnoreCollision(a, b, true);
            }
        }
    }

    void AddWorldDespawn(GameObject go)
    {
        if (go == null)
            return;

        var despawn = go.GetComponent<WorldDistanceDespawn>();
        if (despawn == null)
            despawn = go.AddComponent<WorldDistanceDespawn>();

        despawn.axisLimit = WorldSpawnDirector.CurrentDespawnAxisLimit;
    }
}
