using UnityEngine;

public class ShipStats : MonoBehaviour
{
    public int maxHP;
    public int currentHP;

    public float powerGenPerSec;
    public float powerUsePerSec;
    public float netPowerPerSec;

    public float energyMax;
    public float energyCurrent;

    public float totalThrust;
    public float totalMass;
    public float totalDps;

    ModuleInstance[] modules;

    void Start()
    {
        Rebuild();
        // 시작은 풀충전으로 하고 싶으면 아래 한 줄 유지
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
        totalDps = 0;
        energyMax = 0f;

        foreach (var m in modules)
        {
            if (m.data == null) continue;

            maxHP += m.data.maxHP;
            powerGenPerSec += m.data.powerGenPerSec;
            powerUsePerSec += m.data.powerUsePerSec;
            totalThrust += m.data.thrust;
            totalMass += m.data.mass;
            totalDps += m.data.dps;
            energyMax += m.data.maxEnergy;
        }

        netPowerPerSec = powerGenPerSec - powerUsePerSec;

        // MVP: 현재HP는 일단 maxHP로 맞춰 시작(추후 파손/수리 반영)
        currentHP = maxHP;

        // energyMax가 바뀌었으니 현재 에너지도 범위 안으로
        energyCurrent = Mathf.Clamp(energyCurrent, 0f, energyMax);
    }
}