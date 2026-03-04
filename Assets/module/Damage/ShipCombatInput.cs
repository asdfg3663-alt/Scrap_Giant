using UnityEngine;

public class ShipCombatInput : MonoBehaviour
{
    // ✅ 무기들이 읽는 전역 입력값
    public static bool FireHeld { get; private set; }
    public static bool FireDown { get; private set; }

    // ✅ “현재 플레이어쉽”을 전역으로 1개만 지정
    public static ShipStats ActivePlayerShip { get; private set; }

    [Header("Player gating")]
    [SerializeField] private bool requirePlayerTag = true;
    [SerializeField] private string playerTag = "Player";

    ShipStats ship;

    void Awake()
    {
        ship = GetComponentInParent<ShipStats>();
    }

    void OnEnable()
    {
        // 켜질 때 초기화
        FireHeld = false;
        FireDown = false;
    }

    void OnDisable()
    {
        // 이 인스턴스가 ActivePlayerShip였다면 전부 리셋
        if (ship != null && ActivePlayerShip == ship)
        {
            ActivePlayerShip = null;
            FireHeld = false;
            FireDown = false;
        }
    }

    void Update()
    {
        if (ship == null) ship = GetComponentInParent<ShipStats>();

        // ✅ 플레이어쉽이 아니면 전역 입력을 절대 올리지 않음
        if (!IsPlayerShip())
        {
            if (ship != null && ActivePlayerShip == ship)
            {
                ActivePlayerShip = null;
                FireHeld = false;
                FireDown = false;
            }
            return;
        }

        // ✅ 현재 플레이어쉽 등록
        ActivePlayerShip = ship;

        FireHeld = Input.GetKey(KeyCode.Space);
        FireDown = Input.GetKeyDown(KeyCode.Space);
    }

    bool IsPlayerShip()
    {
        if (ship == null) return false;
        if (!requirePlayerTag) return true;
        return ship.CompareTag(playerTag);
    }
}