using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Автоматически ищет объект PersistentMusic (по имени или по тегу) и при появлении добавляет к нему PersistentMusicController.
/// Подойдёт когда объект музыки создаётся динамически из предыдущей сцены.
/// </summary>
public class PersistentMusicAutoAttach : MonoBehaviour
{
    [Tooltip("Имя игрового объекта с музыкой. По умолчанию: PersistentMusic_Player")]
    public string persistentMusicObjectName = "PersistentMusic_Player";

    [Tooltip("Альтернативно можно искать по тегу (оставьте пустым, чтобы не использовать).")]
    public string persistentMusicTag = "";

    [Tooltip("Интервал между попытками найти объект (секунд).")]
    public float pollInterval = 0.5f;

    [Tooltip("Максимальное время ожидания в секундах. 0 — ждать бесконечно.")]
    public float timeoutSeconds = 30f;

    Coroutine searchCoroutine;

    void OnEnable()
    {
        searchCoroutine = StartCoroutine(SearchAndAttach());
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        if (searchCoroutine != null) StopCoroutine(searchCoroutine);
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (searchCoroutine != null) StopCoroutine(searchCoroutine);
        searchCoroutine = StartCoroutine(SearchAndAttach());
    }

    IEnumerator SearchAndAttach()
    {
        float elapsed = 0f;
        while (true)
        {
            GameObject found = null;
            if (!string.IsNullOrEmpty(persistentMusicTag))
            {
                var objs = GameObject.FindGameObjectsWithTag(persistentMusicTag);
                if (objs != null && objs.Length > 0) found = objs[0];
            }

            if (found == null && !string.IsNullOrEmpty(persistentMusicObjectName))
            {
                found = GameObject.Find(persistentMusicObjectName);
            }

            if (found != null)
            {
                var existing = found.GetComponent<PersistentMusicController>();
                if (existing == null)
                {
                    var ctrl = found.AddComponent<PersistentMusicController>();
                    var src = found.GetComponent<AudioSource>();
                    if (src != null) ctrl.audioSource = src;
                }
                yield break;
            }

            if (timeoutSeconds > 0f && elapsed >= timeoutSeconds)
            {
                Debug.LogWarning($"PersistentMusicAutoAttach: не найден объект музыки '{persistentMusicObjectName}' (таймаут).");
                yield break;
            }

            yield return new WaitForSeconds(pollInterval);
            elapsed += pollInterval;
        }
    }
}