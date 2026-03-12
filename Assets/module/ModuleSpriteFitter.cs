using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class ModuleSpriteFitter : MonoBehaviour
{
    [Tooltip("Fit the sprite into this world-space box while preserving aspect ratio.")]
    public Vector2 targetSize = Vector2.one;

    SpriteRenderer spriteRenderer;

    void Awake()
    {
        Apply();
    }

    void OnValidate()
    {
        Apply();
    }

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
