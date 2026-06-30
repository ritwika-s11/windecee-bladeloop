using UnityEngine;

public class RotatingPart : MonoBehaviour
{
    public Vector3 axis = Vector3.right;
    public float rpm = 60f;

    void Update()
    {
        transform.Rotate(axis, rpm * 6f * Time.deltaTime, Space.Self);
    }
}
