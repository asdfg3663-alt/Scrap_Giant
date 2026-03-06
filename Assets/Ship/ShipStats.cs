using UnityEngine;

public class ShipStats : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("플레이어 조종 함선이면 true (인스펙터에서 Player ShipStats에만 체크)")]
    public bool isPlayerShip = false;

    [Tooltip("true면 오브젝트가 Tag=Player일 때 isPlayerShip을 자동으로 true로 설정")]
    public bool autoDetectPlayerByTag = true;

    public int maxHP;
    public int currentHP;

    [Header("Power")]
    public float powerGenPerSec;
    public float powerUsePerSec;
    public float netPowerPerSec;

    [Header("Battery")]
    public float energyMax;
    public float energyCurrent;

    [Header("Movement")]
    public float totalThrust;
    public float totalMass;

    [Header("Combat (MVP)")]
    public float totalDps;

    // 가상 최대치 기반(입력/탄약/열 관리 없음, 아직 미반영)
    public float weaponPowerPerSecPotential;
    public float weaponHeatPerSecPotential;
    public float weaponAmmoPerSecPotential;

    ModuleInstance[] modules;
    bool rebuildQueued;

    void Awake()
    {
        if (autoDetectPlayerByTag && CompareTag("Player"))
            isPlayerShip = true;
    }

    void Start()
    {
        Rebuild();

        if (energyCurrent <= 0f)
            energyCurrent = energyMax;

        if (isPlayerShip)
            PlayerHudRuntime.EnsureForPlayer(this);
    }

    void Update()
    {
        energyCurrent += netPowerPerSec * Time.deltaTime;
        energyCurrent = Mathf.Clamp(energyCurrent, 0f, energyMax);
    }

    void LateUpdate()
    {
        if (!rebuildQueued) return;

        rebuildQueued = false;
        Rebuild();
    }

    void OnTransformChildrenChanged()
    {
        ScheduleRebuild();
    }

    public void ScheduleRebuild()
    {
        rebuildQueued = true;
    }

    public void Rebuild()
    {
        modules = GetComponentsInChildren<ModuleInstance>(true);

        maxHP = 0;
        powerGenPerSec = 0f;
        powerUsePerSec = 0f;
        netPowerPerSec = 0f;

        totalThrust = 0f;
        totalMass = 0f;
        energyMax = 0f;

        totalDps = 0f;
        weaponPowerPerSecPotential = 0f;
        weaponHeatPerSecPotential = 0f;
        weaponAmmoPerSecPotential = 0f;

        foreach (var m in modules)
        {
            if (m == null || m.data == null) continue;
            var d = m.data;

            maxHP += m.GetMaxHp();
            powerGenPerSec += m.GetPowerGenPerSec();

            totalThrust += m.GetThrust();
            totalMass += m.GetMass();
            energyMax += m.GetMaxEnergy();

            bool isWeapon = d.type == ModuleType.Weapon || d.weaponType != WeaponType.None || d.dps > 0f;
            if (!isWeapon)
                powerUsePerSec += m.GetPowerUsePerSec();

            if (isWeapon)
            {
                float dps = m.GetDps();

                totalDps += dps;
                weaponPowerPerSecPotential += Mathf.Max(0f, m.GetWeaponPowerPerShot()) * Mathf.Max(0f, m.GetWeaponFireRate());
                weaponHeatPerSecPotential += Mathf.Max(0f, m.GetWeaponHeatPerShot()) * Mathf.Max(0f, m.GetWeaponFireRate());
                weaponAmmoPerSecPotential += Mathf.Max(0f, m.GetWeaponAmmoPerShot()) * Mathf.Max(0f, m.GetWeaponFireRate());
            }
        }

        netPowerPerSec = powerGenPerSec - powerUsePerSec;
        currentHP = maxHP;
        energyCurrent = Mathf.Clamp(energyCurrent, 0f, energyMax);
    }

    public bool TryConsumeBattery(float amount)
    {
        if (amount <= 0f) return true;
        if (energyCurrent < amount) return false;

        energyCurrent -= amount;
        if (energyCurrent < 0f)
            energyCurrent = 0f;

        return true;
    }
}
