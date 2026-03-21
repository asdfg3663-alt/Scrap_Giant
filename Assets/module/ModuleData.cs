using UnityEngine;

public enum ModuleType
{
    Core,
    Engine,
    FuelTank,
    Reactor,
    Weapon,
    Radiator,
    Scrap,
    Repair,
    Structure,
    SolarPanel
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
    public string localizationKey = string.Empty;
    public ModuleType type = ModuleType.Core;
    public int tier = 1;

    [Header("Durability")]
    public int maxHP = 30;

    [Header("Tier Scaling")]
    [Min(0.01f)] public float hpPerTierMultiplier = 1.5f;
    [Min(0.01f)] public float powerGenPerTierMultiplier = 1.3f;
    [Min(0.01f)] public float powerUsePerTierMultiplier = 1.12f;
    [Min(0.01f)] public float energyPerTierMultiplier = 1.3f;
    [Min(0.01f)] public float fuelPerTierMultiplier = 1.3f;
    [Min(0.01f)] public float fuelSynthesisPerTierMultiplier = 1.25f;
    [Min(0.01f)] public float thrustPerTierMultiplier = 1.25f;
    [Min(0.01f)] public float massPerTierMultiplier = 1.12f;
    [Min(0.01f)] public float scorePerTierMultiplier = 1f;
    [Min(0.01f)] public float maxHeatPerTierMultiplier = 1.2f;
    [Min(0.01f)] public float heatDissipationPerTierMultiplier = 1.2f;
    [Min(0.01f)] public float repairPerTierMultiplier = 1.2f;
    [Min(0.01f)] public float weaponDamagePerTierMultiplier = 1.3f;
    [Min(0.01f)] public float weaponFireRatePerTierMultiplier = 1.1f;
    [Min(0.01f)] public float weaponPowerPerShotPerTierMultiplier = 1.1f;
    [Min(0.01f)] public float weaponHeatPerShotPerTierMultiplier = 1.12f;
    [Min(0.01f)] public float weaponAmmoPerShotPerTierMultiplier = 1f;
    [Min(0.01f)] public float dpsPerTierMultiplier = 1.25f;

    [Header("Upgrade Economy")]
    [Min(0f)] public float upgradeScrapCostMassMultiplier = 10f;
    [Min(0.01f)] public float upgradeScrapCostPerTierMultiplier = 1.5f;

    [Header("Power (per second)")]
    public float powerGenPerSec = 1f;
    public float powerUsePerSec = 0f;

    [Header("Battery")]
    public float maxEnergy = 10f;

    [Header("Fuel")]
    public float maxFuel = 0f;
    public float fuelSynthesisPerSec = 0f;

    [Header("Movement")]
    public float thrust = 0f;
    public float mass = 1f;
    public float scoreMultiplier = 1f;

    [Header("Thermal")]
    public float maxHeat = 0f;
    public float heatDissipationPerSec = 0f;

    [Header("Repair")]
    public float repairPerSecond = 0f;

    [Header("Weapon (MVP)")]
    public WeaponType weaponType = WeaponType.None;

    [Tooltip("Damage applied by one shot.")]
    public float weaponDamage = 0f;

    [Tooltip("Shots fired per second.")]
    public float weaponFireRate = 0f;

    [Tooltip("Battery consumed per shot.")]
    public float weaponPowerPerShot = 0f;

    [Tooltip("Heat generated per shot.")]
    public float weaponHeatPerShot = 0f;

    [Tooltip("Ammo consumed per shot.")]
    public float weaponAmmoPerShot = 0f;

    [Tooltip("If zero, DPS is derived from damage * fire rate.")]
    public float dps = 0f;
}
