using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class ModuleInfoUI : MonoBehaviour
{
    [Header("UI Refs (TMP)")]
    public TMP_Text nameText;
    public TMP_Text hpText;
    public TMP_Text powerText;
    public TMP_Text batteryText;   // 있으면 표시, 없으면 무시
    public TMP_Text extraText;     // (선택) 무기 DPS 같은 추가정보

    [Header("Follow")]
    public Vector2 screenOffset = new Vector2(0f, 40f);
    public bool clampToScreen = true;

    RectTransform rt;
    CanvasGroup cg;
    Camera cam;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (rt == null)
        {
            enabled = false;
            return;
        }

        cam = Camera.main;

        cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();

        Hide();
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

        // 텍스트
        nameText.text = string.IsNullOrEmpty(m.data.displayName) ? "Module" : m.data.displayName;
        hpText.text = $"HP: {m.hp} / {m.data.maxHP}";
        powerText.text = $"Power: +{m.data.powerGenPerSec:0.##}/s  -{m.data.powerUsePerSec:0.##}/s";

        if (batteryText != null)
        {
            var ship = m.GetComponentInParent<ShipStats>();
            batteryText.text = (ship != null)
                ? $"Battery: {ship.energyCurrent:0.##} / {ship.energyMax:0.##}"
                : "Battery: -";
        }

        if (extraText != null)
        {
            if (m.data.type == ModuleType.Weapon || m.data.weaponType != WeaponType.None || m.data.dps > 0f)
            {
                float dps = m.data.dps > 0f ? m.data.dps : (m.data.weaponDamage * m.data.weaponFireRate);
                extraText.text = $"DPS: {dps:0.##}  ({m.data.weaponType})";
                extraText.gameObject.SetActive(true);
            }
            else
            {
                extraText.text = "";
                extraText.gameObject.SetActive(false);
            }
        }

        if (!cam) cam = Camera.main;

        // 모듈 월드 위치 -> 스크린 좌표 -> UI 패널 위치
        Vector3 screenPos = cam.WorldToScreenPoint(m.transform.position);
        Vector2 pos = (Vector2)screenPos + screenOffset;

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
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    void Hide()
    {
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }
}
