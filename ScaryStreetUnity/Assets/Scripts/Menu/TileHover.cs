using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Fires onHover when a select-screen tile is pointed at with the mouse or reached with keys / stick.
public class TileHover : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public Action onHover;
    public void OnPointerEnter(PointerEventData e) => onHover?.Invoke();
    public void OnSelect(BaseEventData e) => onHover?.Invoke();
}
