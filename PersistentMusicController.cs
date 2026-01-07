using UnityEngine;

/// <summary>
/// Контроллер для PersistentMusic_Player.
/// При отключении музыки: сохраняет текущую позицию (timeSamples) и ставит музыку на паузу.
/// При включении: восстанавливает позицию и продолжает воспроизведение.
/// Если кто-то извне попытается включить музыку, пока она запрещена — контроллер моментально снова поставит на паузу,
/// при этом сохранённая позиция не перезаписывается.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PersistentMusicController : MonoBehaviour
{
    const string KEY_MUSIC_ON = "settings_musicOn";
    const string KEY_MUSIC = "settings_musicVolume";

    public AudioSource audioSource; // можно назначить вручную; если null — берём компонент на объекте

    AudioClip originalClip;
    int pausedSample = -1; // сохранённая позиция в samples, -1 — нет сохранённой позиции
    bool allowed = true;
    bool pausedSampleSet = false; // флаг: позиция сохранена при выключении

    void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            originalClip = audioSource.clip;
        }
    }

    void Start()
    {
        // Применим состояние и громкость из PlayerPrefs при старте
        allowed = PlayerPrefs.GetInt(KEY_MUSIC_ON, 1) == 1;
        float vol = PlayerPrefs.GetFloat(KEY_MUSIC, audioSource != null ? audioSource.volume : 1f);
        if (audioSource != null) audioSource.volume = vol;

        if (!allowed)
        {
            // если музыка запрещена, поставим на паузу и сохраним позицию
            if (audioSource != null && audioSource.isPlaying)
            {
                pausedSample = audioSource.timeSamples;
                pausedSampleSet = true;
                audioSource.Pause();
            }
        }
    }

    void Update()
    {
        // Если музыка запрещена, не даём никому запустить её (будем немедленно ставить на паузу).
        if (!allowed && audioSource != null)
        {
            if (audioSource.isPlaying)
            {
                // Если позиция ещё не сохранена (крайний случай), то сохраняем её перед паузой
                if (!pausedSampleSet)
                {
                    pausedSample = audioSource.timeSamples;
                    pausedSampleSet = true;
                }
                audioSource.Pause();
            }
        }
    }

    // Устанавливает разрешение музыки и сразу сохраняет в PlayerPrefs
    public void SetAllowed(bool isAllowed)
    {
        if (audioSource == null)
        {
            // всё равно сохраним флаг, чтобы другие сцены знали состояние
            PlayerPrefs.SetInt(KEY_MUSIC_ON, isAllowed ? 1 : 0);
            PlayerPrefs.Save();
            allowed = isAllowed;
            return;
        }

        // если уже в нужном состоянии — ничего не делаем
        if (allowed == isAllowed) return;

        allowed = isAllowed;
        PlayerPrefs.SetInt(KEY_MUSIC_ON, allowed ? 1 : 0);
        PlayerPrefs.Save();

        if (!allowed)
        {
            // сохранить текущую позицию и поставить на паузу
            if (audioSource.isPlaying)
            {
                pausedSample = audioSource.timeSamples;
                pausedSampleSet = true;
                audioSource.Pause();
            }
            else
            {
                // если не играет — запомним текущ позицию (на будущее)
                pausedSample = audioSource.timeSamples;
                pausedSampleSet = true;
            }
        }
        else
        {
            // разрешаем — восстановим позицию и воспроизводим (если есть клип)
            if (originalClip != null && audioSource.clip != originalClip)
            {
                audioSource.clip = originalClip;
            }

            if (pausedSampleSet && audioSource.clip != null)
            {
                // защитим от выхода за границы
                int maxSamples = audioSource.clip.samples;
                if (pausedSample >= maxSamples) pausedSample = 0;
                audioSource.timeSamples = Mathf.Clamp(pausedSample, 0, Mathf.Max(0, audioSource.clip.samples - 1));
                audioSource.Play();
                pausedSample = -1;
                pausedSampleSet = false;
            }
            else
            {
                // если позиция не была сохранена — просто продолжаем (ничего не делаем)
                // если нужно всегда начать воспроизведение при разрешении, можно вызвать audioSource.Play() здесь
            }
        }
    }

    // Установить громкость музыки (сохраняет в PlayerPrefs)
    public void SetVolume(float v)
    {
        if (audioSource != null)
        {
            audioSource.volume = v;
        }
        PlayerPrefs.SetFloat(KEY_MUSIC, v);
        PlayerPrefs.Save();
    }

    // Возвращает текущее разрешение на воспроизведение музыки
    public bool IsAllowed()
    {
        return allowed;
    }

    // Возвращает сохранённую позицию (debug/для UI)
    public int GetPausedSample() => pausedSample;
}