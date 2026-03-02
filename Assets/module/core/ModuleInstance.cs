using UnityEngine;

public class ModuleInstance : MonoBehaviour
{
    public ModuleData data;

    [HideInInspector] public int hp;

    void Awake()
    {
        if (data != null)
            hp = data.maxHP;
    }
}