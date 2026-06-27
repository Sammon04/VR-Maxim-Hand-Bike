using UnityEngine;

public class HandbikeController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontCollider;
    public WheelCollider rearLeftCollider;
    public WheelCollider rearRightCollider;

    [Header("Visual Meshes")]
    public Transform handlebarsMesh; // The parent of the front wheel mesh
    public Transform frontWheelMesh;
    public Transform rearLeftWheelMesh;
    public Transform rearRightWheelMesh;

    [Header("Handbike Settings")]
    public float motorTorque = 150f;
    public float maxSteerAngle = 30f;

    void Start()
    {
        // Lowers center of mass so the 3-wheeler doesn't flip over easily
        GetComponent<Rigidbody>().centerOfMass = new Vector3(0, -0.5f, 0);
    }

    void FixedUpdate()
    {
        // Reads the Left/Right thumbstick on your controller for steering
        float steerInput = Input.GetAxis("XRI_Left_Primary2DAxis_X");

        // Reads the Up/Down thumbstick on your controller for gas/brakes
        float accelInput = Input.GetAxis("XRI_Right_Primary2DAxis_Y");

        // 1. Apply Steering to the Front Wheel Collider
        float steerAngle = steerInput * maxSteerAngle;
        frontCollider.steerAngle = steerAngle;

        // 2. Apply Acceleration (Handbikes are front-wheel drive!)
        frontCollider.motorTorque = accelInput * motorTorque;

        // 3. Sync all the Visual Meshes to match the Physics Colliders
        UpdateVisuals(frontCollider, frontWheelMesh, true);
        UpdateVisuals(rearLeftCollider, rearLeftWheelMesh, false);
        UpdateVisuals(rearRightCollider, rearRightWheelMesh, false);
    }

    void UpdateVisuals(WheelCollider collider, Transform mesh, bool isFrontWheel)
    {
        Vector3 position;
        Quaternion rotation;

        // Get the exact physics position and rotation from the Wheel Collider
        collider.GetWorldPose(out position, out rotation);

        if (isFrontWheel && handlebarsMesh != null)
        {
            // Rotate the entire handlebars/fork object left and right to steer
            handlebarsMesh.localRotation = Quaternion.Euler(0, frontCollider.steerAngle, 0);

            // Spin the front wheel mesh on its local X axis based on world movement
            mesh.position = position;
            mesh.rotation = rotation;
        }
        else
        {
            // For rear wheels, smoothly match position and spinning rotation
            mesh.position = position;
            mesh.rotation = rotation;
        }
    }
}
