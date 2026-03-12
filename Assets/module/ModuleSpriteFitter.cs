using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class ModuleSpriteFitter : MonoBehaviour
{
    [Tooltip("Fit the sprite into this world-space box while preserving aspect ratio.")]
    public Vector2 targetSize = Vector2.one;

    SpriteRenderer spriteRenderer;

    void Start()
    {
        Apply();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EditorApplication.delayCall += ApplyDelayed;
    }
#endif

#if UNITY_EDITOR
    void ApplyDelayed()
    {
        if (this == null)
            return;

        Apply();
    }
#endif

    public void Apply()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return;

        Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
        if (spriteSize.x <= 0.0001f || spriteSize.y <= 0.0001f)
            return;

        float scale = Mathf.Min(
            targetSize.x / spriteSize.x,
            targetSize.y / spriteSize.y);

        if (scale <= 0f)
            return;

        spriteRenderer.drawMode = SpriteDrawMode.Sliced;
        spriteRenderer.size = spriteSize * scale;
    }
}
