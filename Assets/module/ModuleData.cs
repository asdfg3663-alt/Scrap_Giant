using UnityEngine;

public enum ModuleType
{
    Core,
    Engine,
    FuelTank,
    Reactor,
    Weapon,
    Radiator,
    Scrap
}

public enum WeaponType
{
    None,
    Laser,
    Cannon,
    Railgun
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
    public float powerUsePerSec = 0f;     // 상시 소비(-)  (레이저 같은 무기의 '발사 중' 소비는 아래 Weapon 섹션)

    [Header("Battery")]
    public float maxEnergy = 10f;         // 최대 전력 저장량(배터리)

    [Header("Movement")]
    public float thrust = 0f;             // 추력(+)
    public float mass = 1f;               // 무게(+)

    [Header("Weapon (MVP)")]
    public WeaponType weaponType = WeaponType.None;

    [Tooltip("발사 1회당 데미지(레이저도 1틱=1발로 취급)")]
    public float weaponDamage = 0f;

    [Tooltip("초당 발사 횟수")]
    public float weaponFireRate = 0f;

    [Tooltip("발사 1회당 전력 소모(배터리에서 차감). 레이저는 이 값이 큼.")]
    public float weaponPowerPerShot = 0f;

    [Tooltip("발사 1회당 발열(나중에 Heat 시스템 붙일 때 사용).")]
    public float weaponHeatPerShot = 0f;

    [Tooltip("발사 1회당 탄약 소모. 레이저는 0.")]
    public float weaponAmmoPerShot = 0f;

    [Tooltip("표시/밸런스용 DPS. 0이면 (weaponDamage*weaponFireRate)로 자동 계산합니다.")]
    public float dps = 0f;                // 기존 UI/밸런스 호환용
}
