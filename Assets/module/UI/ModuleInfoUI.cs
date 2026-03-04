using System.Collections.Generic;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class ModuleInfoUI : MonoBehaviour
{
    [Header("Templates (TMP)")]
    [Tooltip("이름 줄 템플릿(예: Font Size 22). 반드시 비활성 권장.")]
    public TMP_Text nameTemplate;

    [Tooltip("나머지 정보 줄 템플릿(예: Font Size 16). 반드시 비활성 권장.")]
    public TMP_Text lineTemplate;

    [Header("Auto Layout")]
    public AutoStackTMP autoStack;

    [Header("Follow")]
    public Vector2 screenOffset = new Vector2(0f, 40f);
    public bool clampToScreen = true;

    RectTransform rt;
    CanvasGroup cg;
    Camera cam;

    // 실제로 화면에 보여줄 "이름 줄" 1개 (재사용)
    TMP_Text nameLine;

    // 정보 줄 풀링
    readonly List<TMP_Text> pool = new();
    int useCount = 0;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        cam = Camera.main;

        cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();

        if (autoStack == null) autoStack = GetComponentInChildren<AutoStackTMP>(true);

        // 템플릿 자동 탐색(인스펙터 연결 실수 방지)
        if (nameTemplate == null || lineTemplate == null)
        {
            // 자식 TMP들 중에서 폰트 사이즈 큰 걸 name, 작은 걸 line으로 추정
            var tmps = GetComponentsInChildren<TMP_Text>(true);
            if (tmps != null && tmps.Length >= 2)
            {
                TMP_Text bestBig = null, bestSmall = null;
                foreach (var t in tmps)
                {
                    if (t == null) continue;
                    if (bestBig == null || t.fontSize > bestBig.fontSize) bestBig = t;
                    if (bestSmall == null || t.fontSize < bestSmall.fontSize) bestSmall = t;
                }
                if (nameTemplate == null) nameTemplate = bestBig;
                if (lineTemplate == null) lineTemplate = bestSmall;
            }
            else if (tmps != null && tmps.Length == 1)
            {
                // 하나만 있으면 lineTemplate로 보고 nameTemplate도 동일 사용(최후 수단)
                if (lineTemplate == null) lineTemplate = tmps[0];
                if (nameTemplate == null) nameTemplate = tmps[0];
            }
        }

        // ✅ “New Text” 같은 템플릿 노출 방지: 텍스트 비우고 비활성
        SafeDisableTemplate(nameTemplate);
        SafeDisableTemplate(lineTemplate);

        Hide();
    }

    void SafeDisableTemplate(TMP_Text t)
    {
        if (t == null) return;
        t.text = "";                  // ← “New Text” 제거 핵심
        if (t.gameObject.activeSelf) t.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        var m = ModuleSelection.Selected;

        if (m == null || m.data == null)
        {
            Hide();
            return;
        }

        Show();

        BuildLines(m);

        // 자동 정렬/패널 리사이즈
        if (autoStack != null)
            autoStack.Rebuild();

        FollowSelected(m);
    }

    void BuildLines(ModuleInstance m)
    {
        var d = m.data;

        // 1) 이름 줄 확보/세팅 (Font 22 템플릿 기반)
        EnsureNameLine();
        nameLine.text = string.IsNullOrEmpty(d.displayName) ? "Module" : d.displayName;
        nameLine.gameObject.SetActive(true);

        // 2) 정보 줄들 풀링 시작
        BeginLines();

        // HP는 항상 표시
        AddLine($"HP: {m.hp} / {d.maxHP}");

        // 아래부터는 "값이 있는 것만" 표시
        if (d.powerGenPerSec > 0f || d.powerUsePerSec > 0f)
            AddLine($"Power: +{d.powerGenPerSec:0.##}/s  -{d.powerUsePerSec:0.##}/s");

        var ship = m.GetComponentInParent<ShipStats>();
        if (ship != null && ship.energyMax > 0f)
            AddLine($"Battery: {ship.energyCurrent:0.##} / {ship.energyMax:0.##}");

        if (d.mass > 0f) AddLine($"Mass: {d.mass:0.##}");
        if (d.thrust > 0f) AddLine($"Thrust: {d.thrust:0.##}");
        if (d.maxEnergy > 0f) AddLine($"Energy Cap: {d.maxEnergy:0.##}");

        bool hasWeapon =
            d.weaponType != WeaponType.None ||
            d.weaponDamage > 0f ||
            d.weaponFireRate > 0f ||
            d.dps > 0f ||
            d.weaponPowerPerShot > 0f ||
            d.weaponHeatPerShot > 0f ||
            d.weaponAmmoPerShot > 0f;

        if (hasWeapon)
        {
            float dps = d.dps > 0f ? d.dps : (d.weaponDamage * d.weaponFireRate);
            if (dps > 0f) AddLine($"DPS: {dps:0.##} ({d.weaponType})");

            if (d.weaponDamage > 0f) AddLine($"Damage: {d.weaponDamage:0.##}");
            if (d.weaponFireRate > 0f) AddLine($"FireRate: {d.weaponFireRate:0.##}/s");
            if (d.weaponPowerPerShot > 0f) AddLine($"Power/Shot: {d.weaponPowerPerShot:0.##}");
            if (d.weaponHeatPerShot > 0f) AddLine($"Heat/Shot: {d.weaponHeatPerShot:0.##}");
            if (d.weaponAmmoPerShot > 0f) AddLine($"Ammo/Shot: {d.weaponAmmoPerShot:0.##}");

            // 무기 발사중 초당 소모를 별도 라벨로 보고 싶으면
            if (d.powerUsePerSec > 0f) AddLine($"Power Use(Firing): {d.powerUsePerSec:0.##}/s");
        }

        // 3) 남는 줄 비활성
        EndLines();
    }

    void EnsureNameLine()
    {
        if (nameLine != null) return;
        if (nameTemplate == null) return;

        Transform parent = nameTemplate.transform.parent != null ? nameTemplate.transform.parent : transform;

        var go = Instantiate(nameTemplate.gameObject, parent);
        go.name = "NameLine";
        go.SetActive(true);

        nameLine = go.GetComponent<TMP_Text>();
        nameLine.text = "";
    }

    void BeginLines()
    {
        useCount = 0;
    }

    void AddLine(string text)
    {
        if (lineTemplate == null) return;

        var t = GetLineFromPool();
        t.text = text;
        t.gameObject.SetActive(true);
    }

    TMP_Text GetLineFromPool()
    {
        if (useCount < pool.Count)
        {
            var t = pool[useCount++];
            return t;
        }

        Transform parent = lineTemplate.transform.parent != null ? lineTemplate.transform.parent : transform;

        var go = Instantiate(lineTemplate.gameObject, parent);
        go.name = "Line";
        go.SetActive(true);

        var tNew = go.GetComponent<TMP_Text>();
        tNew.text = "";
        pool.Add(tNew);

        useCount++;
        return tNew;
    }

    void EndLines()
    {
        // 사용한 이후 남는 라인 끄기
        for (int i = 0; i < pool.Count; i++)
        {
            bool shouldShow = i < useCount;
            if (pool[i] != null && pool[i].gameObject.activeSelf != shouldShow)
                pool[i].gameObject.SetActive(shouldShow);
        }
    }

    void FollowSelected(ModuleInstance m)
    {
        if (!cam) cam = Camera.main;

        Vector3 sp = cam.WorldToScreenPoint(m.transform.position);
        Vector2 pos = (Vector2)sp + screenOffset;

        if (clampToScreen)
        {
            Vector2 size = rt.sizeDelta;
            float pad = 10f;
            pos.x = Mathf.Clamp(pos.x, pad + size.x * 0.5f, Screen.width - pad - size.x * 0.5f);
            pos.y = Mathf.Clamp(pos.y, pad + size.y * 0.5f, Screen.height - pad - size.y * 0.5f);
        }

        rt.position = pos;
    }

    void Show()
    {
        cg.alpha = 1f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    void Hide()
    {
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        if (nameLine != null) nameLine.gameObject.SetActive(false);
        useCount = 0;
        EndLines();
    }
}