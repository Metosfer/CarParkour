using UnityEngine;

/// <summary>
/// Visual-only body tilt and sway for cars. Does NOT modify physics.
/// Attach to the car root (or any transform that has a Rigidbody in parents) and
/// assign the visual body Transform (mesh root). The script tilts the visual body
/// based on lateral velocity and steering to create arcade roll/tilt feeling.
/// </summary>
[DisallowMultipleComponent]
public class CarBodyTiltVisual : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Visual body (mesh) transform that will be rotated for tilt.")]
    [SerializeField] private Transform bodyVisual;
    [Tooltip("Rigidbody used for velocity and angular velocity. If null, searched in parents on Awake.")]
    [SerializeField] private Rigidbody rb;

    [Tooltip("Optional: Front-left wheel used to read steer angle.")]
    [SerializeField] private WheelCollider frontLeft;
    [Tooltip("Optional: Front-right wheel used to read steer angle.")]
    [SerializeField] private WheelCollider frontRight;

    [Header("Tilt Enable")] 
    [Tooltip("Enable visual roll tilt effects.")]
    [SerializeField] private bool enableBodyTilt = true;

    [Header("Tilt Amounts")]
    [Tooltip("Maximum absolute visual roll (degrees).")]
    [Range(0f, 30f)]
    [SerializeField] private float maxRollDeg = 14f;
    [Tooltip("Roll from lateral speed (deg per m/s lateral). Higher = stronger sway while turning.")]
    [Min(0f)] [SerializeField] private float lateralRollPerMS = 0.8f;
    [Tooltip("Extra roll from steering at max steer (deg).")]
    [Min(0f)] [SerializeField] private float steerRollDeg = 5f;

    [Header("Multipliers")]
    [Tooltip("Global multiplier applied to the total tilt.")]
    [Min(0f)] [SerializeField] private float tiltGlobalMultiplier = 1.3f;
    [Tooltip("Multiplier for lateral-velocity-based roll.")]
    [Min(0f)] [SerializeField] private float tiltLateralMultiplier = 1.2f;
    [Tooltip("Multiplier for steer-based roll.")]
    [Min(0f)] [SerializeField] private float tiltSteerMultiplier = 1.2f;

    [Header("Speed Scaling")]
    [Tooltip("Scales tilt based on speed (km/h). X: speed, Y: multiplier.")]
    [SerializeField] private AnimationCurve tiltAmountBySpeed = new AnimationCurve(
        new Keyframe(0f,   0.8f),
        new Keyframe(50f,  1.0f),
        new Keyframe(120f, 1.2f)
    );
    [Tooltip("Below this speed (km/h), smoothly reduce visual tilt to zero.")]
    [Min(0f)] [SerializeField] private float minSpeedForTiltKmh = 1.0f;

    [Header("Smoothing")]
    [Tooltip("Use spring-damper integration for a slight oscillation/overshoot feel.")]
    [SerializeField] private bool tiltUseSpring = true;
    [Tooltip("Spring strength (deg/s^2 per deg error). Higher = snappier, more oscillation.")]
    [Min(0f)] [SerializeField] private float tiltSpringStrength = 35f;
    [Tooltip("Damping (deg/s per deg/s). Higher = less oscillation.")]
    [Min(0f)] [SerializeField] private float tiltSpringDamping = 10f;
    [Tooltip("If spring is off, simple exponential smoothing factor (lerp per second).")]
    [Min(0.1f)] [SerializeField] private float tiltSmooth = 10f;

    [Header("Steer Detection")]
    [Tooltip("Prefer using WheelCollider.steerAngle if available.")]
    [SerializeField] private bool useWheelSteerAngleIfAvailable = true;
    [Tooltip("Fallback: estimate steer factor from yaw rate when wheels are not provided.")]
    [SerializeField] private bool useYawRateAsSteerFallback = true;
    [Tooltip("Multiplier converting yaw rate (rad/s around Y) to a -1..1 steer factor.")]
    [Min(0f)] [SerializeField] private float yawRateToSteer = 0.18f;
    [Tooltip("Max steer angle used for normalization when reading WheelColliders.")]
    [Range(1f, 60f)] [SerializeField] private float maxSteerAngle = 30f;

    [Header("Extras")]
    [Tooltip("Add small visual yaw based on current roll.")]
    [SerializeField] private bool tiltAddYawFromRoll = true;
    [Tooltip("Yaw per roll degree (deg yaw for 1 deg roll).")]
    [Min(0f)] [SerializeField] private float tiltYawPerRollDeg = 0.12f;
    [Tooltip("Maximum absolute yaw from roll (deg).")]
    [Range(0f, 10f)] [SerializeField] private float tiltMaxYawFromRollDeg = 3f;

    [Header("Pivot Compensation")]
    [Tooltip("Keep an anchor point visually fixed when rolling the body to avoid gaps with wheels. Useful to lock rear axle position.")]
    [SerializeField] private bool enablePivotCompensation = true;
    [Tooltip("Anchor Transform to keep fixed in world (e.g., an empty at rear axle center attached to chassis root, not under bodyVisual). If null, uses this transform.")]
    [SerializeField] private Transform anchor;
    [Tooltip("How strongly to keep anchor fixed (1 = fully keep, 0 = ignore).")]
    [Range(0f, 1f)] [SerializeField] private float anchorKeepFactor = 1f;
    [Tooltip("Apply compensation as a local position offset on bodyVisual. If false, moves the parent (this) transform.")]
    [SerializeField] private bool applyCompensationOnBody = true;

    private Quaternion bodyInitialLocalRot;
    private Vector3 bodyInitialLocalPos;
    private float currentRollDeg;   // state
    private float rollVelDeg;       // state (deg/s)

    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponentInParent<Rigidbody>();
        }
    }

    private void OnEnable()
    {
        if (bodyVisual != null)
        {
            bodyInitialLocalRot = bodyVisual.localRotation;
            bodyInitialLocalPos = bodyVisual.localPosition;
        }
        currentRollDeg = 0f;
        rollVelDeg = 0f;
    }

    private void OnDisable()
    {
        if (bodyVisual != null)
        {
            bodyVisual.localRotation = bodyInitialLocalRot;
        }
        currentRollDeg = 0f;
        rollVelDeg = 0f;
    }

    private void LateUpdate()
    {
        if (!enableBodyTilt || bodyVisual == null)
            return;

        // Read velocity safely
        Vector3 vel = (rb != null) ? rb.velocity : Vector3.zero;
        float speedKmh = vel.magnitude * 3.6f;

        // Sideways speed in local car space
        float lateralSpeed = Vector3.Dot(vel, transform.right);
        float rollFromLateral = -lateralSpeed * lateralRollPerMS * tiltLateralMultiplier;

        // Steering-based roll
        float steerFactor = 0f;
        if (useWheelSteerAngleIfAvailable && (frontLeft != null || frontRight != null))
        {
            float sum = 0f; int cnt = 0;
            if (frontLeft != null)  { sum += frontLeft.steerAngle;  cnt++; }
            if (frontRight != null) { sum += frontRight.steerAngle; cnt++; }
            float avg = (cnt > 0) ? (sum / cnt) : 0f;
            float denom = Mathf.Max(maxSteerAngle, 1e-3f);
            steerFactor = Mathf.Clamp(avg / denom, -1f, 1f);
        }
        else if (useYawRateAsSteerFallback && rb != null)
        {
            // Convert yaw rate to a steer-like factor (-1..1)
            steerFactor = Mathf.Clamp(rb.angularVelocity.y * yawRateToSteer, -1f, 1f);
        }
        float rollFromSteer = -steerFactor * steerRollDeg * tiltSteerMultiplier;

        // Total target roll with speed scaling
        float speedMul = (tiltAmountBySpeed != null) ? tiltAmountBySpeed.Evaluate(speedKmh) : 1f;
        float targetRoll = (speedKmh < minSpeedForTiltKmh) ? 0f : (rollFromLateral + rollFromSteer) * tiltGlobalMultiplier * speedMul;
        targetRoll = Mathf.Clamp(targetRoll, -maxRollDeg, maxRollDeg);

        // Smooth to target
        if (tiltUseSpring)
        {
            float dt = Time.deltaTime;
            float k = tiltSpringStrength;
            float c = tiltSpringDamping;
            float acc = k * (targetRoll - currentRollDeg) - c * rollVelDeg; // deg/s^2
            rollVelDeg += acc * dt;                                         // deg/s
            currentRollDeg += rollVelDeg * dt;                               // deg
            currentRollDeg = Mathf.Clamp(currentRollDeg, -maxRollDeg, maxRollDeg);
        }
        else
        {
            float smoothRate = tiltSmooth;
            currentRollDeg = Mathf.Lerp(currentRollDeg, targetRoll, 1f - Mathf.Exp(-smoothRate * Time.deltaTime));
        }

        // Optional yaw from roll
        float yawDeg = 0f;
        if (tiltAddYawFromRoll)
        {
            yawDeg = Mathf.Clamp(currentRollDeg * tiltYawPerRollDeg, -tiltMaxYawFromRollDeg, tiltMaxYawFromRollDeg);
        }

        // Save anchor world position before rotation to compute compensation
        Vector3 anchorWorldBefore = Vector3.zero;
        Transform anchorT = anchor != null ? anchor : transform;
        if (enablePivotCompensation && anchorT != null)
            anchorWorldBefore = anchorT.position;

        // Apply rotation
        bodyVisual.localRotation = bodyInitialLocalRot * Quaternion.Euler(0f, yawDeg, currentRollDeg);

        // Compensate pivot drift to keep anchor in place
        if (enablePivotCompensation && anchorT != null && anchorKeepFactor > 0f)
        {
            Vector3 anchorWorldAfter = anchorT.position;
            Vector3 delta = anchorWorldBefore - anchorWorldAfter; // how much anchor moved due to rotation
            if (applyCompensationOnBody)
            {
                // Convert world delta to local of body parent to adjust localPosition
                Transform parent = bodyVisual.parent;
                if (parent != null)
                {
                    Vector3 localDelta = parent.InverseTransformVector(delta * anchorKeepFactor);
                    bodyVisual.localPosition += localDelta;
                }
                else
                {
                    bodyVisual.position += delta * anchorKeepFactor;
                }
            }
            else
            {
                // Move this transform instead of body local
                transform.position += delta * anchorKeepFactor;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (rb == null)
            rb = GetComponentInParent<Rigidbody>();

        if (bodyVisual == null && transform.childCount > 0)
        {
            // Heuristic: try first child as body if unset (non-destructive)
            bodyVisual = transform.GetChild(0);
        }
    }
#endif
}
