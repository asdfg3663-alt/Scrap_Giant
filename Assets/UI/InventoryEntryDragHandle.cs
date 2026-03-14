using System;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class InventoryEntryDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerClickHandler
{
    PlayerHudRuntime owner;
    string entryId;

    public void Configure(PlayerHudRuntime hud, string id)
    {
        owner = hud;
        entryId = id;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner == null || string.IsNullOrWhiteSpace(entryId))
            return;

        owner.TryBeginStoredModuleDrag(entryId, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null || string.IsNullOrWhiteSpace(entryId))
            return;

        owner.SelectInventoryEntry(entryId);
    }
}
