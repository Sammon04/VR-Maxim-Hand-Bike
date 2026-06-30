using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class HandbikeController : MonoBehaviour
{
    public WheelCollider frontCollider;
    public WheelCollider rearLeftCollider;
    public WheelCollider rearRightCollider;

    public Transform handlebarsMesh;
    public Transform frontWheelMesh;
    public Transform rearLeftWheelMesh;
    public Transform rearRightWheelMesh;

    public float motorTorque = 150f;
    public float maxSteerAngle = 30f;
    public float brakeTorque = 200f;

    Rigidbody rb;
    Keyboard keyboard;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
        keyboard = Keyboard.current;
    }

    void FixedUpdate()
    {
        if (keyboard == null) return;

        float steerInput = 0f;
        if (keyboard.dKey.isPressed) steerInput += 1f;
        if (keyboard.aKey.isPressed) steerInput -= 1f;

        float accelInput = 0f;
        if (keyboard.wKey.isPressed) accelInput += 1f;
        if (keyboard.sKey.isPressed) accelInput -= 1f;

        frontCollider.steerAngle = steerInput * maxSteerAngle;

        if (Mathf.Abs(accelInput) > 0.05f)
        {
            frontCollider.motorTorque = accelInput * motorTorque;
            frontCollider.brakeTorque = 0f;
        }
        else
        {
            frontCollider.motorTorque = 0f;
            frontCollider.brakeTorque = brakeTorque;
        }

        UpdateWheelVisual(frontCollider, frontWheelMesh);
        UpdateWheelVisual(rearLeftCollider, rearLeftWheelMesh);
        UpdateWheelVisual(rearRightCollider, rearRightWheelMesh);

        if (handlebarsMesh != null)
            handlebarsMesh.localRotation = Quaternion.Euler(0, frontCollider.steerAngle, 0);
    }

    void UpdateWheelVisual(WheelCollider collider, Transform mesh)
    {
        if (mesh == null || collider == null) return;

        Vector3 position;
        Quaternion rotation;
        collider.GetWorldPose(out position, out rotation);

        mesh.position = position;
        mesh.rotation = rotation * Quaternion.Euler(0, 90f, 0);
    }
}