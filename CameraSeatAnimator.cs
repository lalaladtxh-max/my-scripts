using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// Класс отвечает за "подъём со стула" и загрузку сцены:
/// 1) Procedural (fallback) — смещение и поворот камеры задаются через moveOffset / lookTargetOffset.
/// 2) Animator (опционально) — используется Animator с триггером animatorTrigger;
///    если Animator управляет трансформом камеры, то камера просто остаётся в том состоянии, которое выставил Animator.
///
/// Убрана зависимость от SceneCameraBinder — теперь класс только проигрывает анимацию и загружает сцену.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraSeatAnimator : MonoBehaviour
{
    [Header("Stand-up animation (procedural fallback)")]
    public float duration = 1.0f;
    public Vector3 moveOffset = new Vector3(0f, 1.2f, -0.4f); // смещение для конечной позиции камеры (local)
    public Vector3 lookTargetOffset = new Vector3(0f, 0.6f, 0.8f); // точка, на которую камера будет смотреть (local)

    [Header("Easing (procedural)")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Animator (optional)")]
    [Tooltip("Если true и есть подходящий Animator — используем его. Если animator == null, попробуем взять Animator с камеры.")]
    public bool useAnimator = false;
    [Tooltip("Animator, который содержит триггер для 'подъёма'.")]
    public Animator animator;
    [Tooltip("Имя триггера (Trigger) в Animator для начала анимации.")]
    public string animatorTrigger = "StandUp";
    [Tooltip("Таймаут ожидания перехода/окончания в Animator (в секундах).")]
    public float animatorWaitTimeout = 2.0f;
    [Tooltip("Если true — предполагается, что Animator контролирует Transform камеры. В этом случае просто загрузим сцену после анимации.")]
    public bool animatorControlsTransform = true;

    Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    /// <summary>
    /// Проигрывает анимацию подъёма и загружает сцену.
    /// </summary>
    public void PlayStandUpAndLoadScene(string sceneName)
    {
        if (cam == null) cam = GetComponent<Camera>() ?? Camera.main;
        StartCoroutine(StandUpAndLoad(sceneName));
    }

    IEnumerator StandUpAndLoad(string sceneName)
    {
        // Попытка использовать Animator, если включено
        if (useAnimator)
        {
            Animator a = animator;
            if (a == null && cam != null)
                a = cam.GetComponent<Animator>();

            if (a != null)
            {
                // Trigger the animator
                // remember initial state hash to detect state change
                AnimatorStateInfo initialInfo = a.GetCurrentAnimatorStateInfo(0);
                int initialHash = initialInfo.fullPathHash;

                a.ResetTrigger(animatorTrigger);
                a.SetTrigger(animatorTrigger);

                float timer = 0f;
                bool stateChanged = false;

                // Wait for animator to transition to a different state (or until timeout)
                while (timer < animatorWaitTimeout)
                {
                    if (!a.IsInTransition(0))
                    {
                        AnimatorStateInfo info = a.GetCurrentAnimatorStateInfo(0);
                        if (info.fullPathHash != initialHash)
                        {
                            stateChanged = true;
                            break;
                        }
                    }
                    timer += Time.deltaTime;
                    yield return null;
                }

                if (stateChanged)
                {
                    // Now wait for the current state to finish (normalizedTime >= 1) or timeout
                    float innerTimer = 0f;
                    while (innerTimer < animatorWaitTimeout)
                    {
                        if (!a.IsInTransition(0))
                        {
                            AnimatorStateInfo info = a.GetCurrentAnimatorStateInfo(0);
                            // normalizedTime >= 1f indicates the state completed at least one full cycle (works for non-looping clips)
                            if (info.normalizedTime >= 1f)
                                break;
                        }
                        innerTimer += Time.deltaTime;
                        yield return null;
                    }
                }
                else
                {
                    // State didn't change within timeout; we still wait small extra time to allow clip to play a little
                    float extraWait = Mathf.Min(0.25f, animatorWaitTimeout);
                    yield return new WaitForSeconds(extraWait);
                }

                // Если Animator управляет Transform — просто загрузим сцену.
                if (animatorControlsTransform)
                {
                    SceneManager.LoadScene(sceneName);
                    yield break;
                }
                // Иначе — упадём в процедурный код ниже, который выставит финальное положение камеры.
            }
            // если Animator не найден — fallback к процедурной версии
        }

        // Procedural animation (original behaviour)
        Transform t = cam.transform;
        Vector3 fromPos = t.position;
        Quaternion fromRot = t.rotation;

        Vector3 toPos = fromPos + t.TransformVector(moveOffset);
        Vector3 lookPoint = fromPos + t.TransformVector(lookTargetOffset);
        Quaternion toRot = Quaternion.LookRotation((lookPoint - toPos).normalized, Vector3.up);

        float timerProc = 0f;
        while (timerProc < duration)
        {
            timerProc += Time.deltaTime;
            float p = Mathf.Clamp01(timerProc / duration);
            float ep = ease.Evaluate(p);
            t.position = Vector3.Lerp(fromPos, toPos, ep);
            t.rotation = Quaternion.Slerp(fromRot, toRot, ep);
            yield return null;
        }
        t.position = toPos;
        t.rotation = toRot;

        // Без SceneCameraBinder: просто загружаем сцену.
        SceneManager.LoadScene(sceneName);
    }
}