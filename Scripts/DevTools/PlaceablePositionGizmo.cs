using UnityEngine;

public class PlaceablePositionGizmo : MonoBehaviour
{
    public float gizmoSize = 0.2f;


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.aquamarine;
        Gizmos.DrawSphere(transform.position, gizmoSize);
    }
}
