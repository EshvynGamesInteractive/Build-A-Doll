using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _Core._UI._Views
{
    /// <summary>The make-up stick the player drags over the face. Needs a raycast-target Graphic (e.g. its Image).</summary>
    public class MakeUpStick : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public event Action<PointerEventData> Dragged;
        public event Action Released;

        public void OnBeginDrag(PointerEventData eventData) => Dragged?.Invoke(eventData);

        public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(eventData);

        public void OnEndDrag(PointerEventData eventData) => Released?.Invoke();
    }
}
