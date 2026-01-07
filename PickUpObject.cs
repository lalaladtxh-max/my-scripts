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

    private Rigidbody heldObject = null;
    private Vector3 pickupLocalGrabOffset = Vector3.zero;
    private Quaternion pickupHeldRotation = Quaternion.identity;
    private bool isRotating = false;
    private Vector3 lastMousePos;
    private Quaternion objectRotationYaw = Quaternion.identity;
    private Quaternion objectRotationPitch = Quaternion.identity;

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
            heldObject.linearVelocity = toTarget * holdMoveForce * Time.fixedDeltaTime;
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
                objectRotationYaw = Quaternion.identity;
                objectRotationPitch = Quaternion.identity;
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
        objectRotationYaw = Quaternion.identity;
        objectRotationPitch = Quaternion.identity;
        pickupHeldRotation = heldObject.rotation;
        if (fpsController) fpsController.blockMouseLook = true;
    }

    void UpdateRotation()
    {
        if (!heldObject || !isRotating) return;

        Vector3 delta = Input.mousePosition - lastMousePos;
        lastMousePos = Input.mousePosition;
        float sensitivity = 0.5f;

        objectRotationYaw *= Quaternion.AngleAxis(delta.x * sensitivity, Vector3.up);
        objectRotationYaw = Quaternion.Normalize(objectRotationYaw);
        objectRotationPitch *= Quaternion.AngleAxis(-delta.y * sensitivity, Vector3.right);
        objectRotationPitch = Quaternion.Normalize(objectRotationPitch);

        Quaternion result = objectRotationYaw * objectRotationPitch * pickupHeldRotation;
        result = Quaternion.Normalize(result);

        heldObject.MoveRotation(result);
        pickupHeldRotation = result;
    }

    void StopRotate()
    {
        isRotating = false;
        if (fpsController) fpsController.blockMouseLook = false;
    }
}