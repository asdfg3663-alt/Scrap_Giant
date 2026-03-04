using UnityEngine;

[DisallowMultipleComponent]
public class ModuleInstance : MonoBehaviour
{
    public ModuleData data;

    [Header("Runtime (per-instance)")]
    [Tooltip("현재 HP (각 모듈 인스턴스마다 따로 깎이는 값)")]
    public int hp;

    [Tooltip("최대 HP (data.maxHP에서 복사됨)")]
    public int maxHp;

    [Tooltip("true면 data 변경/초기 생성 시 hp를 maxHp로 재설정")]
    public bool resetHpOnDataAssign = true;

    void Awake()
    {
        SyncFromDataIfNeeded(forceReset: false);
    }

    void OnValidate()
    {
        // 에디터에서 data를 끼웠을 때도 값이 맞도록(플레이 중엔 영향 거의 없음)
        SyncFromDataIfNeeded(forceReset: false);
    }

    public void SyncFromDataIfNeeded(bool forceReset)
    {
        if (data == null) return;

        int newMax = Mathf.Max(1, data.maxHP);
        bool maxChanged = (maxHp != newMax);

        maxHp = newMax;

        if (forceReset || (resetHpOnDataAssign && (hp <= 0 || maxChanged)))
        {
            hp = maxHp;
        }

        hp = Mathf.Clamp(hp, 0, maxHp);
    }
}