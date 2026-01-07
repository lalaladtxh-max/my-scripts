using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// —крипт "«вук".
/// Master volume управл€етс€ через AudioListener.volume (masterSlider).
/// musicSlider и musicToggle теперь управл€ют PersistentMusicController (если он есть) Ч это гарантирует, что
/// после выключени€ музыки через настройки другие кнопки/скрипты не смогут включить еЄ обратно до тех пор,
/// пока вы снова не включите еЄ в настройках.
/// </summary>
public class AudioSettings : MonoBehaviour
{
    [Header("UI")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Toggle musicToggle;
    public Button backButton;

    // ћожно назначить вручную в инспекторе. ≈сли не назначен Ч код попытаетс€ найти объект по имени persistentMusicObjectName.
    [Header("Music source (optional)")]
    public AudioSource persistentMusicSource;
    public string persistentMusicObjectName = "PersistentMusic_Player";

    const string KEY_MASTER = "settings_masterVolume";
    const string KEY_MUSIC = "settings_musicVolume";
    const string KEY_MUSIC_ON = "settings_musicOn";

    void Awake()
    {
        if (masterSlider != null) masterSlider.onValueChanged.AddListener(SetMasterVolume);
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(SetMusicVolume);
        if (musicToggle != null) musicToggle.onValueChanged.AddListener(SetMusicOn);
        if (backButton != null) backButton.onClick.AddListener(OnBack);
    }

    void OnEnable()
    {
        LoadToUI();
    }

    void LoadToUI()
    {
        float master = PlayerPrefs.GetFloat(KEY_MASTER, 1f);
        float music = PlayerPrefs.GetFloat(KEY_MUSIC, 1f);
        bool musicOn = PlayerPrefs.GetInt(KEY_MUSIC_ON, 1) == 1;

        if (masterSlider != null) masterSlider.value = master;
        if (musicSlider != null) musicSlider.value = music;
        if (musicToggle != null) musicToggle.isOn = musicOn;

        ApplyImmediate();
    }

    void ApplyImmediate()
    {
        float master = masterSlider != null ? masterSlider.value : PlayerPrefs.GetFloat(KEY_MASTER, 1f);
        AudioListener.volume = master;

        float musicVol = musicSlider != null ? musicSlider.value : PlayerPrefs.GetFloat(KEY_MUSIC, 1f);

        // ≈сли есть контроллер Ч используем его; иначе Ч работаем с persistentMusicSource (старое поведение)
        var pmc = FindObjectOfType<PersistentMusicController>();
        if (pmc != null)
        {
            pmc.SetVolume(musicVol);
            if (musicToggle != null)
                pmc.SetAllowed(musicToggle.isOn);
            else
                pmc.SetAllowed(PlayerPrefs.GetInt(KEY_MUSIC_ON, 1) == 1);
        }
        else
        {
            var src = GetPersistentMusicSource();
            if (src != null)
            {
                src.volume = musicVol;
                bool musicOn = musicToggle != null ? musicToggle.isOn : PlayerPrefs.GetInt(KEY_MUSIC_ON, 1) == 1;
                if (!musicOn)
                {
                    if (src.isPlaying) src.Pause();
                }
                else
                {
                    if (!src.isPlaying) src.UnPause();
                }
            }
        }
    }

    public void SetMasterVolume(float v)
    {
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(KEY_MASTER, v);
    }

    public void SetMusicVolume(float v)
    {
        var pmc = FindObjectOfType<PersistentMusicController>();
        if (pmc != null)
        {
            pmc.SetVolume(v);
        }
        else
        {
            var src = GetPersistentMusicSource();
            if (src != null) src.volume = v;
        }
        PlayerPrefs.SetFloat(KEY_MUSIC, v);
        PlayerPrefs.Save();
    }

    public void SetMusicOn(bool on)
    {
        var pmc = FindObjectOfType<PersistentMusicController>();
        if (pmc != null)
        {
            pmc.SetAllowed(on);
        }
        else
        {
            var src = GetPersistentMusicSource();
            if (src != null)
            {
                if (on)
                {
                    if (!src.isPlaying) src.UnPause();
                }
                else
                {
                    if (src.isPlaying) src.Pause();
                }
            }
            PlayerPrefs.SetInt(KEY_MUSIC_ON, on ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public void ResetDefaults()
    {
        float defaultMaster = 1f;
        float defaultMusic = 1f;
        bool defaultMusicOn = true;

        if (masterSlider != null) masterSlider.value = defaultMaster;
        if (musicSlider != null) musicSlider.value = defaultMusic;
        if (musicToggle != null) musicToggle.isOn = defaultMusicOn;

        PlayerPrefs.SetFloat(KEY_MASTER, defaultMaster);
        PlayerPrefs.SetFloat(KEY_MUSIC, defaultMusic);
        PlayerPrefs.SetInt(KEY_MUSIC_ON, defaultMusicOn ? 1 : 0);
        PlayerPrefs.Save();
        ApplyImmediate();
    }

    void OnBack()
    {
        var parent = FindObjectOfType<SettingsMenuController>();
        parent?.BackToMain();
    }

    // ¬озвращает AudioSource дл€ persistent music, кэширует попытку поиска
    AudioSource GetPersistentMusicSource()
    {
        if (persistentMusicSource != null) return persistentMusicSource;

        if (!string.IsNullOrEmpty(persistentMusicObjectName))
        {
            var go = GameObject.Find(persistentMusicObjectName);
            if (go != null)
            {
                var src = go.GetComponent<AudioSource>();
                if (src != null)
                {
                    persistentMusicSource = src;
                    return persistentMusicSource;
                }
            }
        }

        return null;
    }
}