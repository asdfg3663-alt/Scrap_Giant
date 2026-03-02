using UnityEngine;

public enum ModuleType
{
    Core, Engine, FuelTank, Reactor, Weapon, Radiator, Scrap
}

[CreateAssetMenu(menuName = "ScrapGiant/Module Data", fileName = "ModuleData_")]
public class ModuleData : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Module";
    public ModuleType type = ModuleType.Core;
    public int tier = 1;

    [Header("Durability")]
    public int maxHP = 30;

    [Header("Power (per second)")]
    public float powerGenPerSec = 1f;     // 발전(+)
    public float powerUsePerSec = 0f;     // 소비(-)

    [Header("Battery")]
    public float maxEnergy = 10f;   // 최대 전력 저장량(배터리)

    [Header("Movement")]
    public float thrust = 0f;             // 추력(+)
    public float mass = 1f;               // 무게(+)

    [Header("Weapon (MVP)")]
    public float dps = 0f;                // 레이저/무기 간단화용
}