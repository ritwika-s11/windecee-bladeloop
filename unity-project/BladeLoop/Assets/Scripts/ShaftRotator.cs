using UnityEngine;

public class ShaftRotator : MonoBehaviour
{
    [Tooltip("Degrees per second. Positive = one direction, negative = the other.")]
    public float rotationSpeed = 120f;

    [Tooltip("Which axis to rotate around. (1,0,0) = X, (0,1,0) = Y, (0,0,1) = Z")]
    public Vector3 rotationAxis = new Vector3(1f, 0f, 0f);

    void Update()
    {
        transform.Rotate(rotationAxis * rotationSpeed * Time.deltaTime, Space.Self);
    }
}