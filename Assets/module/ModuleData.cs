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
