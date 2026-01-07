using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ������ �������� �������.
/// ��������� UI � ��������� 5 �������� �������� (����� ������, ������, �������, �������, ����� �������),
/// ������������ �������������� ������ � ����������.
/// </summary>
public class GraphicsSettings : MonoBehaviour
{
    [Header("UI")]
    public TMP_Dropdown qualityDropdown;
    public Toggle fullscreenToggle;
    public TMP_Dropdown resolutionDropdown;
    public Button backButton;

    const string KEY_QUALITY = "gfx_quality";
    const string KEY_FULLSCREEN = "gfx_fullscreen";
    const string KEY_RES_INDEX = "gfx_resolutionIndex";

    Resolution[] resolutions;

    // ���� ���������� ������ ������� ��������
    class QualityPreset
    {
        public string name;
        public int antiAliasing; // 0,2,4,8
        public int masterTextureLimit; // 0 - full, 1 - half, ...
        public ShadowQuality shadows;
        public float shadowDistance;
        public int vSyncCount; // 0,1,2
        public int pixelLightCount;
        public AnisotropicFiltering anisotropicFiltering;
        public float lodBias;
    }

    // ���������� 5 �������� (�������� ����� ���������� ��� ������)
    QualityPreset[] presets = new QualityPreset[]
    {
        new QualityPreset {
            name = "Очень низкое",
            antiAliasing = 0,
            masterTextureLimit = 2,
            shadows = ShadowQuality.Disable,
            shadowDistance = 0f,
            vSyncCount = 0,
            pixelLightCount = 0,
            anisotropicFiltering = AnisotropicFiltering.Disable,
            lodBias = 0.5f
        },
        new QualityPreset {
            name = "Низкое",
            antiAliasing = 0,
            masterTextureLimit = 1,
            shadows = ShadowQuality.HardOnly,
            shadowDistance = 15f,
            vSyncCount = 0,
            pixelLightCount = 1,
            anisotropicFiltering = AnisotropicFiltering.Disable,
            lodBias = 0.75f
        },
        new QualityPreset {
            name = "Среднее",
            antiAliasing = 2,
            masterTextureLimit = 0,
            shadows = ShadowQuality.All,
            shadowDistance = 30f,
            vSyncCount = 0,
            pixelLightCount = 2,
            anisotropicFiltering = AnisotropicFiltering.Enable,
            lodBias = 1.0f
        },
        new QualityPreset {
            name = "Высокое",
            antiAliasing = 4,
            masterTextureLimit = 0,
            shadows = ShadowQuality.All,
            shadowDistance = 60f,
            vSyncCount = 1,
            pixelLightCount = 4,
            anisotropicFiltering = AnisotropicFiltering.ForceEnable,
            lodBias = 1.5f
        },
        new QualityPreset {
            name = "Очень высокое",
            antiAliasing = 8,
            masterTextureLimit = 0,
            shadows = ShadowQuality.All,
            shadowDistance = 100f,
            vSyncCount = 1,
            pixelLightCount = 8,
            anisotropicFiltering = AnisotropicFiltering.ForceEnable,
            lodBias = 2.0f
        }
    };

    void Awake()
    {
        // Fill quality options with ���� �������
        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            var names = presets.Select(p => p.name).ToList();
            qualityDropdown.AddOptions(names);
            qualityDropdown.onValueChanged.AddListener(SetQuality);
        }

        // Fill resolutions (���������� ��������� �������� ����)
        resolutions = Screen.resolutions
            .Select(r => new Resolution { width = r.width, height = r.height, refreshRate = r.refreshRate })
            .Distinct(new ResolutionComparer())
            .ToArray();

        if (resolutionDropdown != null)
        {
            resolutionDropdown.ClearOptions();
            var opts = resolutions.Select(r => $"{r.width} x {r.height} @ {r.refreshRate}Hz").ToList();
            resolutionDropdown.AddOptions(opts);
            resolutionDropdown.onValueChanged.AddListener(SetResolution);
        }

        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        if (backButton != null) backButton.onClick.AddListener(OnBack);
    }

    void OnEnable()
    {
        LoadToUI();
    }

    void LoadToUI()
    {
        // ��-���������: ������� (2)
        int q = PlayerPrefs.GetInt(KEY_QUALITY, 2);
        bool fs = PlayerPrefs.GetInt(KEY_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
        int ri = PlayerPrefs.GetInt(KEY_RES_INDEX, GetClosestResolutionIndex());

        if (qualityDropdown != null) qualityDropdown.value = Mathf.Clamp(q, 0, presets.Length - 1);
        if (fullscreenToggle != null) fullscreenToggle.isOn = fs;
        if (resolutionDropdown != null && ri >= 0 && ri < resolutionDropdown.options.Count) resolutionDropdown.value = ri;

        ApplyImmediate();
    }

    void ApplyImmediate()
    {
        // apply quality (������� ���� �� UI, ���� UI ����������� � �� PlayerPrefs)
        int q = qualityDropdown != null ? qualityDropdown.value : PlayerPrefs.GetInt(KEY_QUALITY, 2);
        ApplyPreset(q, save: false);

        // fullscreen
        bool fs = fullscreenToggle != null ? fullscreenToggle.isOn : PlayerPrefs.GetInt(KEY_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
        Screen.fullScreen = fs;

        // resolution
        if (resolutionDropdown != null && resolutionDropdown.value >= 0 && resolutionDropdown.value < resolutions.Length)
        {
            var r = resolutions[resolutionDropdown.value];
            Screen.SetResolution(r.width, r.height, Screen.fullScreen, r.refreshRate);
        }
    }

    // ��������� ������ � ��������� ����� (���� save == true)
    void ApplyPreset(int index, bool save = true)
    {
        if (index < 0 || index >= presets.Length) return;
        var p = presets[index];

        QualitySettings.antiAliasing = p.antiAliasing;
        QualitySettings.globalTextureMipmapLimit = p.masterTextureLimit;
        QualitySettings.shadows = p.shadows;
        QualitySettings.shadowDistance = p.shadowDistance;
        QualitySettings.vSyncCount = p.vSyncCount;
        QualitySettings.pixelLightCount = p.pixelLightCount;
        QualitySettings.anisotropicFiltering = p.anisotropicFiltering;
        QualitySettings.lodBias = p.lodBias;

        // ���� � ������� ���� ������ �������� � ������ � �������� � ���� ���������� ���������� �������
        if (index >= 0 && index < QualitySettings.names.Length)
            QualitySettings.SetQualityLevel(index, false);

        if (save)
        {
            PlayerPrefs.SetInt(KEY_QUALITY, index);
            PlayerPrefs.Save();
        }
    }

    public void SetQuality(int index)
    {
        ApplyPreset(index, save: true);
    }

    public void SetFullscreen(bool on)
    {
        Screen.fullScreen = on;
        PlayerPrefs.SetInt(KEY_FULLSCREEN, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetResolution(int index)
    {
        if (index < 0 || index >= resolutions.Length) return;
        var r = resolutions[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreen, r.refreshRate);
        PlayerPrefs.SetInt(KEY_RES_INDEX, index);
        PlayerPrefs.Save();
    }

    public void ResetDefaults()
    {
        int defaultQuality = 2; // �������
        bool defaultFs = Screen.fullScreen;
        int defaultRes = GetClosestResolutionIndex();

        if (qualityDropdown != null) qualityDropdown.value = defaultQuality;
        if (fullscreenToggle != null) fullscreenToggle.isOn = defaultFs;
        if (resolutionDropdown != null) resolutionDropdown.value = defaultRes;

        // apply and save
        PlayerPrefs.SetInt(KEY_QUALITY, defaultQuality);
        PlayerPrefs.SetInt(KEY_FULLSCREEN, defaultFs ? 1 : 0);
        PlayerPrefs.SetInt(KEY_RES_INDEX, defaultRes);
        PlayerPrefs.Save();
        ApplyPreset(defaultQuality, save: false);
        // �������� ���������� � fullscreen
        if (resolutionDropdown != null && resolutionDropdown.value >= 0 && resolutionDropdown.value < resolutions.Length)
        {
            var r = resolutions[resolutionDropdown.value];
            Screen.SetResolution(r.width, r.height, Screen.fullScreen, r.refreshRate);
        }
    }

    void OnBack()
    {
        // ���� ���������� �������� � ������������
        var parentCtrl = FindObjectOfType<SettingsMenuController>();
        parentCtrl?.BackToMain();
    }

    int GetClosestResolutionIndex()
    {
        var current = Screen.currentResolution;
        for (int i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i].width == current.width && resolutions[i].height == current.height)
                return i;
        }
        return Mathf.Clamp(resolutions.Length - 1, 0, resolutions.Length - 1);
    }

    class ResolutionComparer : System.Collections.Generic.IEqualityComparer<Resolution>
    {
        public bool Equals(Resolution x, Resolution y) => x.width == y.width && x.height == y.height && x.refreshRate == y.refreshRate;
        public int GetHashCode(Resolution obj) { unchecked { return obj.width * 397 ^ obj.height * 31 ^ obj.refreshRate; } }
    }
}