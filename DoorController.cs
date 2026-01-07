using System.Collections;
using UnityEngine;

/// <summary>
/// DoorController — управляет открытием/закрытием двери.
/// Поддерживает два режима:
/// 1) useAnimator = true  — управление через Animator (trigger'ы Open/Close или Toggle).
/// 2) useAnimator = false — скриптовый поворот (rotate) вокруг локальной оси (hinge должен быть корректно установлен).
///
/// Также проигрывает звуки open/close через AudioSource (не играет автоматически при старте).
/// Методы Open(), Close(), Toggle() можно вызывать из InteractableButton.onPressed.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorController : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip("Если true — используем Animator. Иначе — плавный поворот трансформа.")]
    public bool useAnimator = false;
    public Animator animator;
    [Tooltip("Имя триггера для открытия (Animator)")]
    public string animatorOpenTrigger = "Open";
    [Tooltip("Имя триггера для закрытия (Animator)")]
    public string animatorCloseTrigger = "Close";

    [Header("Transform rotation (fallback)")]
    [Tooltip("Если используем поворот, угол открытия в градусах (от закрытого состояния)")]
    public float openAngle = 90f;
    [Tooltip("Длительность анимации открытия/закрытия в секундах (скриптовый поворот)")]
    public float openDuration = 0.6f;
    [Tooltip("Если true — угол применяется по локальной оси Y (обычно для дверей).")]
    public bool useLocalYAxis = true;

    [Header("Sound")]
    [Tooltip("Звук открытия")]
    public AudioClip openClip;
    [Tooltip("Звук закрытия")]
    public AudioClip closeClip;
    [Tooltip("Если не указан — будет использован/создан AudioSource на этом GameObject")]
    public AudioSource audioSource;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Behaviour")]
    [Tooltip("Если true — дверь закрывается при повторном нажатии (Toggle). Если false — только Open")]
    public bool allowToggle = true;
    [Tooltip("Если true — состояние isOpen сохраняется и можно закрывать")]
    public bool startOpened = false;

    // внутреннее
    bool isOpen = false;
    Quaternion closedRotation;
    Quaternion openedRotation;
    Coroutine rotateCoroutine;

    void Awake()
    {
        // AudioSource настройка: предотвращаем автозапуск
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        audioSource.playOnAwake = false;

        // запомним начальную локальную ротацию как "закрытую"
        closedRotation = transform.localRotation;

        // рассчитаем openedRotation (локально)
        if (useLocalYAxis)
        {
            openedRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        }
        else
        {
            // поворот вокруг глобальной оси Y (если нужно)
            openedRotation = Quaternion.Euler(transform.localEulerAngles + new Vector3(0f, openAngle, 0f));
        }

        isOpen = startOpened;
        transform.localRotation = isOpen ? openedRotation : closedRotation;
    }

    // Вызывается извне (например из InteractableButton.onPressed)
    public void Toggle()
    {
        if (!allowToggle)
        {
            if (!isOpen) Open();
            return;
        }

        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (useAnimator)
        {
            if (animator == null)
            {
                Debug.LogWarning($"DoorController.Open(): animator не установлен на '{gameObject.name}'");
            }
            else
            {
                animator.ResetTrigger(animatorCloseTrigger);
                animator.SetTrigger(animatorOpenTrigger);
            }
        }
        else
        {
            StartRotateTo(openedRotation);
        }

        PlaySound(openClip);
        isOpen = true;
    }

    public void Close()
    {
        if (useAnimator)
        {
            if (animator == null)
            {
                Debug.LogWarning($"DoorController.Close(): animator не установлен на '{gameObject.name}'");
            }
            else
            {
                animator.ResetTrigger(animatorOpenTrigger);
                animator.SetTrigger(animatorCloseTrigger);
            }
        }
        else
        {
            StartRotateTo(closedRotation);
        }

        PlaySound(closeClip);
        isOpen = false;
    }

    void StartRotateTo(Quaternion target)
    {
        if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
        rotateCoroutine = StartCoroutine(RotateCoroutine(target, openDuration));
    }

    IEnumerator RotateCoroutine(Quaternion target, float duration)
    {
        Quaternion start = transform.localRotation;
        float t = 0f;
        if (duration <= 0f)
        {
            transform.localRotation = target;
            yield break;
        }
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            transform.localRotation = Quaternion.Slerp(start, target, k);
            yield return null;
        }
        transform.localRotation = target;
        rotateCoroutine = null;
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, volume);
        }
    }

    // опционально — публичные геттеры
    public bool IsOpen() => isOpen;
}