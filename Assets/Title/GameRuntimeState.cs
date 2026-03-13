using UnityEngine;

public static class GameRuntimeState
{
    public static bool GameplayBlocked { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        GameplayBlocked = false;
    }

    public static void SetGameplayBlocked(bool blocked)
    {
        GameplayBlocked = blocked;
    }
}
