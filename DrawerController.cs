using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// DrawerController — двигает объект между закрытой и открытой позициями вдоль выбранной локальной оси.
/// Управление активацией выполняется по кнопке действия, которую назначает игрок в настройках (например "Row_action").
/// Поддерживает звук открытия/закрытия и отображение подсказки в UI (TextMeshPro) с текущей назначенной кнопкой.
/// Исправление: звук открытия/закрытия теперь воспроизводится только один раз при старте соответствующего движения,
/// даже если пользователь повторно нажимает кнопку во время самого движения.
/// </summary>
public class DrawerController : MonoBehaviour
{
    [Header("Movement")]
    public float openDistance = 0.3f;
    public float speed = 1.5f;
    public Vector3 localMoveAxis = Vector3.forward;
    public bool useLocalSpace = true;
    public bool startOpen = false;

    [Header("Activation / Player binding")]
    [Tooltip("Имя действия в вашем ControlsSettings (по умолчанию Row_action)")]
    public string actionName = "Row_action";
    [Tooltip("Если true — игрок должен быть в радиусе activationDistance для активации кнопкой")]
    public bool requirePlayerInRange = true;
    public float activationDistance = 2f;

    [Header("Sound (optional)")]
    public AudioClip openClip;
    public AudioClip closeClip;
    public AudioSource audioSource;

    [Header("UI Prompt (optional)")]
    public TextMeshProUGUI promptLabel;
    public GameObject promptContainer;

    // internal
    private Vector3 closedPos;
    private Vector3 openPos;
    private bool isOpen;
    private Coroutine moveRoutine;

    // binding / player reference
    private KeyCode actionKey = KeyCode.E;
    private FPSController fpsController;
    private Transform playerTransform;

    // prompt cache
    private string cachedLabelText = null;
    private bool cachedPromptVisible = false;

    // --- Sound-play guards: ensure open/close sound plays only once per movement start ---
    private bool soundPlayedOpen = false;
    private bool soundPlayedClose = false;

    void Awake()
    {
        if (useLocalSpace) closedPos = transform.localPosition;
        else closedPos = transform.position;

        Vector3 dir = localMoveAxis.normalized;
        Vector3 offset = dir * openDistance;
        if (useLocalSpace) openPos = closedPos + offset;
        else openPos = closedPos + transform.TransformDirection(offset);

        isOpen = startOpen;
        if (isOpen)
        {
            if (useLocalSpace) transform.localPosition = openPos;
            else transform.position = openPos;
        }
        else
        {
            if (useLocalSpace) transform.localPosition = closedPos;
            else transform.position = closedPos;
        }

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        SetPromptActive(false);
    }

    void OnEnable()
    {
        fpsController = FindObjectOfType<FPSController>();
        if (fpsController != null)
        {
            actionKey = fpsController.GetBinding(actionName);
            playerTransform = fpsController.transform;
        }
        else
        {
            int stored = PlayerPrefs.GetInt("key_" + actionName, (int)actionKey);
            actionKey = (KeyCode)stored;
        }

        FPSController.BindingChanged += OnBindingChanged;
        UpdatePromptImmediately();
    }

    void OnDisable()
    {
        FPSController.BindingChanged -= OnBindingChanged;
    }

    void OnBindingChanged(string changedAction, KeyCode newKey)
    {
        if (string.Equals(changedAction, actionName, System.StringComparison.OrdinalIgnoreCase))
        {
            actionKey = newKey;
            UpdatePromptImmediately();
        }
    }

    void Update()
    {
        if (playerTransform == null)
            playerTransform = FindObjectOfType<FPSController>()?.transform;

        UpdatePrompt();

        if (actionKey != KeyCode.None)
        {
            if (Input.GetKeyDown(actionKey))
            {
                if (requirePlayerInRange)
                {
                    if (playerTransform != null)
                    {
                        float dist = Vector3.Distance(playerTransform.position, transform.position);
                        if (dist <= activationDistance)
                            Toggle();
                    }
                    else
                    {
                        Toggle();
                    }
                }
                else
                {
                    Toggle();
                }
            }
        }
    }

    void UpdatePrompt()
    {
        bool shouldBeVisible = false;
        if (promptLabel != null || promptContainer != null)
        {
            if (!requirePlayerInRange)
            {
                shouldBeVisible = true;
            }
            else if (playerTransform != null)
            {
                float dist = Vector3.Distance(playerTransform.position, transform.position);
                shouldBeVisible = dist <= activationDistance;
            }
        }

        if (shouldBeVisible != cachedPromptVisible)
        {
            SetPromptActive(shouldBeVisible);
            cachedPromptVisible = shouldBeVisible;
        }

        if (shouldBeVisible)
        {
            string label = GetKeyDisplayLabel(actionKey);
            if (label != cachedLabelText)
            {
                if (promptLabel != null) promptLabel.text = label;
                cachedLabelText = label;
            }
        }
    }

    void UpdatePromptImmediately()
    {
        cachedLabelText = null;
        cachedPromptVisible = !cachedPromptVisible;
        UpdatePrompt();
    }

    void SetPromptActive(bool active)
    {
        if (promptContainer != null)
            promptContainer.SetActive(active);
        else if (promptLabel != null)
            promptLabel.gameObject.SetActive(active);
    }

    string GetKeyDisplayLabel(KeyCode kc)
    {
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
            case KeyCode.Mouse2: return "СКМ";
        }

        string s = kc.ToString();
        if (s.StartsWith("Alpha") && s.Length > 5)
            return s.Substring(5);
        if (s.StartsWith("Keypad") && s.Length > 6)
            return s.Substring(6);

        if (kc == KeyCode.UpArrow) return "?";
        if (kc == KeyCode.DownArrow) return "?";
        if (kc == KeyCode.LeftArrow) return "?";
        if (kc == KeyCode.RightArrow) return "?";

        return s;
    }

    // --- Movement API ---
    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveToTarget(true));
    }

    public void Close()
    {
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveToTarget(false));
    }

    private IEnumerator MoveToTarget(bool targetOpen)
    {
        Vector3 target = targetOpen ? openPos : closedPos;

        // --- sound-play guard: play only once per start of this movement direction ---
        if (targetOpen)
        {
            if (!soundPlayedOpen)
            {
                if (openClip != null) PlayClip(openClip);
                soundPlayedOpen = true;
            }
            // reset opposite flag so next close will play
            soundPlayedClose = false;
        }
        else
        {
            if (!soundPlayedClose)
            {
                if (closeClip != null) PlayClip(closeClip);
                soundPlayedClose = true;
            }
            soundPlayedOpen = false;
        }

        while (true)
        {
            Vector3 current = useLocalSpace ? (Vector3)transform.localPosition : (Vector3)transform.position;
            if (Vector3.Distance(current, target) <= 0.0005f)
            {
                if (useLocalSpace) transform.localPosition = target;
                else transform.position = target;
                break;
            }

            Vector3 next = Vector3.MoveTowards(current, target, speed * Time.deltaTime);
            if (useLocalSpace) transform.localPosition = next;
            else transform.position = next;

            yield return null;
        }

        isOpen = targetOpen;
        moveRoutine = null;
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null) return;
        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
        else
        {
            GameObject go = new GameObject("TempAudio");
            go.transform.position = transform.position;
            var src = go.AddComponent<AudioSource>();
            src.spatialBlend = 1f;
            src.PlayOneShot(clip);
            Destroy(go, clip.length + 0.1f);
        }
    }

    void OnMouseDown()
    {
        Toggle();
    }
}