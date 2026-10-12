using UnityEngine;
using UnityEngine.InputSystem;

public class HandFingerCurl : MonoBehaviour
{
    [System.Serializable]
    public class Finger
    {
        public string name;
        public Transform[] joints;        // base -> tip (skip the tip bone)
        public float[] maxAngles;         // degrees per joint when fully curled
        public Vector3 curlAxis = Vector3.right;
        public bool drivenByTrigger;      // index = trigger button, others = grip
        [HideInInspector] public Quaternion[] openRot;
    }

    [Header("Input")]
    public InputActionReference gripAction;     // float (Select Value)
    public InputActionReference triggerAction;  // float (Activate Value)

    [Header("Fingers")]
    public Finger[] fingers;
    public float smoothing = 15f;
    [Range(-1, 1)] public float debugCurl = -1; // 0..1 forces curl for tuning; -1 = off

    [Header("Grab zone")]
    public Transform grabCenter;          // empty child near the palm
    public float grabRadius = 0.08f;      // meters
    public LayerMask handleMask;          // the Handle layer
    [Range(0.1f, 1f)] public float gripThreshold = 0.5f;
    public bool latchWhileHeld = true;    // stay closed if the hand drifts out while gripping

    [Header("Glue to handle")]
    public Transform snapTarget;          // empty under Grip_Left / Grip_Right
    public float snapSpeed = 20f;

    float grip, trigger;
    bool gripping;
    readonly Collider[] hits = new Collider[4];

    Vector3 restLocalPos;
    Quaternion restLocalRot;
    float snapBlend;

    void OnEnable()
    {
        gripAction?.action.Enable();
        triggerAction?.action.Enable();
    }

    void Start()
    {
        foreach (var f in fingers)
        {
            f.openRot = new Quaternion[f.joints.Length];
            for (int i = 0; i < f.joints.Length; i++)
                f.openRot[i] = f.joints[i].localRotation;
        }

        restLocalPos = transform.localPosition;
        restLocalRot = transform.localRotation;
    }

    void LateUpdate()
    {
        // --- Input + grab zone ---
        float rawGrip = gripAction ? gripAction.action.ReadValue<float>() : 0f;
        float rawTrigger = triggerAction ? triggerAction.action.ReadValue<float>() : 0f;

        Transform center = grabCenter ? grabCenter : transform;
        bool pressed = rawGrip >= gripThreshold;
        bool inZone = Physics.OverlapSphereNonAlloc(center.position, grabRadius, hits,
                          handleMask, QueryTriggerInteraction.Collide) > 0;

        if (!pressed) gripping = false;
        else if (inZone) gripping = true;
        else if (!latchWhileHeld) gripping = false;

        float g = gripping ? rawGrip : 0f;
        float t = rawTrigger;
        if (debugCurl >= 0) { g = t = debugCurl; }

        grip = Mathf.Lerp(grip, g, Time.deltaTime * smoothing);
        trigger = Mathf.Lerp(trigger, t, Time.deltaTime * smoothing);

        // --- Finger curl ---
        foreach (var f in fingers)
        {
            float curl = f.drivenByTrigger ? trigger : grip;
            for (int i = 0; i < f.joints.Length; i++)
            {
                float angle = (i < f.maxAngles.Length ? f.maxAngles[i] : 0f) * curl;
                f.joints[i].localRotation = f.openRot[i] * Quaternion.AngleAxis(angle, f.curlAxis);
            }
        }

        // --- Glue to handle ---
        float target = (gripping && snapTarget) ? 1f : 0f;
        snapBlend = Mathf.MoveTowards(snapBlend, target, Time.deltaTime * snapSpeed);

        transform.localPosition = restLocalPos;   // pose from the controller
        transform.localRotation = restLocalRot;

        if (snapBlend > 0f && snapTarget)
        {
            // align the palm point (center) to the snap target
            Quaternion delta = snapTarget.rotation * Quaternion.Inverse(center.rotation);
            Vector3 goalPos = snapTarget.position + delta * (transform.position - center.position);
            Quaternion goalRot = delta * transform.rotation;

            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, goalPos, snapBlend),
                Quaternion.Slerp(transform.rotation, goalRot, snapBlend));
        }
    }

    void OnDrawGizmosSelected()
    {
        Transform center = grabCenter ? grabCenter : transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center.position, grabRadius);
    }
}