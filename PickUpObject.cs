using UnityEngine;

public class PickUpObject : MonoBehaviour
{
    [Header("Настройки подбора")]
    public float holdDistance = 2.2f;
    public float holdMoveForce = 600f;
    public float holdRotateSpeed = 8f;
    public float throwForce = 400f;
    public LayerMask pickableMask = ~0;
    public Camera playerCamera;
    public FPSController fpsController;

    // --- Делаем необходимые поля публичными для диагностики ---
    public Rigidbody heldObject = null;
    public Vector3 pickupLocalGrabOffset = Vector3.zero;
    public Quaternion pickupHeldRotation = Quaternion.identity;
    public bool isRotating = false;
    public Vector3 lastMousePos;
    public float rotateYaw = 0f;
    public float rotatePitch = 0f;
    public Quaternion objectInitialRotation = Quaternion.identity;
    // -----------------------------------------------------------

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
            heldObject.linearVelocity = toTarget * holdMoveForce * Time.fixedDeltaTime; // velocity, как в оригинале

            if (!isRotating)
            {
                heldObject.MoveRotation(Quaternion.Slerp(heldObject.rotation, pickupHeldRotation, holdRotateSpeed * Time.fixedDeltaTime));
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
                // Для диагностики
                rotateYaw = 0f;
                rotatePitch = 0f;
                objectInitialRotation = heldObject.rotation;
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
        if (fpsController) fpsController.blockMouseLook = true;
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
    }

    void StopRotate()
    {
        isRotating = false;
        if (fpsController) fpsController.blockMouseLook = false;
    }
}