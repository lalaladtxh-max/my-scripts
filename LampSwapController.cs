using UnityEngine;

/// <summary>
/// Управляет визуальной заменой лампы (transparent / opaque) и переключением/установкой состояния одного или нескольких
/// LightController-ов. Сохранена обратная совместимость: поле lightController оставлено, добавлено additionalLightControllers.
/// Поведение:
/// - Если initializeFromLight == true, начальное состояние берётся из lightController (если задан),
///   иначе из первого available additionalLightControllers.
/// - ToggleLamp/SetState применяют операцию к main lightController и ко всем additionalLightControllers.
/// - ApplyState по-прежнему переключает opaque/transparent GameObject-ы для визуального эффекта.
/// </summary>
public class LampSwapController : MonoBehaviour
{
    [Tooltip("LightController, отвечающий за физический источник/состояние света (обратная совместимость)")]
    public LightController lightController;

    [Tooltip("Дополнительные LightController, которые нужно синхронно переключать вместе с main (можно оставить пустым)")]
    public LightController[] additionalLightControllers;

    [Tooltip("GameObject прозрачной версии лампы (например, 'off' визуал)")]
    public GameObject transparentLamp;

    [Tooltip("GameObject непрозрачной версии лампы (например, 'on' визуал)")]
    public GameObject opaqueLamp;

    [Tooltip("Если true - начальное состояние будет инициализировано с LightController'ов, иначе будет использоваться initialOnState")]
    public bool initializeFromLight = true;

    [Tooltip("Если initializeFromLight == false, использовать это значение для начальной установки")]
    public bool initialOnState = false;

    void Reset()
    {
        // Сохранён старый behaviour: искать LightController в дочерних объектах
        if (lightController == null)
            lightController = GetComponentInChildren<LightController>();
    }

    void Start()
    {
        bool on = initialOnState;

        if (initializeFromLight)
        {
            // Попытаться получить состояние из main контроллера
            if (lightController != null)
            {
                on = GetControllerState(lightController);
            }
            else if (additionalLightControllers != null)
            {
                // или из первого доступного дополнительного контроллера
                foreach (var lc in additionalLightControllers)
                {
                    if (lc == null) continue;
                    on = GetControllerState(lc);
                    break;
                }
            }
        }

        ApplyState(on);
    }

    /// <summary>
    /// Переключить лампу(ы) — инвертировать текущее состояние (определяется по main контроллеру или первому дополнительному).
    /// </summary>
    public void ToggleLamp()
    {
        bool isOn = false;
        bool determined = false;

        if (initializeFromLight)
        {
            if (lightController != null)
            {
                isOn = GetControllerState(lightController);
                determined = true;
            }
            else if (additionalLightControllers != null)
            {
                foreach (var lc in additionalLightControllers)
                {
                    if (lc == null) continue;
                    isOn = GetControllerState(lc);
                    determined = true;
                    break;
                }
            }
        }

        if (!determined)
        {
            // Fallback: посмотреть на opaqueLamp.activeSelf
            if (opaqueLamp != null)
            {
                isOn = opaqueLamp.activeSelf;
            }
            else
            {
                // Нечего переключать — выйти
                return;
            }
        }

        SetState(!isOn);
    }

    /// <summary>
    /// Установить состояние для всех контроллеров и обновить визуал.
    /// </summary>
    public void SetState(bool on)
    {
        if (lightController != null)
            lightController.SetState(on);

        if (additionalLightControllers != null)
        {
            foreach (var lc in additionalLightControllers)
            {
                if (lc == null) continue;
                lc.SetState(on);
            }
        }

        ApplyState(on);
    }

    void ApplyState(bool on)
    {
        if (opaqueLamp != null)
            opaqueLamp.SetActive(on);
        if (transparentLamp != null)
            transparentLamp.SetActive(!on);
    }

    public void TurnOn() => SetState(true);
    public void TurnOff() => SetState(false);

    // Вспомогательная функция: получить состояние конкретного LightController подобно старой логике
    bool GetControllerState(LightController lc)
    {
        if (lc == null) return false;
        if (lc.useEnabledProperty)
            return lc.targetLight != null ? lc.targetLight.enabled : false;
        else
            return lc.targetGameObject != null ? lc.targetGameObject.activeSelf : false;
    }
}