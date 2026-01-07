using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class InteractableButton : MonoBehaviour
{
    [Header("Activation")]
    public KeyCode activationKey = KeyCode.E;

    [Tooltip("Optional: if set, this is the action name used by ControlsSettings / FPSController (e.g. 'Row_action' or 'action').\n" +
             "If provided, the button will initialize its activationKey from the current binding and update when bindings change.")]
    public string actionName = "";

    [Header("Target to animate")]
    public Transform pressTransform;

    [Header("Animation (fallback)")]
    public bool useAnimator = false;
    public Animator animator;
    public string animatorPressTrigger = "Press";
    public Vector3 pressScale = new Vector3(0.9f, 0.9f, 0.9f);
    public float pressDuration = 0.12f;

    [Header("Sound")]
    public AudioClip pressSound;
    public AudioSource audioSource;

    [Header("Output / Callbacks")]
    public UnityEvent onPressed;

    [Header("Music pause settings")]
    public bool enableMusicPause = true;
    public string musicTargetTag = "MusicTarget";

    // Для отслеживания состояния паузы
    private AudioSource musicSource;
    private bool musicWasPlayingBeforePause = false;

    ObjectHighlighter highlighter;
    bool isPressing = false;
    Vector3 initialLocalScale;

    // fallback persistent name used by LoadingScreenController
    const string persistentMusicName = "PersistentMusic_Player";

    // cached set of normalized action name variants for matching
    List<string> actionVariants = new List<string>();

    void Awake()
    {
        highlighter = GetComponent<ObjectHighlighter>();
        if (highlighter == null)
            highlighter = GetComponentInChildren<ObjectHighlighter>();

        if (pressTransform == null)
        {
            MeshRenderer mr = GetComponentInChildren<MeshRenderer>();
            if (mr != null) pressTransform = mr.transform;
            else
            {
                SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null) pressTransform = smr.transform;
            }
        }
        if (pressTransform == null)
            pressTransform = this.transform;

        if (useAnimator && animator == null)
        {
            animator = pressTransform.GetComponent<Animator>();
            if (animator == null)
                animator = GetComponent<Animator>();
        }

        initialLocalScale = pressTransform.localScale;

        if (audioSource == null && pressSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        BuildActionVariants();
    }

    void BuildActionVariants()
    {
        actionVariants.Clear();
        if (string.IsNullOrEmpty(actionName)) return;

        string baseName = actionName.Trim();

        // canonical variants:
        actionVariants.Add(baseName);
        actionVariants.Add(baseName.ToLowerInvariant());
        actionVariants.Add(baseName.ToUpperInvariant());

        // If not already contains Row_ prefix, add with prefix
        if (!baseName.StartsWith("Row_", StringComparison.OrdinalIgnoreCase))
        {
            actionVariants.Add("Row_" + baseName);
            actionVariants.Add(("Row_" + baseName).ToLowerInvariant());
        }
        else
        {
            // add without prefix
            var noPrefix = baseName.StartsWith("Row_", StringComparison.OrdinalIgnoreCase) ? baseName.Substring(4) : baseName;
            actionVariants.Add(noPrefix);
            actionVariants.Add(noPrefix.ToLowerInvariant());
        }

        // normalize by removing spaces for comparisons
        for (int i = 0; i < actionVariants.Count; i++)
            actionVariants[i] = actionVariants[i].Replace(" ", "");
    }

    void OnEnable()
    {
        // Subscribe to binding changes so we update activationKey when the player rebinds keys
        try
        {
            FPSController.BindingChanged += OnBindingChanged;
        }
        catch { }

        // Initialize activationKey from FPSController or PlayerPrefs if actionName provided
        if (!string.IsNullOrEmpty(actionName))
        {
            // Prefer FPSController if present
            var fps = FindObjectOfType<FPSController>();
            if (fps != null)
            {
                // Try multiple variants to get binding
                KeyCode found = KeyCode.None;
                foreach (var v in actionVariants)
                {
                    found = fps.GetBinding(v);
                    if (found != KeyCode.None) break;
                }
                if (found != KeyCode.None)
                {
                    activationKey = found;
                }
                else
                {
                    // Fallback to PlayerPrefs with different key names
                    int stored = TryReadPrefsForVariants(actionVariants, (int)activationKey);
                    activationKey = (KeyCode)stored;
                }
            }
            else
            {
                int stored = TryReadPrefsForVariants(actionVariants, (int)activationKey);
                activationKey = (KeyCode)stored;
            }
        }
    }

    void OnDisable()
    {
        try
        {
            FPSController.BindingChanged -= OnBindingChanged;
        }
        catch { }
    }

    int TryReadPrefsForVariants(List<string> variants, int defaultVal)
    {
        if (variants == null || variants.Count == 0) return defaultVal;
        foreach (var v in variants)
        {
            if (string.IsNullOrEmpty(v)) continue;
            string key = "key_" + v;
            // Try exact key
            if (PlayerPrefs.HasKey(key))
            {
                return PlayerPrefs.GetInt(key, defaultVal);
            }
            // Also try with original case
            string alt = "key_" + actionName;
            if (PlayerPrefs.HasKey(alt))
            {
                return PlayerPrefs.GetInt(alt, defaultVal);
            }
        }
        // last resort: try "key_action" common name
        if (PlayerPrefs.HasKey("key_Row_action")) return PlayerPrefs.GetInt("key_Row_action", defaultVal);
        if (PlayerPrefs.HasKey("key_action")) return PlayerPrefs.GetInt("key_action", defaultVal);
        return defaultVal;
    }

    // Invoked when FPSController broadcasts a binding change
    void OnBindingChanged(string changedAction, KeyCode newKey)
    {
        if (string.IsNullOrEmpty(actionName)) return;

        string normalizedChanged = (changedAction ?? "").Replace(" ", "").ToLowerInvariant();

        foreach (var v in actionVariants)
        {
            if (string.Equals(v.ToLowerInvariant(), normalizedChanged, StringComparison.OrdinalIgnoreCase))
            {
                activationKey = newKey;
                return;
            }
            // also compare without Row_ prefix
            var vNoRow = v.StartsWith("Row_", StringComparison.OrdinalIgnoreCase) ? v.Substring(4) : v;
            if (string.Equals(vNoRow.ToLowerInvariant(), normalizedChanged, StringComparison.OrdinalIgnoreCase))
            {
                activationKey = newKey;
                return;
            }
            // or changedAction may be full canonical, compare both sides without prefix
            var changedNoRow = normalizedChanged.StartsWith("row_") ? normalizedChanged.Substring(4) : normalizedChanged;
            if (string.Equals(v.ToLowerInvariant(), changedNoRow, StringComparison.OrdinalIgnoreCase))
            {
                activationKey = newKey;
                return;
            }
        }
    }

    void Start()
    {
        if (!enableMusicPause) return;

        if (string.IsNullOrWhiteSpace(musicTargetTag))
            musicTargetTag = "MusicTarget";
        GameObject[] musTargets = null;
        try
        {
            musTargets = GameObject.FindGameObjectsWithTag(musicTargetTag);
        }
        catch
        {
            musTargets = null;
        }
        if (musTargets != null && musTargets.Length > 0)
            musicSource = musTargets[0].GetComponent<AudioSource>();
        else
            musicSource = null;
    }

    void Update()
    {
        bool highlighted = (highlighter != null && highlighter.IsHighlighted);

        if (highlighted && !isPressing)
        {
            if (Input.GetKeyDown(activationKey))
            {
                StartCoroutine(DoPress());
            }
        }
    }

    IEnumerator DoPress()
    {
        isPressing = true;

        if (useAnimator && animator != null)
        {
            animator.SetTrigger(animatorPressTrigger);
            yield return new WaitForSeconds(Mathf.Max(0.01f, pressDuration));
        }
        else
        {
            float half = pressDuration * 0.5f;
            Vector3 from = pressTransform.localScale;
            Vector3 to = Vector3.Scale(initialLocalScale, pressScale);
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                pressTransform.localScale = Vector3.Lerp(from, to, t / half);
                yield return null;
            }
            pressTransform.localScale = to;
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                pressTransform.localScale = Vector3.Lerp(to, initialLocalScale, t / half);
                yield return null;
            }
            pressTransform.localScale = initialLocalScale;
        }

        if (pressSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(pressSound);
        }
        else if (pressSound != null)
        {
            AudioSource.PlayClipAtPoint(pressSound, transform.position);
        }

        if (enableMusicPause) // Только если разрешено
            TogglePauseResumeMusic();

        onPressed?.Invoke();

        yield return new WaitForSeconds(0.05f);
        isPressing = false;
    }

    void TogglePauseResumeMusic()
    {
        if (string.IsNullOrEmpty(musicTargetTag))
            musicTargetTag = "MusicTarget";

        // If we don't have a cached musicSource, try to find it by tag first
        if (musicSource == null)
        {
            GameObject[] targets = null;
            try
            {
                targets = GameObject.FindGameObjectsWithTag(musicTargetTag);
            }
            catch
            {
                targets = null;
            }

            if (targets != null && targets.Length > 0)
            {
                musicSource = targets[0].GetComponent<AudioSource>();
            }
        }

        // Fallback: if still null, try persistent object created by LoadingScreenController
        if (musicSource == null)
        {
            var persistent = GameObject.Find(persistentMusicName);
            if (persistent != null)
            {
                musicSource = persistent.GetComponent<AudioSource>();
            }
        }

        if (musicSource == null) return;

        if (musicSource.isPlaying)
        {
            musicSource.Pause();
            musicWasPlayingBeforePause = true;
        }
        else if (musicWasPlayingBeforePause)
        {
            musicSource.UnPause();
            musicWasPlayingBeforePause = false;
        }
        else if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    void OnValidate()
    {
        if (pressDuration < 0.02f) pressDuration = 0.02f;
        if (pressScale.x <= 0f) pressScale.x = 0.01f;
        if (pressScale.y <= 0f) pressScale.y = 0.01f;
        if (pressScale.z <= 0f) pressScale.z = 0.01f;
    }
}