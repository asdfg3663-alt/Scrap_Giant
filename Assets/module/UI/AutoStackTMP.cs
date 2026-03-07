using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AutoStackTMP : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("첫 줄(모듈 이름)의 anchored Y")]
    public float startY = 0f;

    [Tooltip("기본 줄 간격(예: 20이면 -20씩 내려감)")]
    public float lineStep = 20f;

    [Tooltip("1번째 줄(이름)과 2번째 줄 사이에만 추가 간격")]
    public float extraGapAfterFirstLine = 10f;

    [Tooltip("keepOriginalX=false일 때 고정 X")]
    public float xFixed = 0f;

    [Tooltip("각 텍스트의 기존 X 유지")]
    public bool keepOriginalX = true;

    [Header("Filter")]
    public bool onlyActive = true;
    public bool ignoreEmptyText = true;

    [Header("Auto Resize Panel (optional)")]
    [Tooltip("배경 패널 RectTransform (예: ModuleInfoPanel 배경 Image의 RectTransform)")]
    public RectTransform resizeTarget;
    public float paddingTop = 10f;
    public float paddingBottom = 10f;

    [Header("When")]
    [Tooltip("true면 매 프레임 정렬. 보통 false + 필요할 때 Rebuild() 호출 추천")]
    public bool runEveryFrame = false;

    readonly List<TextMeshProUGUI> _ordered = new();

    void OnEnable() => Rebuild();

    void LateUpdate()
    {
        if (runEveryFrame) Rebuild();
    }

    public void Rebuild()
    {
        _ordered.Clear();
        CollectInHierarchyOrder(transform);

        float y = startY;
        bool firstPlaced = false;
        int lineCount = 0;

        foreach (var t in _ordered)
        {
            if (t == null) continue;

            if (onlyActive && !t.gameObject.activeInHierarchy) continue;
            if (ignoreEmptyText && string.IsNullOrWhiteSpace(t.text)) continue;

            var rt = (RectTransform)t.transform;

            float x = keepOriginalX ? rt.anchoredPosition.x : xFixed;
            rt.anchoredPosition = new Vector2(x, y);

            lineCount++;

            y -= lineStep;

            if (!firstPlaced)
            {
                y -= extraGapAfterFirstLine;
                firstPlaced = true;
            }
        }

        if (resizeTarget != null)
        {
            float contentHeight = 0f;
            if (lineCount > 0)
            {
                // 첫 줄 포함 lineCount줄이 쌓일 때 필요한 높이 근사
                // (lineCount-1)*lineStep + extraGapAfterFirstLine + 첫줄 1줄 높이(lineStep) 정도
                contentHeight = (lineCount - 1) * lineStep + extraGapAfterFirstLine + lineStep;
            }

            float targetH = paddingTop + paddingBottom + contentHeight;
            var sd = resizeTarget.sizeDelta;
            resizeTarget.sizeDelta = new Vector2(sd.x, Mathf.Max(20f, targetH));
        }
    }

    void CollectInHierarchyOrder(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);

            var layout = child.GetComponent<LayoutElement>();
            if (layout != null && layout.ignoreLayout)
                continue;

            var tmp = child.GetComponent<TextMeshProUGUI>();
            if (tmp != null) _ordered.Add(tmp);

            if (child.childCount > 0)
                CollectInHierarchyOrder(child);
        }
    }
}
