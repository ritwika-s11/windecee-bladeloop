using UnityEngine;

public class KilnRotator : MonoBehaviour
{
    public Vector3 axis = Vector3.right;
    public float rpm = 1.0f;

    void Update()
    {
        transform.Rotate(axis, rpm * 6f * Time.deltaTime, Space.Self);
    }
}
