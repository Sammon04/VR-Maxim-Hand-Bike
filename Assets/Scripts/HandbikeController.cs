using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Rigidbody))]
public class HandbikeController : MonoBehaviour
{
    public float motorTorque = 10f;
    public float maxVelocity = 50f;
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

    [Header("Crank Visual")]
    [Tooltip("The 3D model of the crank handles that should visually spin as the player cranks")]
    public Transform crankVisual;
    [Tooltip("Local axis the crank visual rotates around. (Default is Vector3.right, may need to use Vector3.forward if visual looks incorrect)")]
    public Vector3 crankVisualAxis = Vector3.right;

    [Header("VR Hand Positions")]
    [Tooltip("Drag your Left VR Controller GameObject here")]
    public Transform leftHandTransform;
    [Tooltip("Drag your Right VR Controller GameObject here")]
    public Transform rightHandTransform;
    [Tooltip("Degrees of cranking per second to reach full speed (360 = 1 rotation/sec)")]
    public float maxCrankDegreesPerSec = 360f;
    [Tooltip("Check this if pedaling forward moves the bike backward")]
    public bool reverseCrankDirection = true;

    [Header("VR Crank Area")]
    [Tooltip("Empty GameObject placed at the crank's hub. Both hands must stay within the crankZoneRadius of this for pedaling or steering to work")]
    public Transform crankZoneCenter;
    [Tooltip("How far (in meters) a hand can be from crankZoneCenter and still count as being on the crank")]
    public float crankZoneRadius = 0.35f;

    [Header("VR Steering")]
    [Tooltip("Sideways offset of the hands (in meters, relative to the bike) that produces full steering input")]
    public float maxSteerHandOffset = .15f;

    [Header("VR Input Actions")]
    [Tooltip("Left grip button, e.g. <XRController>{LeftHand}/gripButton")]
    public InputActionProperty leftGripHeld;
    [Tooltip("Right grip button, e.g. <XRController>{RightHand}/gripButton")]
    public InputActionProperty rightGripHeld;
    [Tooltip("Brake Button, e.g. <XRController>{LeftHand}/triggerButton")]
    public InputActionProperty brakeHeld;
    

    [Header("Ground Contact")]
    public float groundContactForce = 15f;
    

    Rigidbody rb;
    Keyboard keyboard;

    // Variables for tracking the pedaling motion
    float previousCrankAngle = 0f;
    bool wasGrabbing = false;

    Quaternion crankVisualRestRotation = Quaternion.identity;

    void OnEnable()
    {
        leftGripHeld.action?.Enable();
        rightGripHeld.action?.Enable();
        brakeHeld.action?.Enable();     
    }

    void OnDisable()
    {
        leftGripHeld.action?.Disable();
        rightGripHeld.action?.Disable();
        brakeHeld.action?.Disable();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass -= new Vector3(0, 0.5f, 0);
        rb.maxLinearVelocity = maxVelocity;
        keyboard = Keyboard.current;

        if (crankVisual != null)
            crankVisualRestRotation = crankVisual.localRotation;
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

        // ---CHECKS IF BOTH HANDS ARE ON THE CRANK---
        bool handsOnCrank = leftHandTransform != null && rightHandTransform != null && crankZoneCenter != null &&
            Vector3.Distance(leftHandTransform.position, crankZoneCenter.position) <= crankZoneRadius &&
            Vector3.Distance(rightHandTransform.position, crankZoneCenter.position) <= crankZoneRadius;
        if (handsOnCrank)
        {
            bool bothGripsHeld = 
                leftGripHeld.action != null && leftGripHeld.action.IsPressed() &&
                rightGripHeld.action != null && rightGripHeld.action.IsPressed();
            if (bothGripsHeld)
            {
                // physical line connecting the left hand to the right hand
                Vector3 handsVector = rightHandTransform.position - leftHandTransform.position;

                // converts the distance to bike's local space
                Vector3 localhandsVector = transform.InverseTransformDirection(handsVector);

                // calculates the angle of the line
                float currentCrankAngle = Mathf.Atan2(localhandsVector.y, localhandsVector.z) * Mathf.Rad2Deg;

                // spin the visual crank arm to match where the hands currently are
                if (crankVisual != null)
                    crankVisual.localRotation = crankVisualRestRotation * Quaternion.AngleAxis(currentCrankAngle, crankVisualAxis);

                if (wasGrabbing)
                {
                    // finds how much the hands rotated since the last frame
                    float angleDelta = Mathf.DeltaAngle(previousCrankAngle, currentCrankAngle);

                    // inverts the direction if necessary
                    if (reverseCrankDirection) angleDelta *= -1f;

                    // converts to speed (in degrees per second) and mapt to -1 to 1
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
                wasGrabbing = false;
            }
            // ---VR STEERING: push the midpoint of your hands sideways relative to the bike ---
            Vector3 handsMidpoint = (leftHandTransform.position + rightHandTransform.position) * 0.5f;
            Vector3 localOffset = transform.InverseTransformPoint(handsMidpoint) - transform.InverseTransformPoint(crankZoneCenter.position);
            float vrSteer = Mathf.Clamp(localOffset.x / maxSteerHandOffset, -1f, 1f);
            if (Mathf.Abs(vrSteer) > Mathf.Abs(steerInput))
                steerInput = vrSteer;
        }
        else
        {
            // Hands left the crank zone entirely - reset tracking so it doesn't jump when they come back into the zone
            wasGrabbing = false;
        }

        // --- VR BRAKING & STEERING ---
        if (brakeHeld.action != null && brakeHeld.action.IsPressed())
            braking = true;

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
        rb.AddForce(Vector3.down * groundContactForce, ForceMode.Acceleration);
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

    private void OnDrawGizmosSelected()
    {
        if (crankZoneCenter == null) return;
        Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
        Gizmos.DrawWireSphere(crankZoneCenter.position, crankZoneRadius);
    }
}