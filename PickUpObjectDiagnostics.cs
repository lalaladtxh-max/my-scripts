using UnityEngine;

public class PickUpObject : MonoBehaviour
{
    // ... Оригинальные переменные и поля без изменений ...

    private Vector3 lastHeldPos = Vector3.zero; // Для отслеживания рывков
    private Quaternion lastHeldRot = Quaternion.identity; // Для отслеживания вращения

    void Start()
    {
        if (!playerCamera) playerCamera = Camera.main;
        if (!fpsController) fpsController = FindObjectOfType<FPSController>();
    }

    void Update()
    {
        if (!fpsController || !playerCamera || !fpsController.controlsEnabled)
            return;

        if (Input.GetKeyDown(fpsController.key_pickup))
        {
            if (heldObject)
                ReleaseObject(false);
            else
                TryPickup();
        }
        if (heldObject && Input.GetKeyDown(fpsController.key_throw))
        {
            ReleaseObject(true);
        }
        if (heldObject)
        {
            if (Input.GetKeyDown(fpsController.key_rotate))
                StartRotate();
            if (Input.GetKey(fpsController.key_rotate))
                UpdateRotation();
            if (Input.GetKeyUp(fpsController.key_rotate))
                StopRotate();
        }
    }

    void FixedUpdate()
    {
        if (heldObject)
        {
            Vector3 target = playerCamera.transform.position + playerCamera.transform.forward * holdDistance;
            Vector3 toTarget = target + playerCamera.transform.rotation * pickupLocalGrabOffset - heldObject.position;
            Vector3 velocity = toTarget * holdMoveForce * Time.fixedDeltaTime;

            // == Лог перемещения ==
            if ((heldObject.position - lastHeldPos).sqrMagnitude > 0.0025f || velocity.sqrMagnitude > 0.01f)
            {
                Debug.Log(
                    $"[MOVE] Pos: {heldObject.position:F3}, Target: {target:F3}, Offset: {pickupLocalGrabOffset:F3}, Vel: {velocity:F3}, Δ: {(heldObject.position - lastHeldPos).magnitude:F4}"
                );
                lastHeldPos = heldObject.position;
            }

            heldObject.linearVelocity = velocity; // velocity, как в оригинале

            if (!isRotating)
            {
                Quaternion rot = Quaternion.Slerp(heldObject.rotation, pickupHeldRotation, holdRotateSpeed * Time.fixedDeltaTime);
                // == Лог вращения без ручного режима ==
                if (Quaternion.Angle(heldObject.rotation, rot) > 0.5f)
                {
                    Debug.Log(
                        $"[ROTATE] LerpTo: {rot.eulerAngles:F2} from {heldObject.rotation.eulerAngles:F2}"
                    );
                }
                heldObject.MoveRotation(rot);
            }
        }
    }

    void TryPickup()
    {
        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 4.0f, pickableMask, QueryTriggerInteraction.Collide))
        {
            Rigidbody rb = hit.collider.attachedRigidbody;
            if (rb != null && !rb.isKinematic && !rb.CompareTag("Player"))
            {
                heldObject = rb;
                heldObject.useGravity = true;
                heldObject.linearDamping = 6f;
                heldObject.angularDamping = 8f;
                pickupLocalGrabOffset = heldObject.transform.InverseTransformPoint(hit.point) - Vector3.zero;
                pickupHeldRotation = heldObject.rotation;
                heldObject.maxAngularVelocity = 50f;
                rotateYaw = 0f;
                rotatePitch = 0f;
                objectInitialRotation = heldObject.rotation;

                lastHeldPos = heldObject.position;
                lastHeldRot = heldObject.rotation;

                Debug.Log($"[PICKUP] Pickup {heldObject.name} | Pos: {heldObject.position:F3}, Rot: {heldObject.rotation.eulerAngles:F2}");
            }
        }
    }

    void ReleaseObject(bool withThrow)
    {
        if (!heldObject) return;
        heldObject.linearDamping = 0.05f;
        heldObject.angularDamping = 0.05f;
        if (withThrow)
        {
            heldObject.linearVelocity = Vector3.zero;
            heldObject.AddForce(playerCamera.transform.forward * throwForce, ForceMode.VelocityChange);
            Debug.Log($"[PICKUP] Throw {heldObject.name} | Force: {throwForce}, Dir: {playerCamera.transform.forward}");
        }
        else
        {
            Debug.Log($"[PICKUP] Release {heldObject.name}");
        }
        heldObject = null;
        isRotating = false;
        if (fpsController) fpsController.blockMouseLook = false;
    }

    void StartRotate()
    {
        if (!heldObject) return;
        isRotating = true;
        lastMousePos = Input.mousePosition;
        objectInitialRotation = heldObject.rotation;
        rotateYaw = 0f;
        rotatePitch = 0f;
        lastHeldRot = heldObject.rotation;
        if (fpsController) fpsController.blockMouseLook = true;
        Debug.Log($"[ROTATE] Start rotating {heldObject.name}");
    }

    void UpdateRotation()
    {
        if (!heldObject || !isRotating) return;

        Vector3 delta = Input.mousePosition - lastMousePos;
        lastMousePos = Input.mousePosition;
        float sensitivity = 0.5f;

        rotateYaw += delta.x * sensitivity;
        rotatePitch -= delta.y * sensitivity;
        rotatePitch = Mathf.Clamp(rotatePitch, -89f, 89f);

        Quaternion rot = Quaternion.Euler(rotatePitch, rotateYaw, 0f);
        Quaternion result = rot * objectInitialRotation;
        heldObject.MoveRotation(result);
        pickupHeldRotation = result;

        // == Лог вращения ==
        if (Quaternion.Angle(lastHeldRot, result) > 0.5f || delta.sqrMagnitude > 1f)
        {
            Debug.Log(
                $"[ROTATE] ΔYaw: {rotateYaw:F2}, ΔPitch: {rotatePitch:F2}, MouseΔ: {delta}, NewRot: {result.eulerAngles:F2}"
            );
            lastHeldRot = result;
        }
    }

    void StopRotate()
    {
        isRotating = false;
        if (fpsController) fpsController.blockMouseLook = false;
        Debug.Log($"[ROTATE] Stop rotating");
    }
}

// ВАЖНО! В логах ты всегда увидишь [MOVE] для перемещения, [ROTATE] для ручного вращения, а [PICKUP] для подбора/выброса.
// Все значения округлены для читаемости, ничего лишнего!