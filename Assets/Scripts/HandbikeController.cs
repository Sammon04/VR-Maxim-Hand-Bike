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

    Rigidbody rb;
    Keyboard keyboard;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass -= new Vector3(0, 0.5f, 0);
        rb.maxLinearVelocity = maxVelocity;
        keyboard = Keyboard.current;
    }

    void FixedUpdate()
    {
        if (keyboard == null) return;

        float accelInput = 0f;

        if (keyboard.wKey.isPressed) accelInput += 1f;
        if (keyboard.sKey.isPressed) accelInput -= 1f;

        float steerInput = 0f;
        if (keyboard.dKey.isPressed) steerInput += 1f;
        if (keyboard.aKey.isPressed) steerInput -= 1f;


        float targetSpeed = accelInput * motorTorque;
        float rate = (accelInput != 0f) ? acceleration : deceleration;
        if (keyboard.spaceKey.isPressed) rate = brakeTorque;
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

