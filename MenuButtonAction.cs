using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// ????????? ??????? ???????? ?? ???????-?????? ?? ?????.
/// ?? ?????????? ??? ?????? (StartGame / ToggleMusic / OpenSettings), ??????????? ??????? ???????? ??? ?????????/???????
/// ? ???????? onPressed (????? ????? ???? ?????????? ?????????????? ??????? ????? ?????????).
/// ?????????????: ????????????? ???? ??? ????????? (hoverSound) ?????????? ????? ??? ?????.
/// 
/// »зменено: теперь скрипт оперирует визуальным Transform (visualTransform). Collider должен быть на родительском (статичном) объекте,
/// а видима€ часть и Animator Ч в дочернем объекте. “огда при анимации/масштабировании видимой части коллайдер останетс€ статичным.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MenuButtonAction : MonoBehaviour
{
    public enum ButtonType { StartGame, ToggleMusic, OpenSettings, ToggleLight, QuitGame }

    [Header("Behavior")]
    public ButtonType buttonType = ButtonType.StartGame;

    [Header("Hover / Press animation")]
    public bool useAnimator = false;
    public Animator animator;
    public string hoverTrigger = "Hover";
    public string hoverExitTrigger = "HoverExit";
    public string pressTrigger = "Press";

    [Header("Target visual transform (child). If null, first child will be used)")]
    public Transform visualTransform;

    [Header("Fallback scale animation")]
    public Vector3 hoverScale = Vector3.one * 1.08f;
    public float hoverScaleSpeed = 6f;
    public Vector3 pressScale = Vector3.one * 0.95f;
    public float pressAnimDuration = 0.12f;

    [Header("Sound (optional)")]
    [Tooltip("????, ??????? ??????????????? ??? ???????")]
    public AudioClip pressSound;
    [Tooltip("????, ??????? ??????????????? ??? ????????? (??? ?????????)")]
    public AudioClip hoverSound;
    [Tooltip("????, ??????? ??????????????? ??? ????????? (??? ???????? ???????)")]
    public AudioClip hoverExitSound;
    [Tooltip("????? AudioSource. ???? ?? ??????, ????? ?????? ????????????? ??? ?????? ??????????????? ?????")]
    public AudioSource audioSource;

    [Header("Callbacks")]
    public UnityEvent onPressed;

    Vector3 initialVisualScale;
    Coroutine pressRoutine;

    void Awake()
    {
        // ≈сли visualTransform не указан, попытаемс€ вз€ть первый дочерний объект
        if (visualTransform == null)
        {
            if (transform.childCount > 0)
                visualTransform = transform.GetChild(0);
            else
                visualTransform = transform; // fallback Ч сам объект
        }

        // сохраним начальную (визуальную) шкалу
        initialVisualScale = visualTransform.localScale;

        // ≈сли animator нужен Ч попробуем найти его сначала на visualTransform, затем на текущем объекте
        if (animator == null && useAnimator)
        {
            if (visualTransform != null)
                animator = visualTransform.GetComponent<Animator>();
            if (animator == null)
                animator = GetComponent<Animator>();
        }

        // если есть звуки, но нет AudioSource Ч создадим дефолтный (на родительском объекте)
        if (audioSource == null && (pressSound != null || hoverSound != null || hoverExitSound != null))
        {
            CreateDefaultAudioSource();
        }
    }

    // ??????? ??????? AudioSource ? ??????????? ??????????? ??? UI/????????????? ??????
    void CreateDefaultAudioSource()
    {
        if (audioSource != null) return;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f; // 2D (?? ????????? ? ???????) ? ?????? ??? UI/???? 
        audioSource.volume = 1f;
    }

    #region Hover API
    public void OnHoverEnter()
    {
        // play hover sound if provided
        if (hoverSound != null)
        {
            if (audioSource == null) CreateDefaultAudioSource();
            if (audioSource != null)
                audioSource.PlayOneShot(hoverSound);
        }

        if (useAnimator && animator != null)
        {
            animator.ResetTrigger(hoverExitTrigger);
            animator.SetTrigger(hoverTrigger);
        }
        else
        {
            StopAllCoroutines();
            StartCoroutine(ScaleTo(hoverScale, hoverScaleSpeed));
        }
    }

    public void OnHoverExit()
    {
        // play hover exit sound if provided, otherwise fallback to hoverSound
        if (hoverExitSound != null || hoverSound != null)
        {
            if (audioSource == null) CreateDefaultAudioSource();
            if (audioSource != null)
            {
                if (hoverExitSound != null)
                    audioSource.PlayOneShot(hoverExitSound);
                else
                    audioSource.PlayOneShot(hoverSound);
            }
        }

        if (useAnimator && animator != null)
        {
            animator.ResetTrigger(hoverTrigger);
            animator.SetTrigger(hoverExitTrigger);
        }
        else
        {
            StopAllCoroutines();
            StartCoroutine(ScaleTo(Vector3.one, hoverScaleSpeed)); // back to initial (1x)
        }
    }
    #endregion

    #region Click API
    public void OnClick()
    {
        // play animator or simple press animation
        if (pressRoutine != null) StopCoroutine(pressRoutine);
        pressRoutine = StartCoroutine(DoPressAnim());

        // invoke callbacks
        onPressed?.Invoke();
    }

    IEnumerator DoPressAnim()
    {
        if (useAnimator && animator != null)
        {
            animator.SetTrigger(pressTrigger);
            // don't know exact length ? small wait to allow press sound/callbacks
            yield return new WaitForSeconds(Mathf.Max(0.05f, pressAnimDuration));
        }
        else
        {
            // scale in then out (visualTransform only)
            Vector3 from = visualTransform.localScale;
            Vector3 to = Vector3.Scale(initialVisualScale, pressScale);
            float half = pressAnimDuration * 0.5f;
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                visualTransform.localScale = Vector3.Lerp(from, to, t / half);
                yield return null;
            }
            visualTransform.localScale = to;
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                visualTransform.localScale = Vector3.Lerp(to, initialVisualScale, t / half);
                yield return null;
            }
            visualTransform.localScale = initialVisualScale;
        }

        // play press sound
        if (pressSound != null)
        {
            if (audioSource == null) CreateDefaultAudioSource();
            if (audioSource != null)
            {
                audioSource.PlayOneShot(pressSound);
            }
            else
            {
                // fallback if no AudioSource found (rare)
                AudioSource.PlayClipAtPoint(pressSound, transform.position);
            }
        }
    }

    /// <summary>
    /// ??????? ?????, ????? MenuSceneController ??? ????????? ???????? ????????? ? ??????? onPressed.
    /// </summary>
    public void InvokeOnPressed()
    {
        onPressed?.Invoke();
    }
    #endregion

    IEnumerator ScaleTo(Vector3 scaleFactor, float speed)
    {
        Vector3 target = Vector3.Scale(initialVisualScale, scaleFactor);
        while ((visualTransform.localScale - target).sqrMagnitude > 0.00001f)
        {
            visualTransform.localScale = Vector3.Lerp(visualTransform.localScale, target, Time.deltaTime * speed);
            yield return null;
        }
        visualTransform.localScale = target;
    }
}