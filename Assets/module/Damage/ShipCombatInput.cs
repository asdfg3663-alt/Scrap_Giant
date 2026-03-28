using UnityEngine;

public class ShipCombatInput : MonoBehaviour
{
    public static bool FireHeld { get; private set; }
    public static bool FireDown { get; private set; }
    public static ShipStats ActivePlayerShip { get; private set; }

    [Header("Player gating")]
    [SerializeField] bool requirePlayerTag = true;
    [SerializeField] string playerTag = "Player";

    ShipStats ship;

    void Awake()
    {
        ship = GetComponentInParent<ShipStats>();
    }

    void OnEnable()
    {
        FireHeld = false;
        FireDown = false;
    }

    void OnDisable()
    {
        if (ship != null && ActivePlayerShip == ship)
        {
            ActivePlayerShip = null;
            FireHeld = false;
            FireDown = false;
        }
    }

    void Update()
    {
        if (GameRuntimeState.GameplayBlocked)
        {
            if (ship != null && ActivePlayerShip == ship)
                ActivePlayerShip = null;

            MobileShipInput.SetFireHeld(false);
            FireHeld = false;
            FireDown = false;
            return;
        }

        if (ship == null)
            ship = GetComponentInParent<ShipStats>();

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

        ActivePlayerShip = ship;

        bool keyboardFireHeld = Input.GetKey(KeyCode.Space);
        bool keyboardFireDown = Input.GetKeyDown(KeyCode.Space);
        bool mobileFireHeld = MobileShipInput.FireHeld;
        bool mobileFireDown = MobileShipInput.ConsumeFireDown();

        FireHeld = keyboardFireHeld || mobileFireHeld;
        FireDown = keyboardFireDown || mobileFireDown;
    }

    bool IsPlayerShip()
    {
        if (ship == null)
            return false;

        if (!requirePlayerTag)
            return true;

        return ship.CompareTag(playerTag);
    }
}
