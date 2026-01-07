using UnityEngine;

[AddComponentMenu("Interaction/LightController")]
public class LightController : MonoBehaviour
{
    [Tooltip("Если указан — будет переключаться именно этот компонент Light. Если не указан, будет попытка взять Light у этого GameObject.")]
    public Light targetLight;

    [Tooltip("Альтернативно можно управлять включением/выключением всего GameObject (SetActive). Если true — будет переключаться именно Light.enabled; если false — будет переключаться activeSelf у выбранного gameObject.")]
    public bool useEnabledProperty = true;

    [Tooltip("Если useEnabledProperty == false, можно указать GameObject, который нужно включать/выключать. По умолчанию используется этот объект.")]
    public GameObject targetGameObject;

    void Reset()
    {
        // по умолчанию пробуем прикрепить Light на том же объекте
        if (targetLight == null)
            targetLight = GetComponent<Light>();
        if (targetGameObject == null)
            targetGameObject = this.gameObject;
    }

    // Переключить текущее состояние
    public void Toggle()
    {
        if (useEnabledProperty)
        {
            if (targetLight == null)
                targetLight = GetComponent<Light>();
            if (targetLight == null) return;
            targetLight.enabled = !targetLight.enabled;
        }
        else
        {
            GameObject go = targetGameObject != null ? targetGameObject : this.gameObject;
            go.SetActive(!go.activeSelf);
        }
    }

    // Установить состояние явно
    public void SetState(bool on)
    {
        if (useEnabledProperty)
        {
            if (targetLight == null)
                targetLight = GetComponent<Light>();
            if (targetLight == null) return;
            targetLight.enabled = on;
        }
        else
        {
            GameObject go = targetGameObject != null ? targetGameObject : this.gameObject;
            go.SetActive(on);
        }
    }

    public void TurnOn() => SetState(true);
    public void TurnOff() => SetState(false);
}