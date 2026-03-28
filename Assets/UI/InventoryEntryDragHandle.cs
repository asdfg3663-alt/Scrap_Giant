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

        eventData.useDragThreshold = false;
        owner.TryBeginStoredModuleDrag(entryId, eventData.position, eventData.pointerId);
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
