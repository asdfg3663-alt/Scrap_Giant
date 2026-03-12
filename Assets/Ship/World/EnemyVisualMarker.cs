using UnityEngine;

[DisallowMultipleComponent]
public class EnemyVisualMarker : MonoBehaviour
{
    public Color originalColor = Color.white;
    public SpriteRenderer outlineRenderer;
    public bool hasOriginalColor;

    public void Capture(SpriteRenderer source)
    {
        if (source == null || hasOriginalColor)
            return;

        originalColor = source.color;
        hasOriginalColor = true;
    }

    public void Restore(SpriteRenderer source)
    {
        if (source != null && hasOriginalColor)
            source.color = originalColor;

        if (outlineRenderer != null)
            Object.Destroy(outlineRenderer.gameObject);

        Object.Destroy(this);
    }
}
