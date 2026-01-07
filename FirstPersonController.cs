using System;
using UnityEngine;

/// <summary>
/// First Person Controller с поддержкой расширенного управления и опцией blockMouseLook для работы с PickUpObject.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    [Header("References")]
    public Transform bodyTransform;
    public Camera playerCamera;

    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 9f;
    public float crouchSpeed = 2.5f;
    public float mouseSensitivity = 2f;

    [Header("Momentum / Acceleration")]
    public float accelTime = 0.06f;
    public float decelTime = 0.35f;

    [Header("Jump & Gravity")]
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;
    public float maxFallSpeed = -30f;

    [Header("Heights")]
    public float standingHeight = 2f;
    public float crouchHeight = 1f;
    public float heightSmoothTime = 0.08f;

    [Header("Lean")]
    public float leanAngle = 45f;
    public float leanSpeed = 10f;

    [Header("Collision")]
    public LayerMask ceilingMask = ~0;

    [Header("Footsteps (Audio)")]
    public AudioSource footstepSource;
    public AudioSource sfxSource;
    public AudioClip[] footstepClips;
    public AudioClip[] walkFootstepClips;
    public AudioClip[] sprintFootstepClips;
    public AudioClip[] crouchFootstepClips;
    public AudioClip[] jumpClips;
    public AudioClip[] landClips;
    public float walkStepInterval = 0.5f;
    public float sprintStepInterval = 0.34f;
    public float crouchStepInterval = 0.8f;
    [Range(0f, 1f)]
    public float footstepVolume = 1f;
    public float footstepMoveThreshold = 0.1f;

    [Header("Head Bob (Camera Shake)")]
    public bool enableHeadBob = true;
    public float walkBobHeight = 0.03f;
    public float sprintBobHeight = 0.06f;
    public float walkBobFrequency = 1.5f;
    public float sprintBobFrequency = 2.8f;
    public float bobSwayAmount = 0.02f;
    public float bobSmoothness = 10f;
    public bool disableBobOnCrouch = true;

    [Header("Input / Key Bindings")]
    [Tooltip("Если true — используется кастомная схема управления из биндов ниже. Если false — используется Input.GetAxis + GetKey стандартно.")]
    public bool useCustomKeys = true;

    public KeyCode key_forward = KeyCode.W;
    public KeyCode key_back = KeyCode.S;
    public KeyCode key_left = KeyCode.A;
    public KeyCode key_right = KeyCode.D;
    public KeyCode key_sprint = KeyCode.LeftShift;
    public KeyCode key_crouch = KeyCode.LeftControl;
    public KeyCode key_jump = KeyCode.Space;
    public KeyCode key_action = KeyCode.E;
    public KeyCode key_lean = KeyCode.Q;

    [Header("Pickup Interaction Keys")]
    public KeyCode key_pickup = KeyCode.F;      // Поднять/отпустить предмет
    public KeyCode key_rotate = KeyCode.Mouse0; // Вращение предмета (ЛКМ)
    public KeyCode key_throw = KeyCode.Mouse1; // Бросок предмета (ПКМ)

    const string PREF_KEY_PREFIX = "key_";
    const string SENSITIVITY_KEY = "mouse_sensitivity";
    public static event Action<string, KeyCode> BindingChanged;

    [Header("UI / Escape menu")]
    public GameObject mainMenu;
    public GameObject[] subPanels;
    public GameObject crosshairCanvas;
    public bool lockCursorOnResume = true;

    CharacterController cc;
    float verticalVelocity = 0f;

    float currentHeight;
    float heightVelocity = 0f;
    Vector3 cameraInitialLocalPos;
    float cameraStandLocalY;
    float cameraYVelocity = 0f;
    Vector3 cameraBaseLocalPos;
    Vector3 cameraBobVelocity = Vector3.zero;
    Vector3 bodyInitialLocalPos;
    float bodyYVelocity = 0f;
    float yaw = 0f;
    float pitch = 0f;
    Vector3 currentHorizontalVelocity = Vector3.zero;
    Vector3 horizontalVelocityRef = Vector3.zero;
    float footstepTimer = 0f;
    Vector3 lastPosition;
    bool wasMovingLastFrame = false;
    bool wasGrounded = true;
    float bobTimer = 0f;

    [HideInInspector]
    public bool controlsEnabled = true;

    // --- Новое: блокировка обзора мышью во время вращения предмета ---
    [HideInInspector]
    public bool blockMouseLook = false;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        if (bodyTransform == null)
        {
            if (transform.childCount > 0)
                bodyTransform = transform.GetChild(0);
            else
                Debug.LogWarning("Body Transform not assigned — please assign bodyTransform.");
        }

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null)
            Debug.LogWarning("Player camera not assigned and none found in children.");

        currentHeight = Mathf.Max(cc.height, standingHeight);
        cameraInitialLocalPos = (playerCamera != null) ? playerCamera.transform.localPosition : Vector3.up * (currentHeight - 0.1f);
        cameraStandLocalY = cameraInitialLocalPos.y;
        cameraBaseLocalPos = cameraInitialLocalPos;

        if (bodyTransform != null)
            bodyInitialLocalPos = bodyTransform.localPosition;
        else
            bodyInitialLocalPos = Vector3.zero;

        yaw = transform.localEulerAngles.y;
        if (playerCamera != null)
        {
            float camX = playerCamera.transform.localEulerAngles.x;
            if (camX > 180f) camX -= 360f;
            pitch = camX;
        }
        transform.localEulerAngles = new Vector3(0f, yaw, 0f);
        if (playerCamera != null)
            playerCamera.transform.localEulerAngles = new Vector3(pitch, 0f, 0f);

        if (lockCursorOnResume)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (footstepSource == null && HasAnyFootstepClips())
        {
            GameObject go = new GameObject("FootstepSource");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            footstepSource = go.AddComponent<AudioSource>();
            footstepSource.playOnAwake = false;
            footstepSource.spatialBlend = 1f;
            footstepSource.minDistance = 1f;
            footstepSource.maxDistance = 20f;
            footstepSource.rolloffMode = AudioRolloffMode.Linear;
        }

        if (sfxSource == null && HasAnySfxClips())
        {
            GameObject go = new GameObject("SFXSource");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            sfxSource = go.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 1f;
            sfxSource.minDistance = 1f;
            sfxSource.maxDistance = 30f;
            sfxSource.rolloffMode = AudioRolloffMode.Linear;
        }

        lastPosition = transform.position;
        footstepTimer = 0f;
        wasGrounded = cc.isGrounded;

        LoadBindingsFromPlayerPrefs();
        BroadcastAllBindings();
    }

    void LoadBindingsFromPlayerPrefs()
    {
        key_forward = (KeyCode)PlayerPrefs.GetInt(PREF_KEY_PREFIX + "Row_up", (int)key_forward);
        key_back = (KeyCode)PlayerPrefs.GetInt(PREF_KEY_PREFIX + "Row_down", (int)key_back);
        key_right = (KeyCode)PlayerPrefs.GetInt(PREF_KEY_PREFIX + "Row_right", (int)key_right);
        key_left = (KeyCode)PlayerPrefs.GetInt(PREF_KEY_PREFIX + "Row_left", (int)key_left);
        key_sprint = (KeyCode)PlayerPrefs.GetInt(PREF_KEY_PREFIX + "Row_sprint", (int)key_sprint);
        key_jump = (KeyCode)PlayerPrefs.GetInt(PREF_KEY_PREFIX + "Row_jump", (int)key_jump);
        key_crouch = (KeyCode)PlayerPrefs.GetInt(PREF_KEY_PREFIX + "Row_crought", (int)key_crouch);
        key_action = (KeyCode)PlayerPrefs.GetInt(PREF_KEY_PREFIX + "Row_action", (int)key_action);
        key_lean = (KeyCode)PlayerPrefs.GetInt(PREF_KEY_PREFIX + "Row_naklon", (int)key_lean);
        key_pickup = (KeyCode)PlayerPrefs.GetInt(PREF_KEY_PREFIX + "Row_pickup", (int)key_pickup);
        key_rotate = (KeyCode)PlayerPrefs.GetInt(PREF_KEY_PREFIX + "Row_rotate", (int)key_rotate);
        key_throw = (KeyCode)PlayerPrefs.GetInt(PREF_KEY_PREFIX + "Row_throw", (int)key_throw);
        mouseSensitivity = PlayerPrefs.GetFloat(SENSITIVITY_KEY, mouseSensitivity);
    }

    void BroadcastAllBindings()
    {
        BindingChanged?.Invoke("Row_up", key_forward);
        BindingChanged?.Invoke("Row_down", key_back);
        BindingChanged?.Invoke("Row_right", key_right);
        BindingChanged?.Invoke("Row_left", key_left);
        BindingChanged?.Invoke("Row_sprint", key_sprint);
        BindingChanged?.Invoke("Row_crought", key_crouch);
        BindingChanged?.Invoke("Row_jump", key_jump);
        BindingChanged?.Invoke("Row_action", key_action);
        BindingChanged?.Invoke("Row_naklon", key_lean);
        BindingChanged?.Invoke("Row_pickup", key_pickup);
        BindingChanged?.Invoke("Row_rotate", key_rotate);
        BindingChanged?.Invoke("Row_throw", key_throw);
    }

    void Update()
    {
        HandleEscape();

        if (!controlsEnabled)
        {
            currentHorizontalVelocity = Vector3.zero;
            horizontalVelocityRef = Vector3.zero;
            lastPosition = transform.position;
            return;
        }

        HandleMouseLook();
        HandleHeightAndCrouch();
        HandleMovement();
        HandleLean();
        HandleHeadBob();
        lastPosition = transform.position;
    }

    void HandleEscape()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (AnySubPanelActive())
        {
            DeactivateAllSubPanels();
            return;
        }
        ToggleMainMenu();
    }

    bool AnySubPanelActive()
    {
        if (subPanels == null || subPanels.Length == 0) return false;
        foreach (var p in subPanels)
        {
            if (p != null && p.activeSelf) return true;
        }
        return false;
    }

    void DeactivateAllSubPanels()
    {
        if (subPanels == null) return;
        foreach (var p in subPanels)
        {
            if (p != null && p.activeSelf)
                p.SetActive(false);
        }

        if (mainMenu != null && mainMenu.activeSelf)
        {
            var settings = mainMenu.GetComponent<SettingsMenuController>();
            if (settings != null)
                settings.BackToMain();
        }
        else
        {
            RestoreControlsAndCursor();
        }
    }

    void ToggleMainMenu()
    {
        if (mainMenu == null)
        {
            Debug.LogWarning("FPSController: mainMenu not assigned but ESC was pressed.");
            return;
        }

        bool willOpen = !mainMenu.activeSelf;
        mainMenu.SetActive(willOpen);

        if (willOpen)
        {
            if (crosshairCanvas != null) crosshairCanvas.SetActive(false);
            controlsEnabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            RestoreControlsAndCursor();
        }
    }

    void RestoreControlsAndCursor()
    {
        if (crosshairCanvas != null) crosshairCanvas.SetActive(true);
        controlsEnabled = true;

        if (lockCursorOnResume)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void HandleMouseLook()
    {
        if (blockMouseLook)
            return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.localEulerAngles = new Vector3(0f, yaw, 0f);

        if (playerCamera != null)
            playerCamera.transform.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    bool GetKey(KeyCode k) => Input.GetKey(k);
    bool GetKeyDownKey(KeyCode k) => Input.GetKeyDown(k);

    void HandleMovement()
    {
        float inputX = 0f;
        float inputZ = 0f;

        if (useCustomKeys)
        {
            inputZ = (GetKey(key_forward) ? 1f : 0f) + (GetKey(key_back) ? -1f : 0f);
            inputX = (GetKey(key_right) ? 1f : 0f) + (GetKey(key_left) ? -1f : 0f);
        }
        else
        {
            inputX = Input.GetAxisRaw("Horizontal");
            inputZ = Input.GetAxisRaw("Vertical");
        }

        Vector3 input = new Vector3(inputX, 0f, inputZ);
        input = Vector3.ClampMagnitude(input, 1f);

        bool isSprinting = useCustomKeys ? GetKey(key_sprint) : (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        bool isCrouching = useCustomKeys ? GetKey(key_crouch) : (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
        float targetSpeed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);

        Vector3 desiredHorizontal = Vector3.zero;
        if (input.sqrMagnitude > 0.0001f)
        {
            Vector3 localDir = input.normalized;
            desiredHorizontal = transform.TransformDirection(localDir) * targetSpeed;
        }

        float smoothTime = (input.sqrMagnitude > 0.0001f) ? accelTime : decelTime;
        currentHorizontalVelocity = Vector3.SmoothDamp(currentHorizontalVelocity, desiredHorizontal, ref horizontalVelocityRef, Mathf.Max(0.0001f, smoothTime));

        if (cc.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if ((useCustomKeys ? GetKeyDownKey(key_jump) : Input.GetKeyDown(KeyCode.Space)) && cc.isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            PlaySfx(jumpClips);
            wasGrounded = false;
        }

        verticalVelocity += gravity * Time.deltaTime;
        verticalVelocity = Mathf.Max(verticalVelocity, maxFallSpeed);

        Vector3 velocity = currentHorizontalVelocity + Vector3.up * verticalVelocity;
        cc.Move(velocity * Time.deltaTime);

        ResolveGroundPenetration();

        if (!wasGrounded && cc.isGrounded)
        {
            PlaySfx(landClips);
        }
        wasGrounded = cc.isGrounded;

        bool sprint = useCustomKeys ? GetKey(key_sprint) : (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        bool crouch = useCustomKeys ? GetKey(key_crouch) : (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
        HandleFootsteps(sprint, crouch);
    }

    void HandleHeightAndCrouch()
    {
        bool wantCrouch = useCustomKeys ? GetKey(key_crouch) : (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
        float targetHeight = wantCrouch ? crouchHeight : standingHeight;

        float lowestWorldYBefore = cc.bounds.min.y;
        float centerYOffset = cc.center.y - (cc.height * 0.5f);

        if (!wantCrouch && cc.height < standingHeight)
        {
            float standingCenterLocalY = standingHeight * 0.5f + centerYOffset;
            Vector3 standingCenterWorld = transform.position + Vector3.up * standingCenterLocalY;

            float halfHeight = standingHeight * 0.5f;
            float capsuleRadius = cc.radius * 0.9f;
            Vector3 capsuleTop = standingCenterWorld + Vector3.up * (halfHeight - capsuleRadius);
            Vector3 capsuleBottom = standingCenterWorld - Vector3.up * (halfHeight - capsuleRadius);

            Collider[] hits = Physics.OverlapCapsule(capsuleBottom, capsuleTop, capsuleRadius, ceilingMask, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            if (hits != null && hits.Length > 0)
            {
                for (int i = 0; i < hits.Length; i++)
                {
                    Collider c = hits[i];
                    if (c == null) continue;
                    if (c.transform.IsChildOf(transform)) continue;
                    if (c == cc) continue;
                    blocked = true;
                    break;
                }
            }

            if (blocked)
            {
                targetHeight = cc.height;
            }
        }

        currentHeight = Mathf.SmoothDamp(cc.height, targetHeight, ref heightVelocity, Mathf.Max(0.0001f, heightSmoothTime));
        currentHeight = Mathf.Clamp(currentHeight, 0.1f, standingHeight);

        float prevCenterX = cc.center.x;
        float prevCenterZ = cc.center.z;

        cc.height = currentHeight;
        cc.center = new Vector3(prevCenterX, cc.height * 0.5f + centerYOffset, prevCenterZ);

        float lowestWorldYAfter = cc.bounds.min.y;
        float deltaY = lowestWorldYBefore - lowestWorldYAfter;
        if (Mathf.Abs(deltaY) > 0.00001f)
        {
            transform.position += Vector3.up * deltaY;
        }

        if (playerCamera != null)
        {
            bool cameraIsChildOfBody = (bodyTransform != null) && playerCamera.transform.IsChildOf(bodyTransform);
            float camTargetY;
            if (cameraIsChildOfBody)
            {
                camTargetY = cameraStandLocalY;
            }
            else
            {
                camTargetY = cameraStandLocalY - (standingHeight - currentHeight);
            }

            Vector3 camLocal = playerCamera.transform.localPosition;
            camLocal.y = Mathf.SmoothDamp(camLocal.y, camTargetY, ref cameraYVelocity, Mathf.Max(0.0001f, heightSmoothTime));
            cameraBaseLocalPos = camLocal;
            playerCamera.transform.localPosition = cameraBaseLocalPos;
        }

        if (bodyTransform != null)
        {
            float targetBodyLocalY = bodyInitialLocalPos.y - (standingHeight - currentHeight);
            Vector3 bodyLocal = bodyTransform.localPosition;
            bodyLocal.y = Mathf.SmoothDamp(bodyLocal.y, targetBodyLocalY, ref bodyYVelocity, Mathf.Max(0.0001f, heightSmoothTime));
            bodyTransform.localPosition = bodyLocal;
        }
    }

    void HandleLean()
    {
        if (bodyTransform == null) return;

        bool wantLean = useCustomKeys ? GetKey(key_lean) : Input.GetKey(KeyCode.Q);
        float targetAngle = wantLean ? leanAngle : 0f;

        Quaternion targetRot = Quaternion.Euler(targetAngle, 0f, 0f);
        bodyTransform.localRotation = Quaternion.Slerp(bodyTransform.localRotation, targetRot, Time.deltaTime * leanSpeed);
    }

    void ResolveGroundPenetration()
    {
        Vector3 capsuleWorldCenter = transform.position + cc.center;
        float maxCheck = cc.height;

        RaycastHit hit;
        if (Physics.Raycast(capsuleWorldCenter, Vector3.down, out hit, maxCheck + 0.1f, ~0, QueryTriggerInteraction.Ignore))
        {
            float desiredBottomY = hit.point.y;
            float currentBottomY = cc.bounds.min.y;
            float penetration = desiredBottomY - currentBottomY;
            if (penetration > 0.001f)
            {
                transform.position += Vector3.up * penetration;
            }
        }
    }

    void HandleHeadBob()
    {
        if (!enableHeadBob || playerCamera == null) return;

        bool isCrouching = useCustomKeys ? GetKey(key_crouch) : (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
        if (disableBobOnCrouch && isCrouching)
        {
            playerCamera.transform.localPosition = Vector3.SmoothDamp(playerCamera.transform.localPosition, cameraBaseLocalPos, ref cameraBobVelocity, 1f / Mathf.Max(0.0001f, bobSmoothness));
            bobTimer = 0f;
            return;
        }

        float horizontalSpeed = new Vector3(currentHorizontalVelocity.x, 0f, currentHorizontalVelocity.z).magnitude;
        bool isMoving = cc.isGrounded && horizontalSpeed > footstepMoveThreshold;
        bool isSprinting = useCustomKeys ? GetKey(key_sprint) : (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        if (!isMoving)
        {
            playerCamera.transform.localPosition = Vector3.SmoothDamp(playerCamera.transform.localPosition, cameraBaseLocalPos, ref cameraBobVelocity, 1f / Mathf.Max(0.0001f, bobSmoothness));
            bobTimer = 0f;
            return;
        }

        float freq = isSprinting ? sprintBobFrequency : walkBobFrequency;
        float height = isSprinting ? sprintBobHeight : walkBobHeight;
        float speedForTarget = isSprinting ? sprintSpeed : walkSpeed;
        float speedFactor = Mathf.Clamp01(horizontalSpeed / Mathf.Max(0.01f, speedForTarget));

        bobTimer += Time.deltaTime * freq * (0.5f + 0.5f * speedFactor);

        float bobPosY = Mathf.Sin(bobTimer * Mathf.PI * 2f) * height * (0.5f + 0.5f * speedFactor);
        float bobPosX = Mathf.Cos(bobTimer * Mathf.PI * 2f) * bobSwayAmount * (0.2f + 0.8f * speedFactor);

        Vector3 targetLocalPos = cameraBaseLocalPos + new Vector3(bobPosX, bobPosY, 0f);
        playerCamera.transform.localPosition = Vector3.SmoothDamp(playerCamera.transform.localPosition, targetLocalPos, ref cameraBobVelocity, 1f / Mathf.Max(0.0001f, bobSmoothness));
    }

    void HandleFootsteps(bool isSprinting, bool isCrouching)
    {
        if (!HasAnyFootstepClips() || footstepSource == null)
            return;

        Vector3 delta = transform.position - lastPosition;
        float dt = Mathf.Max(Time.deltaTime, 1e-6f);
        float horizontalSpeed = new Vector3(delta.x, 0f, delta.z).magnitude / dt;
        bool isMoving = cc.isGrounded && horizontalSpeed > footstepMoveThreshold;
        float baseInterval = isSprinting ? sprintStepInterval : (isCrouching ? crouchStepInterval : walkStepInterval);

        if (!wasMovingLastFrame && isMoving)
            footstepTimer = baseInterval * 0.5f;

        if (!isMoving)
        {
            footstepTimer = 0f;
            wasMovingLastFrame = false;
            return;
        }

        float speedForTarget = (isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed));
        float speedFactor = Mathf.Clamp(horizontalSpeed / Mathf.Max(0.01f, speedForTarget), 0.05f, 2f);
        float interval = baseInterval / speedFactor;

        footstepTimer -= Time.deltaTime;
        if (footstepTimer <= 0f)
        {
            PlayFootstepSound(isSprinting, isCrouching);
            footstepTimer = interval;
        }

        wasMovingLastFrame = true;
    }

    void PlayFootstepSound(bool isSprinting, bool isCrouching)
    {
        if (!HasAnyFootstepClips() || footstepSource == null)
            return;

        AudioClip[] clipsToUse = null;
        if (isSprinting && sprintFootstepClips != null && sprintFootstepClips.Length > 0)
            clipsToUse = sprintFootstepClips;
        else if (isCrouching && crouchFootstepClips != null && crouchFootstepClips.Length > 0)
            clipsToUse = crouchFootstepClips;
        else if (!isSprinting && !isCrouching && walkFootstepClips != null && walkFootstepClips.Length > 0)
            clipsToUse = walkFootstepClips;
        else
            clipsToUse = footstepClips;

        if (clipsToUse == null || clipsToUse.Length == 0)
            return;

        int idx = UnityEngine.Random.Range(0, clipsToUse.Length);
        AudioClip clip = clipsToUse[idx];
        if (clip == null) return;

        footstepSource.PlayOneShot(clip, footstepVolume);
    }

    void PlaySfx(AudioClip[] clips)
    {
        if ((clips == null || clips.Length == 0)) return;

        AudioSource src = sfxSource != null ? sfxSource : footstepSource;
        if (src == null) return;

        int idx = UnityEngine.Random.Range(0, clips.Length);
        AudioClip clip = clips[idx];
        if (clip == null) return;

        src.PlayOneShot(clip, footstepVolume);
    }

    bool HasAnyFootstepClips()
    {
        return (footstepClips != null && footstepClips.Length > 0)
            || (walkFootstepClips != null && walkFootstepClips.Length > 0)
            || (sprintFootstepClips != null && sprintFootstepClips.Length > 0)
            || (crouchFootstepClips != null && crouchFootstepClips.Length > 0);
    }

    bool HasAnySfxClips()
    {
        return (jumpClips != null && jumpClips.Length > 0) || (landClips != null && landClips.Length > 0);
    }

    void OnDrawGizmosSelected()
    {
        if (cc == null) cc = GetComponent<CharacterController>();
        if (cc == null) return;

        Gizmos.color = Color.yellow;
        Vector3 worldHeadPos = transform.position + Vector3.up * (standingHeight - cc.radius);
        Gizmos.DrawWireSphere(worldHeadPos, cc.radius * 0.9f);
    }

    public void SetBinding(string actionName, KeyCode key)
    {
        if (string.IsNullOrEmpty(actionName)) return;
        string canonical = null;

        switch (actionName)
        {
            case "Row_up":
            case "Row_up ":
            case "up":
            case "move_forward":
                key_forward = key; canonical = "Row_up"; break;
            case "Row_down":
            case "down":
            case "move_back":
                key_back = key; canonical = "Row_down"; break;
            case "Row_left":
            case "left":
            case "move_left":
                key_left = key; canonical = "Row_left"; break;
            case "Row_right":
            case "right":
            case "move_right":
                key_right = key; canonical = "Row_right"; break;
            case "Row_sprint":
            case "sprint":
                key_sprint = key; canonical = "Row_sprint"; break;
            case "Row_crought":
            case "Row_crouch":
            case "crouch":
                key_crouch = key; canonical = "Row_crought"; break;
            case "Row_jump":
            case "jump":
                key_jump = key; canonical = "Row_jump"; break;
            case "Row_action":
            case "action":
                key_action = key; canonical = "Row_action"; break;
            case "Row_naklon":
            case "naklon":
            case "lean":
                key_lean = key; canonical = "Row_naklon"; break;
            case "Row_pickup":
            case "pickup":
            case "grab":
                key_pickup = key; canonical = "Row_pickup"; break;
            case "Row_rotate":
            case "rotate":
            case "turn":
                key_rotate = key; canonical = "Row_rotate"; break;
            case "Row_throw":
            case "throw":
                key_throw = key; canonical = "Row_throw"; break;
            default:
                Debug.LogWarning($"FPSController.SetBinding: Unknown action '{actionName}'. Binding ignored (but stored in PlayerPrefs).");
                break;
        }
        try
        {
            if (!string.IsNullOrEmpty(canonical))
                BindingChanged?.Invoke(canonical, key);
            else
                BindingChanged?.Invoke(actionName, key);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"FPSController: BindingChanged handler threw: {ex}");
        }
    }

    public KeyCode GetBinding(string actionName)
    {
        switch (actionName)
        {
            case "Row_up": case "up": case "move_forward": return key_forward;
            case "Row_down": case "down": case "move_back": return key_back;
            case "Row_left": case "left": case "move_left": return key_left;
            case "Row_right": case "right": case "move_right": return key_right;
            case "Row_sprint": case "sprint": return key_sprint;
            case "Row_crought": case "Row_crouch": case "crouch": return key_crouch;
            case "Row_jump": case "jump": return key_jump;
            case "Row_action": case "action": return key_action;
            case "Row_naklon": case "naklon": case "lean": return key_lean;
            case "Row_pickup": case "pickup": case "grab": return key_pickup;
            case "Row_rotate": case "rotate": case "turn": return key_rotate;
            case "Row_throw": case "throw": return key_throw;
            default:
                return KeyCode.None;
        }
    }
}