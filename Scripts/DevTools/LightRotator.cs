using UnityEngine;

public class LightRotator : MonoBehaviour
{
    public bool rotate = true;

    public float rotationSpeed = 10f;

    void Update()
    {
        if (rotate)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
    }
}
