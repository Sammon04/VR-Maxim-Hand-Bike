using NUnit.Framework;
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
    [Tooltip("Sideways offset (meters) below which no steering is applied to absorb hand wobble while cranking")]
    public float steerDeadzone = 0.03f;

    [Header("VR Input Actions")]
    [Tooltip("Left grip button, e.g. <XRController>{LeftHand}/gripButton")]
    public InputActionProperty leftGripHeld;
    [Tooltip("Right grip button, e.g. <XRController>{RightHand}/gripButton")]
    public InputActionProperty rightGripHeld;
    [Tooltip("Brake Button, e.g. <XRController>{LeftHand}/triggerButton")]
    public InputActionProperty brakeHeld;
    

    [Header("Ground Contact")]
    public float groundContactForce = 15f;


    [Header("Audio")]
    [Tooltip("Looping rolling/wheel sound. Set Loop = true on the source.")]
    public AudioSource rollingAudio;

    [Tooltip("Looping brake squeal. Set Loop = ture on the source.")]
    public AudioSource brakeAudio;

    [Tooltip("Looping Chain/Cranking sounds while cranking")]
    public AudioSource crankAudio;

    [Tooltip("Audio for collision impact sounds")]
    public AudioSource impactAudio;

    
    [Tooltip("Minimum relative velocity needed to trigger a crash sound")]
    public float minImpactVelocity = 1.5f;


    public float rollingMaxVolume = .8f;
    public float rollingMinPitch = .7f;
    public float rollingMaxPitch = 1.4f;
    public float brakeMaxVolume = 1f;
    [Tooltip("Brake sound only plays above this speed")]
    public float brakeMinSpeed = 1f;
    public float audioFadeSpeed = 6f;
    float lastImpactTime = 0f;
    [Tooltip("Sets the cooldown for the collision sounds playing")]
    public float impactCooldown = 0.4f;

    [Header("Game Settings")]
    [Tooltip("Drag GameSettings Asset here")]
    public GameSettings gameSettings;

    float MasterVolume => gameSettings != null ? gameSettings.masterVolume : 1f;
    float brakeVolumeCurrent;
    

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

        foreach (var src in new[] {rollingAudio, brakeAudio, crankAudio, impactAudio})
        {
            if (src == null ) continue;
            src.loop = (src != impactAudio);

            if (src != impactAudio)
            {
                src.volume = 0f;
            }
            else
                src.volume = 1f;
            src.spatialBlend = 1f; // makes it 3D
            if (src != impactAudio && !src.isPlaying) src.Play();
        }
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

            float rawOffset = localOffset.x;
            float vrSteer = 0f;
            if (Mathf.Abs(rawOffset) > steerDeadzone && maxSteerHandOffset > steerDeadzone)
            {
                float sign = Mathf.Sign(rawOffset);
                float remapped = (Mathf.Abs(rawOffset) - steerDeadzone) / (maxSteerHandOffset - steerDeadzone);
                vrSteer = sign * Mathf.Clamp01(remapped);
            }

            if (Mathf.Abs(vrSteer) > Mathf.Abs(steerInput)) {
                steerInput = vrSteer;
            }
               
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
        UpdateAudio(currentSpeed, braking, accelInput);
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

    void UpdateAudio(float speed, bool braking, float accelInput)
    {
        float speed01 = Mathf.Clamp01(Mathf.Abs(speed) / motorTorque);
        float master = MasterVolume;
        float dt = Time.deltaTime;
        if (rollingAudio != null)
        {
            float target = speed01 * rollingMaxVolume * master;
            rollingAudio.volume = Mathf.MoveTowards(rollingAudio.volume, target, audioFadeSpeed * dt);
            rollingAudio.pitch = Mathf.Lerp(rollingMinPitch, rollingMaxPitch, speed01);
        }

        // plays when already moving then braking, not idle braking
        if (brakeAudio != null)
        {
            bool brakeActive = braking && Mathf.Abs(speed) > brakeMinSpeed;
            float target = brakeActive ? brakeMaxVolume * speed01 * master : 0f;
            brakeAudio.volume = Mathf.MoveTowards(brakeAudio.volume, target, audioFadeSpeed * 2f * dt);
        }

        if (crankAudio != null)
        {
            float target = Mathf.Abs(accelInput) * master;
            crankAudio.volume = Mathf.MoveTowards(crankAudio.volume, target, audioFadeSpeed * dt);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        
        if (impactAudio == null || impactAudio.clip == null) return;

        //  Check if enough time has passed since the last sound played
        if (Time.time < lastImpactTime + impactCooldown) return;

       
        if (collision.gameObject.CompareTag("Ground")) return;

        if (collision.relativeVelocity.magnitude > minImpactVelocity)
        {
            float impactForceFactor = Mathf.Clamp01(collision.relativeVelocity.magnitude / 10f);
            float finalVolume = impactForceFactor * MasterVolume;

            impactAudio.PlayOneShot(impactAudio.clip, finalVolume);

            lastImpactTime = Time.time;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (crankZoneCenter == null) return;
        Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
        Gizmos.DrawWireSphere(crankZoneCenter.position, crankZoneRadius);
    }
}