using UnityEngine;

[ExecuteAlways]
public class ClockFromSun_Robust : MonoBehaviour
{
    public enum ReadAxis { X = 0, Y = 1, Z = 2 }
    public enum AxisReference { WorldAxis, LocalToClockTransform, CustomWorldAxis }

    [Header("References")]
    public Transform directionalLight;
    public Transform hourHand;
    public Transform minuteHand;

    [Header("Light reading")]
    public ReadAxis readAxis = ReadAxis.X;
    public bool invertDirection = false;
    public bool useLightForward = true; // если false - используем light.up вместо forward

    [Header("Axis reference")]
    public AxisReference axisReference = AxisReference.WorldAxis;
    public Vector3 customWorldAxis = Vector3.up; // используетс€, если AxisReference == CustomWorldAxis

    [Header("Multipliers (360∞ light -> X hand revolutions)")]
    [Tooltip("ƒл€ 360∞ поворота света сколько оборотов сделает часова€ стрелка")]
    public float hourRevolutionsPer360 = 2f;
    [Tooltip("ƒл€ 360∞ поворота света сколько оборотов сделает минутна€ стрелка")]
    public float minuteRevolutionsPer360 = 24f;

    [Header("Absolute synchronization (deterministic mapping)")]
    [Tooltip("≈сли true Ч вычисл€ем положение стрелок напр€мую из текущего угла света " +
             "и заданной эталонной точки. Ёто даЄт детерминированный результат в Edit/Play.")]
    public bool useAbsoluteSync = true;

    [Tooltip("ѕроектированный угол света (в градусах); когда свет будет на этом угле, " +
             "стрелки будут находитьс€ в позици€х referenceMinuteAngle/referenceHourAngle.")]
    public float referenceLightAngle = 90f; // пример: свет в 90∞ по выбранной оси

    [Tooltip("ћировой угол (в градусах) минутной стрелки вокруг выбранной оси при referenceLightAngle")]
    public float referenceMinuteAngle = 90f;

    [Tooltip("ћировой угол (в градусах) часовой стрелки вокруг выбранной оси при referenceLightAngle")]
    public float referenceHourAngle = 180f;

    // internal (old incremental behaviour)
    double prevTime;
    float prevLightAngle = 0f; // angle in degrees in (-180..180]
    float accumulatedHourAngle = 0f;   // degrees (can be >360)
    float accumulatedMinuteAngle = 0f; // degrees

    Quaternion hourInitialWorldRotation = Quaternion.identity;
    Quaternion minuteInitialWorldRotation = Quaternion.identity;

    // reference offsets used for absolute sync:
    Quaternion hourReferenceRotationOffset = Quaternion.identity;
    Quaternion minuteReferenceRotationOffset = Quaternion.identity;
    bool referenceOffsetsInitialized = false;

    void OnEnable() { Initialize(false); }
    void Start() { Initialize(false); }
    void OnValidate() { if (!Application.isPlaying) Initialize(false); }

    void Initialize(bool resetHands)
    {
        if (directionalLight == null) return;

        // compute initial angle (for incremental mode compatibility)
        prevLightAngle = ComputeLightProjectedAngle();
        accumulatedHourAngle = 0f;
        accumulatedMinuteAngle = 0f;

        if (hourHand != null) hourInitialWorldRotation = hourHand.rotation;
        if (minuteHand != null) minuteInitialWorldRotation = minuteHand.rotation;

        // prepare reference offsets for absolute sync so that the current
        // hand rotations remain unchanged when current light == referenceLightAngle
        referenceOffsetsInitialized = false;
        SetupReferenceOffsets();

        if (resetHands)
        {
            if (hourHand != null) hourHand.rotation = hourInitialWorldRotation;
            if (minuteHand != null) minuteHand.rotation = minuteInitialWorldRotation;
        }

        prevTime = EditorTime();
    }

    void SetupReferenceOffsets()
    {
        if (!useAbsoluteSync) return;
        if (directionalLight == null || hourHand == null || minuteHand == null) return;

        Vector3 axisWorld = ResolveWorldAxis();
        if (axisWorld.sqrMagnitude < 1e-8f) axisWorld = Vector3.up;
        axisWorld.Normalize();

        // For the chosen referenceLightAngle and referenceHandAngle we compute
        // an offset quaternion such that:
        // Quaternion.AngleAxis(referenceHourAngle, axis) * hourReferenceRotationOffset == current hourHand.rotation
        // So later when we set:
        // hourHand.rotation = Quaternion.AngleAxis(hourAngle, axis) * hourReferenceRotationOffset
        // then for hourAngle == referenceHourAngle the rotation equals the initially observed rotation.
        Quaternion refHour = Quaternion.AngleAxis(referenceHourAngle, axisWorld);
        Quaternion refMinute = Quaternion.AngleAxis(referenceMinuteAngle, axisWorld);

        hourReferenceRotationOffset = hourHand.rotation * Quaternion.Inverse(refHour);
        minuteReferenceRotationOffset = minuteHand.rotation * Quaternion.Inverse(refMinute);

        referenceOffsetsInitialized = true;
    }

    void Update()
    {
        if (directionalLight == null || hourHand == null || minuteHand == null) return;

        float currentAngle = ComputeLightProjectedAngle(); // (-180..180]

        if (useAbsoluteSync)
        {
            // ensure offsets are initialized (recompute if needed)
            if (!referenceOffsetsInitialized) SetupReferenceOffsets();

            float relative = Mathf.DeltaAngle(referenceLightAngle, currentAngle); // signed deg difference
            if (invertDirection) relative = -relative;

            // compute absolute target angles (in degrees around axis)
            float hourAngle = referenceHourAngle + relative * hourRevolutionsPer360;
            float minuteAngle = referenceMinuteAngle + relative * minuteRevolutionsPer360;

            Vector3 axisWorld = ResolveWorldAxis();
            if (axisWorld.sqrMagnitude < 1e-8f)
            {
                Debug.LogWarning("[ClockFromSun_Robust] resolved axis is near zero; defaulting to Vector3.up");
                axisWorld = Vector3.up;
            }
            axisWorld.Normalize();

            // apply as world rotations using precomputed offsets so we keep other orientation components
            hourHand.rotation = Quaternion.AngleAxis(hourAngle, axisWorld) * hourReferenceRotationOffset;
            minuteHand.rotation = Quaternion.AngleAxis(minuteAngle, axisWorld) * minuteReferenceRotationOffset;

            // keep incremental bookkeeping consistent in case user switches modes
            prevLightAngle = currentAngle;
            accumulatedHourAngle = hourAngle;
            accumulatedMinuteAngle = minuteAngle;
        }
        else
        {
            // incremental (legacy) behaviour: rotate hands by delta since last frame
            float delta = Mathf.DeltaAngle(prevLightAngle, currentAngle); // safe signed delta
            prevLightAngle = currentAngle;

            if (invertDirection) delta = -delta;

            // convert per-360 multipliers to degree multipliers
            float hourMultiplier = hourRevolutionsPer360 * 1f;   // revolutions per 360 degrees of light
            float minuteMultiplier = minuteRevolutionsPer360 * 1f;

            // delta is in degrees of light; for hands we need degrees = delta * (revolutions)
            accumulatedHourAngle += delta * hourMultiplier;
            accumulatedMinuteAngle += delta * minuteMultiplier;

            Vector3 axisWorld = ResolveWorldAxis();
            if (axisWorld.sqrMagnitude < 1e-8f)
            {
                Debug.LogWarning("[ClockFromSun_Robust] resolved axis is near zero; defaulting to Vector3.up");
                axisWorld = Vector3.up;
            }
            axisWorld.Normalize();

            // apply as world rotations relative to initial world rotation
            hourHand.rotation = hourInitialWorldRotation * Quaternion.AngleAxis(accumulatedHourAngle, axisWorld);
            minuteHand.rotation = minuteInitialWorldRotation * Quaternion.AngleAxis(accumulatedMinuteAngle, axisWorld);
        }
    }

    // compute a stable signed angle of the light around the chosen axis by projecting a direction vector into the plane orthogonal to axis
    float ComputeLightProjectedAngle()
    {
        // choose axis in world space
        Vector3 axisWorld = ResolveWorldAxis();
        if (axisWorld.sqrMagnitude < 1e-8f) axisWorld = Vector3.up;
        axisWorld.Normalize();

        // choose a direction vector of the light to project (forward or up)
        Vector3 lightDir = useLightForward ? directionalLight.forward : directionalLight.up;
        // project onto plane orthogonal to axis: p = lightDir - (axis ? lightDir) * axis
        Vector3 proj = lightDir - axisWorld * Vector3.Dot(lightDir, axisWorld);
        float projLen = proj.magnitude;
        if (projLen < 1e-6f)
        {
            // edge case: lightDir is parallel to axis Ч choose alternative (use right vector)
            proj = useLightForward ? directionalLight.right : directionalLight.forward;
            proj = proj - axisWorld * Vector3.Dot(proj, axisWorld);
            projLen = proj.magnitude;
            if (projLen < 1e-6f)
            {
                // give up and return 0
                return 0f;
            }
        }
        proj /= projLen;

        // build a stable orthonormal basis u,v in the plane perpendicular to axisWorld
        Vector3 arbitrary = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(arbitrary, axisWorld)) > 0.99f) arbitrary = Vector3.right; // avoid near parallel
        Vector3 u = Vector3.Cross(axisWorld, arbitrary).normalized; // one basis vector in plane
        Vector3 v = Vector3.Cross(axisWorld, u).normalized;         // second basis vector in plane

        // compute coords of proj in (u,v)
        float x = Vector3.Dot(proj, u);
        float y = Vector3.Dot(proj, v);

        float angleRad = Mathf.Atan2(y, x); // [-pi, pi]
        float angleDeg = angleRad * Mathf.Rad2Deg; // (-180..180]
        return angleDeg;
    }

    Vector3 ResolveWorldAxis()
    {
        Vector3 axis;
        switch (readAxis)
        {
            case ReadAxis.X: axis = Vector3.right; break;
            case ReadAxis.Y: axis = Vector3.up; break;
            case ReadAxis.Z: axis = Vector3.forward; break;
            default: axis = Vector3.up; break;
        }

        switch (axisReference)
        {
            case AxisReference.WorldAxis:
                return axis;
            case AxisReference.LocalToClockTransform:
                return transform.TransformDirection(axis);
            case AxisReference.CustomWorldAxis:
                return customWorldAxis;
            default:
                return axis;
        }
    }

    double EditorTime()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorApplication.isPlaying ? Time.realtimeSinceStartup : UnityEditor.EditorApplication.timeSinceStartup;
#else
        return Time.realtimeSinceStartup;
#endif
    }
}