using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ControlsSettings:
/// - хранит привязки в PlayerPrefs (key_<actionName>)
/// - применяет привязки в FPSController (runtime)
/// - хранит/отдаёт чувствительность мыши
/// - предоставляет метод GetKeyFor для чтения текущего KeyCode привязки
/// - предоставляет метод GetKeyLabel для получения текстового представления клавиши (удобно для подсказок)
/// </summary>
public class ControlsSettings : MonoBehaviour
{
    [Serializable]
    public class BindingUI
    {
        public string actionName;               // имя действия (например "Row_up" и т.п.)
        public Button changeButton;             // кнопка "Change"
        public TextMeshProUGUI keyLabel;        // label для отображения текущей привязки в UI настроек
        public KeyCode defaultKey = KeyCode.None;
    }

    public BindingUI[] bindings;
    public Button backButton;

    // UI для мыши (опционально)
    [Header("Mouse sensitivity UI (optional)")]
    public Slider mouseSensitivitySlider;
    public TextMeshProUGUI mouseSensitivityLabel;

    public const string PREFIX = "key_";
    const string SENSITIVITY_KEY = "mouse_sensitivity";

    bool waitingForKey = false;
    BindingUI currentBinding = null;
    FPSController fpsController; // ссылка на FPSController (runtime)

    void Awake()
    {
        // подписываем кнопки UI
        foreach (var b in bindings)
        {
            var local = b;
            if (local.changeButton != null)
                local.changeButton.onClick.AddListener(() => StartRebind(local));
        }

        if (backButton != null) backButton.onClick.AddListener(OnBack);

        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
        }
    }

    void OnEnable()
    {
        fpsController = FindObjectOfType<FPSController>();
        LoadToUI();
        ApplyAllBindingsToFPS();
    }

    void LoadToUI()
    {
        foreach (var b in bindings)
        {
            int stored = PlayerPrefs.GetInt(PREFIX + b.actionName, (int)b.defaultKey);
            KeyCode kc = (KeyCode)stored;
            if (b.keyLabel != null) b.keyLabel.text = kc.ToString();
        }

        if (mouseSensitivitySlider != null)
        {
            float defaultSens = (fpsController != null) ? fpsController.mouseSensitivity : 2f;
            float storedSens = PlayerPrefs.GetFloat(SENSITIVITY_KEY, defaultSens);
            mouseSensitivitySlider.SetValueWithoutNotify(storedSens);
            if (mouseSensitivityLabel != null) mouseSensitivityLabel.text = storedSens.ToString("0.00");
        }
    }

    void StartRebind(BindingUI b)
    {
        if (waitingForKey) return;
        waitingForKey = true;
        currentBinding = b;
        if (b.keyLabel != null) b.keyLabel.text = "...";
        StartCoroutine(WaitForKey());
    }

    IEnumerator WaitForKey()
    {
        while (!Input.anyKeyDown)
            yield return null;

        KeyCode detected = KeyCode.None;
        foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKeyDown(kc))
            {
                detected = kc;
                break;
            }
        }

        if (currentBinding != null)
        {
            if (detected != KeyCode.None)
            {
                if (IsKeyAlreadyAssigned(detected, currentBinding.actionName))
                {
                    Debug.Log("Клавиша уже назначена");
                    if (currentBinding.keyLabel != null)
                    {
                        currentBinding.keyLabel.text = "Клавиша занята";
                        int stored = PlayerPrefs.GetInt(PREFIX + currentBinding.actionName, (int)currentBinding.defaultKey);
                        StartCoroutine(RestoreLabelAfterDelay(currentBinding, 1.2f, stored));
                    }
                }
                else
                {
                    // сохраняем
                    PlayerPrefs.SetInt(PREFIX + currentBinding.actionName, (int)detected);
                    PlayerPrefs.Save();

                    // обновляем UI
                    if (currentBinding.keyLabel != null) currentBinding.keyLabel.text = detected.ToString();

                    // применяем в FPSController
                    ApplyBindingToFPS(currentBinding.actionName, detected);
                }
            }
            else
            {
                int stored = PlayerPrefs.GetInt(PREFIX + currentBinding.actionName, (int)currentBinding.defaultKey);
                if (currentBinding.keyLabel != null) currentBinding.keyLabel.text = ((KeyCode)stored).ToString();
            }
        }

        waitingForKey = false;
        currentBinding = null;
    }

    IEnumerator RestoreLabelAfterDelay(BindingUI binding, float delay, int storedKey)
    {
        yield return new WaitForSeconds(delay);
        if (binding != null && binding.keyLabel != null)
            binding.keyLabel.text = ((KeyCode)storedKey).ToString();
    }

    bool IsKeyAlreadyAssigned(KeyCode kc, string exceptActionName)
    {
        if (kc == KeyCode.None) return false;

        // 1) проверяем PlayerPrefs (сохранённые привязки)
        foreach (var b in bindings)
        {
            if (string.Equals(b.actionName, exceptActionName, StringComparison.Ordinal)) continue;
            int stored = PlayerPrefs.GetInt(PREFIX + b.actionName, (int)b.defaultKey);
            if ((KeyCode)stored == kc) return true;
        }

        // 2) проверяем runtime-привязки в FPSController
        if (fpsController != null)
        {
            foreach (var b in bindings)
            {
                if (string.Equals(b.actionName, exceptActionName, StringComparison.Ordinal)) continue;
                KeyCode runtime = fpsController.GetBinding(b.actionName);
                if (runtime == kc) return true;
            }
        }

        return false;
    }

    void ApplyBindingToFPS(string actionName, KeyCode kc)
    {
        if (fpsController == null)
        {
            fpsController = FindObjectOfType<FPSController>();
            if (fpsController == null) return;
        }

        fpsController.SetBinding(actionName, kc);
        // FPSController.SetBinding должен вызвать BindingChanged в своём коде, если нужно оповестить UI
    }

    void ApplyAllBindingsToFPS()
    {
        if (fpsController == null)
            fpsController = FindObjectOfType<FPSController>();
        if (fpsController == null) return;

        foreach (var b in bindings)
        {
            int stored = PlayerPrefs.GetInt(PREFIX + b.actionName, (int)b.defaultKey);
            fpsController.SetBinding(b.actionName, (KeyCode)stored);
        }

        float storedSens = PlayerPrefs.GetFloat(SENSITIVITY_KEY, fpsController.mouseSensitivity);
        fpsController.mouseSensitivity = storedSens;
    }

    public void ResetDefaults()
    {
        foreach (var b in bindings)
        {
            PlayerPrefs.SetInt(PREFIX + b.actionName, (int)b.defaultKey);
            if (b.keyLabel != null) b.keyLabel.text = b.defaultKey.ToString();
            ApplyBindingToFPS(b.actionName, b.defaultKey);
        }

        if (fpsController != null)
        {
            float defaultSens = fpsController.mouseSensitivity;
            PlayerPrefs.SetFloat(SENSITIVITY_KEY, defaultSens);
            if (mouseSensitivitySlider != null) mouseSensitivitySlider.SetValueWithoutNotify(defaultSens);
            if (mouseSensitivityLabel != null) mouseSensitivityLabel.text = defaultSens.ToString("0.00");
        }

        PlayerPrefs.Save();
    }

    void OnBack()
    {
        var parent = FindObjectOfType<SettingsMenuController>();
        parent?.BackToMain();
    }

    // Получить KeyCode для actionName (runtime)
    public KeyCode GetKeyFor(string actionName)
    {
        int stored = PlayerPrefs.GetInt(PREFIX + actionName, (int)KeyCode.None);
        return (KeyCode)stored;
    }

    /// <summary>
    /// Удобный метод для получения текстовой подсказки по клавише.
    /// Возвращает строку, которую удобно показывать в UI (например "F", "E", "Пробел", "Ctrl", "ЛКМ", "ПКМ").
    /// </summary>
    public string GetKeyLabel(string actionName)
    {
        KeyCode kc = GetKeyFor(actionName);
        if (kc == KeyCode.None) return "";
        // нормализация некоторых кейсов, включая русские аббревиатуры для мыши
        switch (kc)
        {
            case KeyCode.Space: return "Пробел";
            case KeyCode.LeftControl:
            case KeyCode.RightControl: return "Ctrl";
            case KeyCode.LeftAlt:
            case KeyCode.RightAlt: return "Alt";
            case KeyCode.LeftShift:
            case KeyCode.RightShift: return "Shift";
            case KeyCode.Mouse0: return "ЛКМ"; // левая кнопка мыши
            case KeyCode.Mouse1: return "ПКМ"; // правая кнопка мыши
            default:
                // Для букв и цифр оставляем просто ToString (например "F", "E", "1")
                return kc.ToString();
        }
    }

    // --- Mouse sensitivity handling ---
    void OnMouseSensitivityChanged(float value)
    {
        if (mouseSensitivityLabel != null) mouseSensitivityLabel.text = value.ToString("0.00");

        PlayerPrefs.SetFloat(SENSITIVITY_KEY, value);
        PlayerPrefs.Save();

        if (fpsController == null)
            fpsController = FindObjectOfType<FPSController>();
        if (fpsController != null)
        {
            fpsController.mouseSensitivity = value;
        }
    }
}