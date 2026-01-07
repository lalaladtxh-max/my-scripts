using UnityEngine;

/// <summary>
/// URP-?????: ????????? Sun: ?????? ?????????????/???? Directional Light,
/// ambient ? ?????? ???? ? ??????????? ?? ????/??????? ???.
/// ????????? ??????????/?????????????? ???? ????????? (????????????) ????????
/// ????? PlayerPrefs ? ????? singleton (DontDestroyOnLoad).
/// </summary>
[ExecuteAlways]
public class RotatingSun_URP : MonoBehaviour
{
    [Header("???????? ? ?????")]
    public float rotationSpeed = 10f;
    [Range(0, 24)] public float hourOfDay = 12f;

    [Header("???? (URP-friendly)")]
    [Tooltip("??????? ????????????? (URP: ~1..2 ??????)")]
    public float dayIntensity = 1.25f;
    [Tooltip("?????? ????????????? (???? ?????? ? 0, ?????????? 0.05..0.4)")]
    public float nightIntensity = 0.18f;
    public Color dayColor = Color.white;
    public Color nightColor = new Color(0.6f, 0.65f, 0.9f); // ??????? ??????????? ????

    [Header("????????????/??????")]
    public bool autoRotate = true;

    [Header("Ambient / moon")]
    public bool overrideAmbient = true;
    public Color dayAmbientColor = new Color(0.6f, 0.6f, 0.7f);
    [Range(0f, 1f)] public float dayAmbientIntensity = 1f;
    public Color nightAmbientColor = new Color(0.02f, 0.03f, 0.06f);
    [Range(0f, 1f)] public float nightAmbientIntensity = 0.18f;

    [Tooltip("???????????? Directional Light, ??????????? '?????'")]
    public Light moonLight;
    public Color moonColor = new Color(0.6f, 0.7f, 1f);
    [Range(0f, 2f)] public float moonIntensityAtNight = 0.2f;
    [Range(0f, 2f)] public float moonIntensityAtDay = 0f;

    [Header("????")]
    [Range(0f, 1f)] public float dayShadowStrength = 1f;
    [Range(0f, 1f)] public float nightShadowStrength = 0.5f;

    [Header("Persistence")]
    [Tooltip("???? true ? ????????? ??????????? ? PlayerPrefs ??? ??????/????? ?????.")]
    public bool persistWithPlayerPrefs = true;
    [Tooltip("??????? ?????? PlayerPrefs (???? ????? ?????????? ????????? ???????????).")]
    public string prefsKeyPrefix = "RotatingSun_";
    [Tooltip("???? true ? ?????? ?????? singleton ? ?? ??????????? ??? ???????? ?????.")]
    public bool persistAsSingleton = false;

    private Light _light;
    private bool _warned = false;

    // singleton instance (???? ??????? persistAsSingleton)
    private static RotatingSun_URP _instance;

    void Awake()
    {
        // singleton/DontDestroyOnLoad (???????????)
        if (persistAsSingleton)
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }
        }

        Init();
    }

    // >>>>>>>>>>>> ИЗМЕНЁННЫЙ МЕТОД START() <<<<<<<<<<<<<<<<<<
    void Start()
    {
        Init();
        if (Application.isPlaying)
        {
            if (persistWithPlayerPrefs)
            {
                string p = prefsKeyPrefix;
                // Если PlayerPrefs еще не содержит сохранённых значений — сохранить значения из инспектора
                if (!PlayerPrefs.HasKey(p + "rotationSpeed"))
                {
                    SaveState();
#if UNITY_EDITOR
                    Debug.Log("RotatingSun_URP: Сохранил значения из инспектора в PlayerPrefs на первом запуске этого билда.");
#endif
                }
            }
            LoadState();
        }
        UpdateSun();
    }
    // >>>>>>>>>>>> КОНЕЦ ФРАГМЕНТА <<<<<<<<<<<<<<<<<<

    void OnEnable() { Init(); UpdateSun(); }
    void OnValidate() { Init(); UpdateSun(); }

    void Init()
    {
        if (_light == null) _light = GetComponent<Light>();
    }

    void Update()
    {
        Init();
        if (_light == null)
        {
            if (!_warned) { Debug.LogWarning($"RotatingSun_URP: ??? Light ?? {name}."); _warned = true; }
            return;
        }

        // ?????? ?????????:
        // ? ??????? ?????? (autoRotate) ?????? ?? ????? ?????????? ???? (Play Mode).
        // ? ? ????????? (scene editing) ?????? ?? ????? ????????????? ?????????.
        // ? ??? ??????????? autoRotate ?????? ????? ?????? ????????? ????? hourOfDay
        //   ? ??????? ??? ? ? ?????????, ? ? Play Mode.
        if (Application.isPlaying)
        {
            if (autoRotate)
            {
                float delta = rotationSpeed * Time.deltaTime;
                transform.Rotate(Vector3.right, delta, Space.Self);
            }
            else
            {
                float angle = (hourOfDay / 24f) * 360f - 90f;
                transform.localRotation = Quaternion.Euler(angle, 0f, 0f);
            }
        }
        else
        {
            // ????? ????????? (?? Play):
            // ? ???? autoRotate == true ? ?? ??????? (????????? ??????? ????????????? ?????????).
            // ? ???? autoRotate == false ? ????????????? rotation ?? hourOfDay ??? ?????????????.
            if (!autoRotate)
            {
                float angle = (hourOfDay / 24f) * 360f - 90f;
                transform.localRotation = Quaternion.Euler(angle, 0f, 0f);
            }
        }

        UpdateSun();
    }

    private void UpdateSun()
    {
        if (_light == null) return;

        Vector3 sunDir = transform.forward;
        float dot = Vector3.Dot(sunDir, Vector3.down); // -1 .. 1
        float t = Mathf.Clamp01((dot + 0.1f) / 1.1f); // 0 = ????, 1 = ????

        // ????????????? ? ????
        float targetIntensity = Mathf.Lerp(nightIntensity, dayIntensity, t);
        _light.intensity = targetIntensity;
        _light.color = Color.Lerp(nightColor, dayColor, t);

        // ????
        if (_light.shadows != LightShadows.None)
            _light.shadowStrength = Mathf.Lerp(nightShadowStrength, dayShadowStrength, t);

        // Ambient (Built-in ? URP ????????)
        if (overrideAmbient)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            Color ambCol = Color.Lerp(nightAmbientColor, dayAmbientColor, t);
            float ambInt = Mathf.Lerp(nightAmbientIntensity, dayAmbientIntensity, t);
            RenderSettings.ambientLight = ambCol * ambInt;
        }

        // ????
        if (moonLight != null)
        {
            float moonI = Mathf.Lerp(moonIntensityAtNight, moonIntensityAtDay, t);
            moonLight.enabled = moonI > 0f;
            moonLight.intensity = moonI;
            moonLight.color = moonColor;
        }
    }

    // ----------------- ??????????/???????? ???? ????????? ????? -----------------

    // ??????????????? ?????? ??? ?????? ? ??????
    void SaveColor(string key, Color c)
    {
        PlayerPrefs.SetFloat(key + "_r", c.r);
        PlayerPrefs.SetFloat(key + "_g", c.g);
        PlayerPrefs.SetFloat(key + "_b", c.b);
        PlayerPrefs.SetFloat(key + "_a", c.a);
    }

    Color LoadColor(string key, Color defaultColor)
    {
        string kr = key + "_r";
        if (!PlayerPrefs.HasKey(kr)) return defaultColor;
        Color c = new Color(
            PlayerPrefs.GetFloat(key + "_r", defaultColor.r),
            PlayerPrefs.GetFloat(key + "_g", defaultColor.g),
            PlayerPrefs.GetFloat(key + "_b", defaultColor.b),
            PlayerPrefs.GetFloat(key + "_a", defaultColor.a)
        );
        return c;
    }

    public void SaveState()
    {
        if (!persistWithPlayerPrefs || !Application.isPlaying) return;

        string p = prefsKeyPrefix;

        // ?????????
        PlayerPrefs.SetFloat(p + "rotationSpeed", rotationSpeed);
        PlayerPrefs.SetFloat(p + "hourOfDay", hourOfDay);

        PlayerPrefs.SetFloat(p + "dayIntensity", dayIntensity);
        PlayerPrefs.SetFloat(p + "nightIntensity", nightIntensity);
        SaveColor(p + "dayColor", dayColor);
        SaveColor(p + "nightColor", nightColor);

        PlayerPrefs.SetInt(p + "autoRotate", autoRotate ? 1 : 0);

        PlayerPrefs.SetInt(p + "overrideAmbient", overrideAmbient ? 1 : 0);
        SaveColor(p + "dayAmbientColor", dayAmbientColor);
        PlayerPrefs.SetFloat(p + "dayAmbientIntensity", dayAmbientIntensity);
        SaveColor(p + "nightAmbientColor", nightAmbientColor);
        PlayerPrefs.SetFloat(p + "nightAmbientIntensity", nightAmbientIntensity);

        // moonLight: ????????? ??? ??????? (???? ????) ? ?????????
        if (moonLight != null)
        {
            PlayerPrefs.SetString(p + "moonLightName", moonLight.gameObject.name);
            PlayerPrefs.SetInt(p + "moonLightExists", 1);
            SaveColor(p + "moonColor", moonColor);
            PlayerPrefs.SetFloat(p + "moonIntensityAtNight", moonIntensityAtNight);
            PlayerPrefs.SetFloat(p + "moonIntensityAtDay", moonIntensityAtDay);
        }
        else
        {
            PlayerPrefs.SetInt(p + "moonLightExists", 0);
            PlayerPrefs.DeleteKey(p + "moonLightName");
        }

        PlayerPrefs.SetFloat(p + "dayShadowStrength", dayShadowStrength);
        PlayerPrefs.SetFloat(p + "nightShadowStrength", nightShadowStrength);

        // ????????? (????????? ??????????) ? ????????? ????????? ???? ????????
        Vector3 e = transform.localEulerAngles;
        PlayerPrefs.SetFloat(p + "rot_x", e.x);
        PlayerPrefs.SetFloat(p + "rot_y", e.y);
        PlayerPrefs.SetFloat(p + "rot_z", e.z);

        // ????? persistence (????? ? ????????? ??? ????? ???? ??????????????)
        PlayerPrefs.SetInt(p + "persistWithPlayerPrefs", persistWithPlayerPrefs ? 1 : 0);
        PlayerPrefs.SetString(p + "prefsKeyPrefix", prefsKeyPrefix);
        PlayerPrefs.SetInt(p + "persistAsSingleton", persistAsSingleton ? 1 : 0);

        PlayerPrefs.Save();
#if UNITY_EDITOR
        Debug.Log($"RotatingSun_URP: saved all inspector fields to PlayerPrefs with prefix '{p}'.");
#endif
    }

    public void LoadState()
    {
        if (!persistWithPlayerPrefs) return; // ???? ???? ???????? ? ?? ?????????

        string p = prefsKeyPrefix;

        // ????????? ?? ????? ????/rotation, ???? ?? ??????????
        if (!PlayerPrefs.HasKey(p + "hourOfDay") && !PlayerPrefs.HasKey(p + "rot_x")) return;

        // ?????????
        rotationSpeed = PlayerPrefs.GetFloat(p + "rotationSpeed", rotationSpeed);
        hourOfDay = PlayerPrefs.GetFloat(p + "hourOfDay", hourOfDay);

        dayIntensity = PlayerPrefs.GetFloat(p + "dayIntensity", dayIntensity);
        nightIntensity = PlayerPrefs.GetFloat(p + "nightIntensity", nightIntensity);
        dayColor = LoadColor(p + "dayColor", dayColor);
        nightColor = LoadColor(p + "nightColor", nightColor);

        autoRotate = PlayerPrefs.GetInt(p + "autoRotate", autoRotate ? 1 : 0) == 1;

        overrideAmbient = PlayerPrefs.GetInt(p + "overrideAmbient", overrideAmbient ? 1 : 0) == 1;
        dayAmbientColor = LoadColor(p + "dayAmbientColor", dayAmbientColor);
        dayAmbientIntensity = PlayerPrefs.GetFloat(p + "dayAmbientIntensity", dayAmbientIntensity);
        nightAmbientColor = LoadColor(p + "nightAmbientColor", nightAmbientColor);
        nightAmbientIntensity = PlayerPrefs.GetFloat(p + "nightAmbientIntensity", nightAmbientIntensity);

        int moonExists = PlayerPrefs.GetInt(p + "moonLightExists", 0);
        string moonName = PlayerPrefs.GetString(p + "moonLightName", "");
        moonColor = LoadColor(p + "moonColor", moonColor);
        moonIntensityAtNight = PlayerPrefs.GetFloat(p + "moonIntensityAtNight", moonIntensityAtNight);
        moonIntensityAtDay = PlayerPrefs.GetFloat(p + "moonIntensityAtDay", moonIntensityAtDay);

        dayShadowStrength = PlayerPrefs.GetFloat(p + "dayShadowStrength", dayShadowStrength);
        nightShadowStrength = PlayerPrefs.GetFloat(p + "nightShadowStrength", nightShadowStrength);

        // Transform
        if (PlayerPrefs.HasKey(p + "rot_x") && PlayerPrefs.HasKey(p + "rot_y") && PlayerPrefs.HasKey(p + "rot_z"))
        {
            Vector3 e = new Vector3(
                PlayerPrefs.GetFloat(p + "rot_x", transform.localEulerAngles.x),
                PlayerPrefs.GetFloat(p + "rot_y", transform.localEulerAngles.y),
                PlayerPrefs.GetFloat(p + "rot_z", transform.localEulerAngles.z)
            );
            transform.localEulerAngles = e;
        }

        // Persistence flags
        persistWithPlayerPrefs = PlayerPrefs.GetInt(p + "persistWithPlayerPrefs", persistWithPlayerPrefs ? 1 : 0) == 1;
        prefsKeyPrefix = PlayerPrefs.GetString(p + "prefsKeyPrefix", prefsKeyPrefix);
        persistAsSingleton = PlayerPrefs.GetInt(p + "persistAsSingleton", persistAsSingleton ? 1 : 0) == 1;

        // ??????? ???????????? ?????? ?? moonLight ?? ????? (???? ?? null ? ??????? ?????)
        if (moonExists == 1)
        {
            if (moonLight == null && !string.IsNullOrEmpty(moonName))
            {
                GameObject found = GameObject.Find(moonName);
                if (found != null)
                {
                    Light l = found.GetComponent<Light>();
                    if (l != null)
                        moonLight = l;
                }
            }
        }

        // ????????? ?????????? ???????? ????? ? ???????? Light/RenderSettings/??????? ?????
        if (_light != null)
        {
            _light.intensity = Mathf.Lerp(nightIntensity, dayIntensity, Mathf.Clamp01((Vector3.Dot(transform.forward, Vector3.down) + 0.1f) / 1.1f));
            _light.color = Color.Lerp(nightColor, dayColor, Mathf.Clamp01((Vector3.Dot(transform.forward, Vector3.down) + 0.1f) / 1.1f));
            if (_light.shadows != LightShadows.None)
                _light.shadowStrength = Mathf.Lerp(nightShadowStrength, dayShadowStrength, Mathf.Clamp01((Vector3.Dot(transform.forward, Vector3.down) + 0.1f) / 1.1f));
        }

        if (overrideAmbient)
        {
            // ??????? ambient
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            // ???????????? t ?? ??????????? ??????
            float dot = Vector3.Dot(transform.forward, Vector3.down);
            float t = Mathf.Clamp01((dot + 0.1f) / 1.1f);
            Color ambCol = Color.Lerp(nightAmbientColor, dayAmbientColor, t);
            float ambInt = Mathf.Lerp(nightAmbientIntensity, dayAmbientIntensity, t);
            RenderSettings.ambientLight = ambCol * ambInt;
        }

        if (moonLight != null)
        {
            moonLight.color = moonColor;
            // ????????/????????? ? ?????????? ????????????? ?? ????????? ?????? t
            float dot = Vector3.Dot(transform.forward, Vector3.down);
            float t = Mathf.Clamp01((dot + 0.1f) / 1.1f);
            float moonI = Mathf.Lerp(moonIntensityAtNight, moonIntensityAtDay, t);
            moonLight.enabled = moonI > 0f;
            moonLight.intensity = moonI;
        }

#if UNITY_EDITOR
        Debug.Log($"RotatingSun_URP: loaded saved inspector fields from PlayerPrefs with prefix '{p}'.");
#endif
    }

    void OnDisable()
    {
        // ????????? ????????? ??? ?????????? (????????, ??? ????????? Play Mode)
        if (Application.isPlaying) SaveState();
    }

    [ContextMenu("Save RotatingSun state to PlayerPrefs")]
    void ContextSave()
    {
        SaveState();
        Debug.Log("RotatingSun_URP: saved state (context).");
    }

    [ContextMenu("Clear saved RotatingSun prefs")]
    void ClearSavedPrefs()
    {
        string p = prefsKeyPrefix;

        PlayerPrefs.DeleteKey(p + "rotationSpeed");
        PlayerPrefs.DeleteKey(p + "hourOfDay");

        PlayerPrefs.DeleteKey(p + "dayIntensity");
        PlayerPrefs.DeleteKey(p + "nightIntensity");
        PlayerPrefs.DeleteKey(p + "dayColor_r");
        PlayerPrefs.DeleteKey(p + "dayColor_g");
        PlayerPrefs.DeleteKey(p + "dayColor_b");
        PlayerPrefs.DeleteKey(p + "dayColor_a");
        PlayerPrefs.DeleteKey(p + "nightColor_r");
        PlayerPrefs.DeleteKey(p + "nightColor_g");
        PlayerPrefs.DeleteKey(p + "nightColor_b");
        PlayerPrefs.DeleteKey(p + "nightColor_a");

        PlayerPrefs.DeleteKey(p + "autoRotate");

        PlayerPrefs.DeleteKey(p + "overrideAmbient");
        PlayerPrefs.DeleteKey(p + "dayAmbientColor_r");
        PlayerPrefs.DeleteKey(p + "dayAmbientColor_g");
        PlayerPrefs.DeleteKey(p + "dayAmbientColor_b");
        PlayerPrefs.DeleteKey(p + "dayAmbientColor_a");
        PlayerPrefs.DeleteKey(p + "dayAmbientIntensity");
        PlayerPrefs.DeleteKey(p + "nightAmbientColor_r");
        PlayerPrefs.DeleteKey(p + "nightAmbientColor_g");
        PlayerPrefs.DeleteKey(p + "nightAmbientColor_b");
        PlayerPrefs.DeleteKey(p + "nightAmbientColor_a");
        PlayerPrefs.DeleteKey(p + "nightAmbientIntensity");

        PlayerPrefs.DeleteKey(p + "moonLightExists");
        PlayerPrefs.DeleteKey(p + "moonLightName");
        PlayerPrefs.DeleteKey(p + "moonColor_r");
        PlayerPrefs.DeleteKey(p + "moonColor_g");
        PlayerPrefs.DeleteKey(p + "moonColor_b");
        PlayerPrefs.DeleteKey(p + "moonColor_a");
        PlayerPrefs.DeleteKey(p + "moonIntensityAtNight");
        PlayerPrefs.DeleteKey(p + "moonIntensityAtDay");

        PlayerPrefs.DeleteKey(p + "dayShadowStrength");
        PlayerPrefs.DeleteKey(p + "nightShadowStrength");

        PlayerPrefs.DeleteKey(p + "rot_x");
        PlayerPrefs.DeleteKey(p + "rot_y");
        PlayerPrefs.DeleteKey(p + "rot_z");

        PlayerPrefs.DeleteKey(p + "persistWithPlayerPrefs");
        PlayerPrefs.DeleteKey(p + "prefsKeyPrefix");
        PlayerPrefs.DeleteKey(p + "persistAsSingleton");

        PlayerPrefs.Save();
        Debug.Log("RotatingSun_URP: cleared saved prefs");
    }

    [ContextMenu("Print debug info")]
    void PrintDebug()
    {
        if (_light == null) Init();
        string moonName = (moonLight != null) ? moonLight.gameObject.name : "<none>";
        Debug.Log($"hour={hourOfDay} | light.intensity={_light?.intensity} | ambient={RenderSettings.ambientLight} | rot={transform.localEulerAngles} | moonLight={moonName}");
    }
}