using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Loading screen controller (TextMeshPro version).
/// ...
/// </summary>
[RequireComponent(typeof(Canvas))]
public class LoadingScreenController : MonoBehaviour
{
    [Header("UI Elements (assign in Inspector)")]
    public TextMeshProUGUI studioText;
    public TextMeshProUGUI supportText;
    public TextMeshProUGUI presentText;
    public Image logoImage;
    public TextMeshProUGUI pressAnyKeyText;

    [Header("Audio")]
    public AudioClip logoSound;
    public AudioClip musicClip;
    public AudioSource sfxSource;
    public AudioSource musicSource;

    [Header("Music behavior")]
    public bool persistMusic = false;
    public float musicStartDelay = 0.05f;
    [Range(0f, 1f)]
    public float musicVolume = 1f;

    [Header("Music binding behavior")]
    public string musicTargetTag = "MusicTarget";
    public bool bindMusicToAllScenes = false;

    [Header("Scene")]
    public string sceneToLoad = "GameScene";

    [Header("Timings (seconds)")]
    public float initialDelay = 0.5f;
    public float textFadeDuration = 1.0f;
    public float textHoldDuration = 0.8f;
    public float betweenTextsDelay = 0.25f;
    public float logoFadeDuration = 0.25f;
    public float logoScaleFrom = 0.6f;
    public float logoScaleTo = 1f;
    public float logoPopDuration = 0.22f;
    public float afterLogoDelay = 0.5f;

    [Header("Press any key")]
    public float pressBlinkMin = 0.1f;
    public float pressBlinkMax = 1f;
    public float pressBlinkSpeed = 2f;

    [Header("Final transition")]
    public Image fadeOverlay;
    public float finalFadeDuration = 0.6f;

    // Internal
    CanvasGroup studioCg, supportCg, presentCg, logoCg, pressCg, overlayCg;
    Canvas mainCanvas;

    bool allowInput = false;
    Coroutine pressBlinkCoroutine;

    GameObject tempMusicGO;

    const string persistentMusicName = "PersistentMusic_Player";

    void Awake()
    {
        mainCanvas = GetComponent<Canvas>();
        if (mainCanvas == null)
            mainCanvas = gameObject.AddComponent<Canvas>();

        if (sfxSource == null && logoSound != null)
        {
            GameObject sfxGo = new GameObject("LoadingSFX");
            sfxGo.transform.SetParent(transform, false);
            sfxSource = sfxGo.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f; // 2D for UI
        }

        studioCg = EnsureCanvasGroup(studioText?.gameObject);
        supportCg = EnsureCanvasGroup(supportText?.gameObject);
        presentCg = EnsureCanvasGroup(presentText?.gameObject);
        logoCg = EnsureCanvasGroup(logoImage?.gameObject);
        pressCg = EnsureCanvasGroup(pressAnyKeyText?.gameObject);

        if (fadeOverlay == null)
            CreateDefaultOverlay();
        overlayCg = EnsureCanvasGroup(fadeOverlay.gameObject);

        SetAlphaInstant(studioCg, 0f);
        SetAlphaInstant(supportCg, 0f);
        SetAlphaInstant(presentCg, 0f);
        SetAlphaInstant(logoCg, 0f);
        SetAlphaInstant(pressCg, 0f);

        if (logoImage != null)
            logoImage.transform.localScale = Vector3.one * logoScaleFrom;

        SetAlphaInstant(overlayCg, 1f);

        // If there is already a persistent music object but user doesn't want persistence, destroy it
        if (!persistMusic)
        {
            var existing = GameObject.Find(persistentMusicName);
            if (existing != null)
            {
                Destroy(existing);
            }
        }

        // If persistMusic && bindMusicToAllScenes, register sceneLoaded now so we will try to bind on every load.
        if (persistMusic && bindMusicToAllScenes && !string.IsNullOrEmpty(musicTargetTag))
        {
            // Ensure not double-subscribed — remove first then add
            SceneManager.sceneLoaded -= OnSceneLoadedBindMusic;
            SceneManager.sceneLoaded += OnSceneLoadedBindMusic;
        }
    }

    void Start()
    {
        StartCoroutine(SequenceCoroutine());
    }

    void Update()
    {
        if (allowInput && Input.anyKeyDown)
        {
            allowInput = false;
            if (pressBlinkCoroutine != null) StopCoroutine(pressBlinkCoroutine);
            StartCoroutine(FadeOutAndLoadScene());
        }
    }

    IEnumerator SequenceCoroutine()
    {
        yield return new WaitForSeconds(initialDelay);

        yield return StartCoroutine(FadeCanvasGroup(overlayCg, 1f, 0f, textFadeDuration * 0.7f));

        if (studioCg != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(studioCg, 0f, 1f, textFadeDuration));
            yield return new WaitForSeconds(textHoldDuration);
            yield return StartCoroutine(FadeCanvasGroup(studioCg, 1f, 0f, textFadeDuration));
        }

        yield return new WaitForSeconds(betweenTextsDelay);

        if (supportCg != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(supportCg, 0f, 1f, textFadeDuration));
            if (presentCg != null)
            {
                yield return new WaitForSeconds(betweenTextsDelay);
                yield return StartCoroutine(FadeCanvasGroup(presentCg, 0f, 1f, textFadeDuration));
            }

            yield return new WaitForSeconds(textHoldDuration);

            if (supportCg != null) StartCoroutine(FadeCanvasGroup(supportCg, 1f, 0f, textFadeDuration));
            if (presentCg != null) StartCoroutine(FadeCanvasGroup(presentCg, 1f, 0f, textFadeDuration));
            yield return new WaitForSeconds(betweenTextsDelay + textFadeDuration * 0.2f);
        }

        if (logoCg != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(logoCg, 0f, 1f, logoFadeDuration));

            if (logoImage != null)
            {
                float t = 0f;
                float from = logoScaleFrom;
                float to = logoScaleTo;
                while (t < logoPopDuration)
                {
                    t += Time.deltaTime;
                    float p = Mathf.Clamp01(t / logoPopDuration);
                    float eased = Mathf.Sin(p * Mathf.PI * 0.5f);
                    float scale = Mathf.Lerp(from, to, eased);
                    logoImage.transform.localScale = Vector3.one * scale;
                    yield return null;
                }
                logoImage.transform.localScale = Vector3.one * to;
            }

            StartCoroutine(PlayLogoAndThenMusic());
            yield return new WaitForSeconds(afterLogoDelay);
        }

        if (pressCg != null)
        {
            pressBlinkCoroutine = StartCoroutine(PressAnyKeyBlink());
        }
        else
        {
            allowInput = true;
        }
    }

    IEnumerator PlayLogoAndThenMusic()
    {
        if (sfxSource != null && logoSound != null)
        {
            sfxSource.PlayOneShot(logoSound);
            float wait = logoSound.length;
            if (wait > 0f) yield return new WaitForSeconds(wait + 0.02f);
        }
        else
        {
            if (musicStartDelay > 0f) yield return new WaitForSeconds(musicStartDelay);
        }

        StartMusic();
    }

    void StartMusic()
    {
        if (musicClip == null) return;

        if (persistMusic)
        {
            var existing = GameObject.Find(persistentMusicName);
            if (existing != null)
            {
                var existingSrc = existing.GetComponent<AudioSource>();
                if (existingSrc != null)
                {
                    if (!existingSrc.isPlaying) existingSrc.Play();
                    existingSrc.volume = musicVolume;
                }
                return;
            }

            GameObject go = new GameObject(persistentMusicName);
            var src = go.AddComponent<AudioSource>();
            src.clip = musicClip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = musicVolume;
            go.transform.SetParent(null);
            DontDestroyOnLoad(go);
            src.Play();

            // add follower component so we can bind persistent audio to objects on scene load if requested
            var follower = go.AddComponent<PersistentMusicFollower>();
            follower.spatialFollow = false;

            // if bindMusicToAllScenes was enabled and a target exists in current scene, bind immediately
            if (bindMusicToAllScenes && !string.IsNullOrEmpty(musicTargetTag))
            {
                GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(musicTargetTag);
                if (taggedObjects.Length > 0)
                {
                    var target = taggedObjects[0];
                    // make it 3D and follow
                    src.spatialBlend = 1f;
                    src.transform.position = target.transform.position;
                    follower.spatialFollow = true;
                    follower.target = target.transform;
                }
            }
        }
        else
        {
            if (musicSource != null)
            {
                musicSource.clip = musicClip;
                musicSource.loop = true;
                musicSource.playOnAwake = false;
                musicSource.spatialBlend = 0f;
                musicSource.volume = musicVolume;
                musicSource.Play();
            }
            else
            {
                if (tempMusicGO == null)
                {
                    tempMusicGO = new GameObject("LoadingMusic_Temp");
                    tempMusicGO.transform.SetParent(transform, false);
                    var src = tempMusicGO.AddComponent<AudioSource>();
                    src.clip = musicClip;
                    src.loop = true;
                    src.playOnAwake = false;
                    src.spatialBlend = 0f;
                    src.volume = musicVolume;
                    src.Play();
                }
            }
        }
    }

    IEnumerator PressAnyKeyBlink()
    {
        yield return StartCoroutine(FadeCanvasGroup(pressCg, 0f, 1f, 0.35f));
        yield return new WaitForSeconds(0.15f);
        allowInput = true;

        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * pressBlinkSpeed;
            float a = Mathf.Lerp(pressBlinkMin, pressBlinkMax, (Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f));
            pressCg.alpha = a;
            yield return null;
        }
    }

    IEnumerator FadeOutAndLoadScene()
    {
        if (!persistMusic)
        {
            if (tempMusicGO != null)
            {
                var src = tempMusicGO.GetComponent<AudioSource>();
                if (src != null) src.Stop();
                Destroy(tempMusicGO);
                tempMusicGO = null;
            }

            var existing = GameObject.Find(persistentMusicName);
            if (existing != null)
            {
                var src = existing.GetComponent<AudioSource>();
                if (src != null) src.Stop();
                Destroy(existing);
            }

            if (musicSource != null && musicSource.isPlaying)
            {
                musicSource.Stop();
            }
        }

        yield return StartCoroutine(FadeCanvasGroup(overlayCg, overlayCg.alpha, 1f, finalFadeDuration));

        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            if (persistMusic && !string.IsNullOrEmpty(musicTargetTag) && !bindMusicToAllScenes)
            {
                SceneManager.sceneLoaded += OnSceneLoadedBindMusic;
            }
            SceneManager.LoadScene(sceneToLoad);
        }
    }

    // Safely bind/copy/detach persistent audio to target in newly loaded scene
    void OnSceneLoadedBindMusic(Scene scene, LoadSceneMode mode)
    {
        if (!bindMusicToAllScenes)
            SceneManager.sceneLoaded -= OnSceneLoadedBindMusic;

        GameObject persistent = GameObject.Find(persistentMusicName);
        var src = persistent?.GetComponent<AudioSource>();

        // Try to find a target in the newly loaded scene that is not the persistent object
        GameObject target = null;
        GameObject[] taggedObjects = null;
        try
        {
            if (!string.IsNullOrEmpty(musicTargetTag))
                taggedObjects = GameObject.FindGameObjectsWithTag(musicTargetTag);
        }
        catch
        {
            taggedObjects = new GameObject[0];
        }

        if (taggedObjects != null && taggedObjects.Length > 0)
        {
            // Prefer objects that belong to the loaded scene and are not the persistent GO
            foreach (var go in taggedObjects)
            {
                if (go == persistent) continue;
                if (go.scene == scene)
                {
                    target = go;
                    break;
                }
            }

            // If none in the new scene, pick any tagged object that is not persistent
            if (target == null)
            {
                foreach (var go in taggedObjects)
                {
                    if (go == persistent) continue;
                    target = go;
                    break;
                }
            }
        }

        if (target == null || src == null)
        {
            // nothing to do
            return;
        }

        if (bindMusicToAllScenes)
        {
            // Make the persistent a spatial follower to the target (3D)
            src.spatialBlend = 1f;
            src.transform.position = target.transform.position;

            var follower = persistent.GetComponent<PersistentMusicFollower>();
            if (follower == null) follower = persistent.AddComponent<PersistentMusicFollower>();
            follower.target = target.transform;
            follower.spatialFollow = true;
            return;
        }
        else
        {
            // Copy audio settings from persistent to target's AudioSource, then destroy persistent
            var newSrc = target.GetComponent<AudioSource>();
            if (newSrc == null) newSrc = target.AddComponent<AudioSource>();

            newSrc.clip = src.clip;
            newSrc.loop = true;
            newSrc.spatialBlend = 1f;
            newSrc.volume = src.volume;
            newSrc.playOnAwake = false;
            // safe copy of time if clip present
            try
            {
                newSrc.time = src.clip != null ? src.time : 0f;
            }
            catch { /* ignore if unavailable */ }

            if (!newSrc.isPlaying) newSrc.Play();

            Destroy(src.gameObject);
        }
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;
        float t = 0f;
        cg.alpha = from;
        if (duration <= 0f)
        {
            cg.alpha = to;
            yield break;
        }
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            cg.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, p));
            yield return null;
        }
        cg.alpha = to;
    }

    CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        if (go == null) return null;
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    void SetAlphaInstant(CanvasGroup cg, float a)
    {
        if (cg == null) return;
        cg.alpha = a;
    }

    void CreateDefaultOverlay()
    {
        GameObject go = new GameObject("FadeOverlay");
        go.transform.SetParent(transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = Color.black;
        fadeOverlay = img;
    }

    class PersistentMusicFollower : MonoBehaviour
    {
        public Transform target;
        public bool spatialFollow = false;

        void LateUpdate()
        {
            if (spatialFollow && target != null)
            {
                transform.position = target.position;
            }
        }
    }
}