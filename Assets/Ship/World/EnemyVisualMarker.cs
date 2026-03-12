using UnityEngine;

[DisallowMultipleComponent]
public class EnemyVisualMarker : MonoBehaviour
{
    public Color originalColor = Color.white;
    public Material originalMaterial;
    public bool hasOriginalMaterial;
    public Transform outlineRoot;
    public bool hasOriginalColor;

    public void Capture(SpriteRenderer source)
    {
        if (source == null || hasOriginalColor)
            return;

        originalColor = source.color;
        hasOriginalColor = true;

        if (!hasOriginalMaterial)
        {
            originalMaterial = source.sharedMaterial;
            hasOriginalMaterial = true;
        }
    }

    public void Restore(SpriteRenderer source)
    {
        if (source != null)
        {
            if (hasOriginalColor)
                source.color = originalColor;

            if (hasOriginalMaterial)
                source.sharedMaterial = originalMaterial;
        }

        if (outlineRoot != null)
            Object.Destroy(outlineRoot.gameObject);

        Object.Destroy(this);
    }
}
