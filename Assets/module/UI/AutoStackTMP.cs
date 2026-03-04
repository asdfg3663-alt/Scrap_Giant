using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class AutoStackTMP : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("첫 줄(모듈 이름)의 Y 위치")]
    public float startY = 0f;

    [Tooltip("기본 줄 간격 (예: 20이면 -20씩 내려감)")]
    public float lineStep = 20f;

    [Tooltip("1번째 줄과 2번째 줄 사이에만 추가로 더 벌릴 간격")]
    public float extraGapAfterFirstLine = 10f;

    [Tooltip("기존 X 유지")]
    public bool keepOriginalX = true;

    [Tooltip("X를 고정하고 싶을 때 사용 (keepOriginalX=false일 때만 적용)")]
    public float xFixed = 0f;

    [Header("Filter")]
    [Tooltip("비활성 오브젝트는 레이아웃에서 제외(빈칸 제거)")]
    public bool onlyActive = true;

    [Tooltip("텍스트가 비어있으면 제외(빈칸 제거)")]
    public bool ignoreEmptyText = true;

    [Header("When")]
    [Tooltip("true면 매 프레임 재정렬(보통 false 추천). 텍스트 갱신 직후 Rebuild() 호출하는 방식이 가장 깔끔함.")]
    public bool runEveryFrame = false;

    // 내부 버퍼(할당 줄이기)
    readonly List<TextMeshProUGUI> _tmps = new();

    void OnEnable() => Rebuild();

    void LateUpdate()
    {
        if (runEveryFrame) Rebuild();
    }

    public void Rebuild()
    {
        // 1) 하이어라키 순서대로 TMP 수집 (sibling 순서 유지)
        _tmps.Clear();
        CollectTmpsInHierarchy(transform);

        // 2) 배치
        float y = startY;
        bool placedFirstLine = false;

        foreach (var t in _tmps)
        {
            if (t == null) continue;

            if (onlyActive && !t.gameObject.activeInHierarchy) continue;
            if (ignoreEmptyText && string.IsNullOrWhiteSpace(t.text)) continue;

            var rt = (RectTransform)t.transform;
            var p = rt.anchoredPosition;

            float x = keepOriginalX ? p.x : xFixed;
            rt.anchoredPosition = new Vector2(x, y);

            // 다음 줄로 이동
            y -= lineStep;

            // ✅ 첫 줄(이름) 다음에만 추가 간격(=2번째 줄부터 더 띄우기)
            if (!placedFirstLine)
            {
                y -= extraGapAfterFirstLine;
                placedFirstLine = true;
            }
        }
    }

    void CollectTmpsInHierarchy(Transform root)
    {
        // root 본인은 제외하고, 자식들을 sibling 순서대로 순회
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);

            var tmp = child.GetComponent<TextMeshProUGUI>();
            if (tmp != null) _tmps.Add(tmp);

            // 재귀(손자까지)
            if (child.childCount > 0)
                CollectTmpsInHierarchy(child);
        }
    }
}