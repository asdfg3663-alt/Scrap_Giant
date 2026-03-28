using UnityEngine;

public static class MobileShipInput
{
    static Vector2 moveVector;
    static bool fireHeld;
    static bool fireDownQueued;

    public static Vector2 MoveVector => moveVector;
    public static bool FireHeld => fireHeld;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        moveVector = Vector2.zero;
        fireHeld = false;
        fireDownQueued = false;
    }

    public static void SetMoveVector(Vector2 value)
    {
        moveVector = Vector2.ClampMagnitude(value, 1f);
    }

    public static void SetFireHeld(bool held)
    {
        if (held && !fireHeld)
            fireDownQueued = true;

        fireHeld = held;
    }

    public static bool ConsumeFireDown()
    {
        bool result = fireDownQueued;
        fireDownQueued = false;
        return result;
    }

    public static void ResetAll()
    {
        moveVector = Vector2.zero;
        fireHeld = false;
        fireDownQueued = false;
    }
}
