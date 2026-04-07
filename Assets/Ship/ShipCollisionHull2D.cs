using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class ShipCollisionHull2D : MonoBehaviour
{
    [Range(0.5f, 1f)]
    public float colliderSize = 0.92f;

    [SerializeField, HideInInspector]
    List<BoxCollider2D> generatedColliders = new();

    public void RebuildHull(ModuleInstance[] modules = null)
    {
        CleanupMissingColliders();

        modules ??= GetComponentsInChildren<ModuleInstance>(true);
        List<Vector2Int> occupiedCells = CollectOccupiedCells(modules);

        EnsureColliderCount(occupiedCells.Count);
        ConfigureColliders(occupiedCells);
        RefreshCollisionIgnores();
    }

    public static bool IsHullCollider(Collider2D collider)
    {
        if (collider == null)
            return false;

        ShipCollisionHull2D hull = collider.GetComponent<ShipCollisionHull2D>();
        if (hull == null)
            return false;

        return hull.OwnsCollider(collider as BoxCollider2D);
    }

    bool OwnsCollider(BoxCollider2D collider)
    {
        if (collider == null)
            return false;

        for (int i = 0; i < generatedColliders.Count; i++)
        {
            if (generatedColliders[i] == collider)
                return true;
        }

        return false;
    }

    List<Vector2Int> CollectOccupiedCells(ModuleInstance[] modules)
    {
        HashSet<Vector2Int> unique = new();

        for (int i = 0; i < modules.Length; i++)
        {
            ModuleInstance module = modules[i];
            if (module == null)
                continue;

            unique.Add(GetModuleGridCell(module.transform));
        }

        List<Vector2Int> cells = new(unique);
        cells.Sort((a, b) =>
        {
            int yCompare = a.y.CompareTo(b.y);
            return yCompare != 0 ? yCompare : a.x.CompareTo(b.x);
        });
        return cells;
    }

    Vector2Int GetModuleGridCell(Transform moduleTransform)
    {
        ModuleAttachment attachment = moduleTransform.GetComponent<ModuleAttachment>();
        if (attachment != null && attachment.shipRoot == transform)
            return attachment.gridPos;

        Vector3 local = transform.InverseTransformPoint(moduleTransform.position);
        return new Vector2Int(Mathf.RoundToInt(local.x), Mathf.RoundToInt(local.y));
    }

    void EnsureColliderCount(int targetCount)
    {
        while (generatedColliders.Count < targetCount)
        {
            BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
            generatedColliders.Add(collider);
        }

        while (generatedColliders.Count > targetCount)
        {
            int lastIndex = generatedColliders.Count - 1;
            BoxCollider2D collider = generatedColliders[lastIndex];
            generatedColliders.RemoveAt(lastIndex);

            if (collider == null)
                continue;

            collider.enabled = false;
            collider.size = Vector2.zero;
            collider.offset = Vector2.zero;

            if (Application.isPlaying)
                Destroy(collider);
            else
                DestroyImmediate(collider);
        }
    }

    void ConfigureColliders(List<Vector2Int> occupiedCells)
    {
        Vector2 size = Vector2.one * Mathf.Clamp(colliderSize, 0.1f, 1f);

        for (int i = 0; i < generatedColliders.Count; i++)
        {
            BoxCollider2D collider = generatedColliders[i];
            if (collider == null)
                continue;

            Vector2Int cell = occupiedCells[i];
            collider.enabled = true;
            collider.isTrigger = false;
            collider.size = size;
            collider.offset = new Vector2(cell.x, cell.y);
        }
    }

    public void RefreshCollisionIgnores()
    {
        Collider2D[] shipColliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < generatedColliders.Count; i++)
        {
            BoxCollider2D hullCollider = generatedColliders[i];
            if (hullCollider == null)
                continue;

            for (int j = 0; j < shipColliders.Length; j++)
            {
                Collider2D other = shipColliders[j];
                if (other == null || other == hullCollider)
                    continue;

                if (IsHullCollider(other))
                    continue;

                Physics2D.IgnoreCollision(hullCollider, other, true);
            }
        }

    }

    void CleanupMissingColliders()
    {
        for (int i = generatedColliders.Count - 1; i >= 0; i--)
        {
            if (generatedColliders[i] == null)
                generatedColliders.RemoveAt(i);
        }
    }
}
