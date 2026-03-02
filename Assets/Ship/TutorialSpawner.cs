using UnityEngine;

public class TutorialSpawner : MonoBehaviour
{
    public Transform shipRoot;              // 플레이어 코어
    public GameObject enginePrefab;
    public GameObject laserPrefab;

    public Vector2 engineOffset = new Vector2(3f, 0f);
    public Vector2 laserOffset  = new Vector2(3f, 2f);

    void Start()
    {
        if (!shipRoot)
            shipRoot = GameObject.Find("PlayerShip")?.transform;

        if (enginePrefab && shipRoot)
            Instantiate(enginePrefab, (Vector2)shipRoot.position + engineOffset, Quaternion.identity);

        if (laserPrefab && shipRoot)
            Instantiate(laserPrefab, (Vector2)shipRoot.position + laserOffset, Quaternion.identity);
    }
}