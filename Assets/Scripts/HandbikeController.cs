using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class HandbikeController : MonoBehaviour
{
    public float motorTorque = 10f;
    public float maxVelocity = 1.0f;
    public float maxSteerAngle = 30f;
    public float acceleration = 15f;
    public float deceleration = 8f;
    public float brakeTorque = 20f;
    float currentSpeed = 0f;

    [Header("Wheel Visuals")]
    public Transform handleBar;
    public Transform frontWheelPivot;
    public Transform[] spinningWheels;

    public float WheelRadius = 0.3f;
    public float maxSteerVisualAngle = 25f;
    public float steerVisualSpeed = 200f;
    float currentSteerAngle = 0.0f;

    [Header("VR Hand Positions")]
    [Tooltip("Drag your Left VR Controller GameObject here")]
    public Transform leftHandTransform;
    [Tooltip("Drag your Right VR Controller GameObject here")]
    public Transform rightHandTransform;
    [Tooltip("Degrees of cranking per second to reach full speed (360 = 1 rotation/sec)")]
    public float maxCrankDegreesPerSec = 360f;
    [Tooltip("Check this if pedaling forward moves the bike backward")]
    public bool reverseCrankDirection = true;

    [Header("VR Input Actions")]
    [Tooltip("Button held while cranking to drive forward, e.g. <XRController>{LeftHand}/gripButton")]
    public InputActionProperty crankGripHeld;
    [Tooltip("Button that brakes, e.g. <XRController>{LeftHand}/triggerButton")]
    public InputActionProperty brakeHeld;
    [Tooltip("Optional thumbstick for steering, e.g. <XRController>{RightHand}/primary2DAxis")]
    public InputActionProperty vrSteerAxis;

    Rigidbody rb;
    Keyboard keyboard;

    // Variables for tracking the pedaling motion
    float previousCrankAngle = 0f;
    bool wasGrabbing = false;

    void OnEnable()
    {
        crankGripHeld.action?.Enable();
        brakeHeld.action?.Enable();
        vrSteerAxis.action?.Enable();
    }

    void OnDisable()
    {
        crankGripHeld.action?.Disable();
        brakeHeld.action?.Disable();
        vrSteerAxis.action?.Disable();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass -= new Vector3(0, 0.5f, 0);
        rb.maxLinearVelocity = maxVelocity;
        keyboard = Keyboard.current;
    }

    void FixedUpdate()
    {
        float accelInput = 0f;
        float steerInput = 0f;
        bool braking = false;

        // --- KEYBOARD FALLBACK ---
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) accelInput += 1f;
            if (keyboard.sKey.isPressed) accelInput -= 1f;
            if (keyboard.dKey.isPressed) steerInput += 1f;
            if (keyboard.aKey.isPressed) steerInput -= 1f;
            if (keyboard.spaceKey.isPressed) braking = true;
        }

        // --- VR HANDBIKE CRANK LOGIC ---
        if (crankGripHeld.action != null && crankGripHeld.action.IsPressed() && leftHandTransform != null && rightHandTransform != null)
        {
            // 1. Get the physical line connecting the left hand to the right hand
            Vector3 handsVector = rightHandTransform.position - leftHandTransform.position;

            // 2. Convert to the bike's local space (so steering doesn't break the math)
            Vector3 localHandsVector = transform.InverseTransformDirection(handsVector);

            // 3. Calculate the angle of that line in the Forward(Z) and Up(Y) plane
            float currentCrankAngle = Mathf.Atan2(localHandsVector.y, localHandsVector.z) * Mathf.Rad2Deg;

            if (wasGrabbing)
            {
                // 4. Find how much the hands rotated since the last frame
                float angleDelta = Mathf.DeltaAngle(previousCrankAngle, currentCrankAngle);

                // 5. Invert direction if necessary
                if (reverseCrankDirection) angleDelta *= -1f;

                // 6. Convert to speed (degrees per second) and map to -1 to 1
                float crankSpeed = angleDelta / Time.fixedDeltaTime;
                float vrAccel = Mathf.Clamp(crankSpeed / maxCrankDegreesPerSec, -1f, 1f);

                if (Mathf.Abs(vrAccel) > Mathf.Abs(accelInput))
                    accelInput = vrAccel;
            }

            previousCrankAngle = currentCrankAngle;
            wasGrabbing = true;
        }
        else
        {
            // Reset state if they let go of the grip
            wasGrabbing = false;
        }

        // --- VR BRAKING & STEERING ---
        if (brakeHeld.action != null && brakeHeld.action.IsPressed())
            braking = true;

        if (vrSteerAxis.action != null)
        {
            Vector2 stick = vrSteerAxis.action.ReadValue<Vector2>();
            if (Mathf.Abs(stick.x) > Mathf.Abs(steerInput))
                steerInput = stick.x;
        }

        // --- MOVEMENT EXECUTION ---
        float targetSpeed = accelInput * motorTorque;
        float rate = (accelInput != 0f) ? acceleration : deceleration;
        if (braking) rate = brakeTorque;

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.fixedDeltaTime);
        float speedFactor = Mathf.Clamp01(Mathf.Abs(currentSpeed) / motorTorque);

        Quaternion turnDelta = Quaternion.Euler(0f, steerInput * maxSteerAngle * speedFactor * Time.fixedDeltaTime, 0f);
        rb.MoveRotation(rb.rotation * turnDelta);

        Vector3 forward = transform.forward * currentSpeed;
        rb.linearVelocity = new Vector3(forward.x, rb.linearVelocity.y, forward.z);

        UpdateSteeringVisual(steerInput);
        UpdateWheelSpin(currentSpeed);
    }

    void UpdateSteeringVisual(float turnInput)
    {
        float targetAngle = turnInput * maxSteerVisualAngle;
        currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetAngle, steerVisualSpeed * Time.fixedDeltaTime);

        Quaternion steerRotation = Quaternion.Euler(0f, currentSteerAngle, 0f);

        if (handleBar != null) handleBar.localRotation = steerRotation;
        if (frontWheelPivot != null) frontWheelPivot.localRotation = steerRotation;
    }

    void UpdateWheelSpin(float currentSpeed)
    {
        float spinSpeed = (currentSpeed / WheelRadius) * Mathf.Rad2Deg;

        foreach (Transform wheel in spinningWheels)
        {
            if (wheel != null)
            {
                wheel.Rotate(Vector3.forward * spinSpeed * Time.fixedDeltaTime, Space.Self);
            }
        }
    }
}