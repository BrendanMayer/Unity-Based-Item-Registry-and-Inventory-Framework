using UnityEngine;
using UnityEngine.EventSystems;

public class UIRaycastProbe : MonoBehaviour,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public void OnPointerDown(PointerEventData e) { Debug.Log($"PROBE PointerDown: {e.button} on {name}"); }
    public void OnBeginDrag(PointerEventData e) { Debug.Log($"PROBE BeginDrag on {name}"); }
    public void OnDrag(PointerEventData e) { /* keep empty */ }
    public void OnEndDrag(PointerEventData e) { Debug.Log($"PROBE EndDrag on {name}"); }
    public void OnDrop(PointerEventData e) { Debug.Log($"PROBE Drop on {name}"); }
}
