using UnityEngine;

public class ShipStats : MonoBehaviour
{
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

    // '가능 최대치' 기반(입력/탄약/전력 부족 등은 아직 미반영)
    public float weaponPowerPerSecPotential;
    public float weaponHeatPerSecPotential;
    public float weaponAmmoPerSecPotential;

    ModuleInstance[] modules;

    void Start()
    {
        Rebuild();

        // 시작은 풀충전
        if (energyCurrent <= 0f) energyCurrent = energyMax;
    }

    void Update()
    {

        // 초당 전력 흐름만큼 배터리 충/방전
        energyCurrent += netPowerPerSec * Time.deltaTime;

        // 0 ~ energyMax 사이로 제한
        energyCurrent = Mathf.Clamp(energyCurrent, 0f, energyMax);
    }

    public void Rebuild()
    {
        modules = GetComponentsInChildren<ModuleInstance>(true);

        maxHP = 0;
        powerGenPerSec = 0;
        powerUsePerSec = 0;
        totalThrust = 0;
        totalMass = 0;
        energyMax = 0f;

        totalDps = 0f;
        weaponPowerPerSecPotential = 0f;
        weaponHeatPerSecPotential = 0f;
        weaponAmmoPerSecPotential = 0f;

        foreach (var m in modules)
        {
            if (m == null || m.data == null) continue;
            var d = m.data;

            maxHP += d.maxHP;
            powerGenPerSec += d.powerGenPerSec;
            powerUsePerSec += d.powerUsePerSec;
            totalThrust += d.thrust;
            totalMass += d.mass;
            energyMax += d.maxEnergy;

            // Weapon DPS: dps가 0이면 weaponDamage*weaponFireRate로 자동 계산
            if (d.type == ModuleType.Weapon || d.weaponType != WeaponType.None || d.dps > 0f)
            {
                float dps = d.dps;
                if (dps <= 0f) dps = Mathf.Max(0f, d.weaponDamage) * Mathf.Max(0f, d.weaponFireRate);

                totalDps += dps;

                weaponPowerPerSecPotential += Mathf.Max(0f, d.weaponPowerPerShot) * Mathf.Max(0f, d.weaponFireRate);
                weaponHeatPerSecPotential += Mathf.Max(0f, d.weaponHeatPerShot) * Mathf.Max(0f, d.weaponFireRate);
                weaponAmmoPerSecPotential += Mathf.Max(0f, d.weaponAmmoPerShot) * Mathf.Max(0f, d.weaponFireRate);
            }
        }

        netPowerPerSec = powerGenPerSec - powerUsePerSec;

        // MVP: 현재HP는 일단 maxHP로 맞춰 시작(추후 파손/수리 반영)
        currentHP = maxHP;

        // energyMax가 바뀌었으니 현재 에너지도 범위 안으로
        energyCurrent = Mathf.Clamp(energyCurrent, 0f, energyMax);
    }

public bool TryConsumeBattery(float amount)
{
    if (amount <= 0f) return true;

    if (energyCurrent < amount)
        return false;

    energyCurrent -= amount;
    // 혹시 모를 음수 방지
    if (energyCurrent < 0f) energyCurrent = 0f;

    return true;
}


}
