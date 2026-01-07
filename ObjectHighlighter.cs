using UnityEngine;
using TMPro;

/// <summary>
/// ObjectHighlighter
/// - Показывает подсказку клавиши действия при наведении (глазом/центром или мышкой)
/// </summary>
[RequireComponent(typeof(Collider))]
public class ObjectHighlighter : MonoBehaviour
{
    [Header("Info")]
    public string objectName = "Object";
    public string actionName = "Row_action";
    public ControlsSettings controlsSettings;
    public TextMeshProUGUI hintLabel;
    public float maxInteractDistance = 3f;
    public Camera playerCamera;

    [Header("Detection")]
    public bool useGaze = true;
    [Tooltip("Радиус для SphereCast. 0 – использовать Raycast.")]
    public float raycastRadius = 0.1f;
    public LayerMask interactMask = ~0;

    // Состояния
    bool isHighlighted = false;
    public bool IsHighlighted { get { return isHighlighted; } }
    KeyCode lastKey = KeyCode.None;
    bool isHovering = false;

    // Для корректной работы hintLabel при нескольких объектах
    static ObjectHighlighter currentLabelOwner = null;

    Collider cachedCollider;

    // Позволяет внешнему коду (например, GazeInteractor) явно включать подсветку
    bool forcedHighlight = false;
    float forcedHighlightTime = -1;

    void Awake()
    {
        cachedCollider = GetComponent<Collider>();
        SetHighlighted(false);
    }

    void Start()
    {
        if (controlsSettings == null)
            controlsSettings = FindObjectOfType<ControlsSettings>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        ClearHint();
        lastKey = KeyCode.None;
    }

    void Update()
    {
        // Приоритет — если нам явно внешним скриптом указали SetHighlighted(true)
        if (forcedHighlight)
        {
            // Если подсветка включена извне, не занимаемся кастами и удерживаем подсветку пока не отменят
            if (!isHovering)
            {
                isHovering = true;
                UpdateHintImmediate();
            }
            return;
        }

        if (useGaze)
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null) return;
            }

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit hit;
            bool hitThis = false;
            float hitDistance = float.MaxValue;

            if (raycastRadius > 0f)
            {
                if (Physics.SphereCast(ray, raycastRadius, out hit, maxInteractDistance, interactMask, QueryTriggerInteraction.Ignore))
                {
                    hitDistance = hit.distance;
                    if (hit.collider != null && IsColliderForThisObject(hit.collider)) hitThis = true;
                }
            }
            else
            {
                if (Physics.Raycast(ray, out hit, maxInteractDistance, interactMask, QueryTriggerInteraction.Ignore))
                {
                    hitDistance = hit.distance;
                    if (hit.collider != null && IsColliderForThisObject(hit.collider)) hitThis = true;
                }
            }

            if (hitThis)
            {
                OnHoverEnterGaze();
            }
            else
            {
                OnHoverExitGaze();
            }
        }

        if (isHovering)
        {
            KeyCode current = GetCurrentKey();
            if (current != lastKey)
            {
                UpdateHintImmediate();
            }
        }
    }

    bool IsColliderForThisObject(Collider c)
    {
        return c != null && (c.gameObject == gameObject || c.transform.IsChildOf(transform));
    }

    // Вызовется когда наведение "глазом" (луч)
    void OnHoverEnterGaze()
    {
        if (!isHovering && !forcedHighlight)
        {
            isHovering = true;
            UpdateHintImmediate();
        }
    }

    void OnHoverExitGaze()
    {
        if (isHovering && !forcedHighlight)
        {
            isHovering = false;
            ClearHintOwned();
        }
    }

    // Мышь для 2D-объектов или в Editor'e
    void OnMouseEnter()
    {
        if (!useGaze && !forcedHighlight)
        {
            if (IsWithinDistanceByPosition())
            {
                isHovering = true;
                UpdateHintImmediate();
            }
        }
    }

    void OnMouseExit()
    {
        if (!useGaze && !forcedHighlight)
        {
            isHovering = false;
            ClearHintOwned();
        }
    }

    bool IsWithinDistanceByPosition()
    {
        if (playerCamera == null) return true;
        float dist = Vector3.Distance(playerCamera.transform.position, transform.position);
        return dist <= maxInteractDistance;
    }

    void UpdateHintImmediate()
    {
        KeyCode kc = GetCurrentKey();
        lastKey = kc;
        string keyText = KeyCodeToLabel(kc);

        if (hintLabel != null)
        {
            currentLabelOwner = this;
            hintLabel.text = keyText ?? "";
        }
    }

    void ClearHintOwned()
    {
        if (hintLabel != null && currentLabelOwner == this)
        {
            hintLabel.text = "";
            currentLabelOwner = null;
        }
    }

    void ClearHint()
    {
        if (hintLabel != null)
        {
            if (currentLabelOwner == null || currentLabelOwner == this)
            {
                hintLabel.text = "";
                currentLabelOwner = null;
            }
        }
    }

    KeyCode GetCurrentKey()
    {
        if (controlsSettings != null)
        {
            return controlsSettings.GetKeyFor(actionName);
        }
        else
        {
            int stored = PlayerPrefs.GetInt(ControlsSettings.PREFIX + actionName, (int)KeyCode.None);
            return (KeyCode)stored;
        }
    }

    string KeyCodeToLabel(KeyCode kc)
    {
        if (controlsSettings != null)
        {
            return controlsSettings.GetKeyLabel(actionName);
        }

        if (kc == KeyCode.None) return "";
        switch (kc)
        {
            case KeyCode.Space: return "Пробел";
            case KeyCode.LeftControl:
            case KeyCode.RightControl: return "Ctrl";
            case KeyCode.LeftAlt:
            case KeyCode.RightAlt: return "Alt";
            case KeyCode.LeftShift:
            case KeyCode.RightShift: return "Shift";
            case KeyCode.Mouse0: return "ЛКМ";
            case KeyCode.Mouse1: return "ПКМ";
            default: return kc.ToString();
        }
    }

    /// <summary>
    /// Внешний вызов для установки подсветки (например, GazeInteractor)
    /// </summary>
    public void SetHighlighted(bool on)
    {
        // Управление приоритетом: если нас явно подсветили - отключаем остальные hover
        forcedHighlight = on;
        if (on)
        {
            // Вызывается при наведении из внешнего кода
            if (!isHovering)
            {
                isHovering = true;
                UpdateHintImmediate();
            }
        }
        else
        {
            if (isHovering)
            {
                isHovering = false;
                ClearHintOwned();
            }
        }
        isHighlighted = on;
    }

    public void OnHoverEnter() => OnHoverEnterGaze();
    public void OnHoverExit() => OnHoverExitGaze();

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (playerCamera == null) return;
        Gizmos.color = Color.yellow;
        Vector3 from = playerCamera.transform.position;
        Vector3 to = from + playerCamera.transform.forward * maxInteractDistance;
        if (raycastRadius > 0f)
        {
            Gizmos.DrawWireSphere(to, raycastRadius);
        }
        else
        {
            Gizmos.DrawLine(from, to);
        }
    }
#endif

    void OnValidate()
    {
        if (maxInteractDistance < 0f) maxInteractDistance = 0f;
        if (raycastRadius < 0f) raycastRadius = 0f;
    }
}