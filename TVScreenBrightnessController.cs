using UnityEngine;
using UnityEngine.Video;

public class TVScreenBrightnessController : MonoBehaviour
{
    public Light sunLight;
    public VideoPlayer videoPlayer;
    public Renderer screenRenderer;

    [Header("Материал для видео (активный ТВ)")]
    public Material tvScreenMaterial;
    [Header("Материал для выключенного ТВ")]
    public Material tvOffMaterial;

    public float dayBrightness = 1.0f;
    public float nightBrightness = 2.3f;

    void Update()
    {
        bool tvIsPlaying = (videoPlayer != null) && videoPlayer.isPlaying;
        if (screenRenderer == null) return;

        // Меняем материал на лету в зависимости от состояния ТВ
        Material targetMat = tvIsPlaying ? tvScreenMaterial : tvOffMaterial;

        // Меняем только если реально нужен другой (оптимизация)
        if (screenRenderer.sharedMaterial != targetMat)
            screenRenderer.material = targetMat;

        // Если ТВ выключен, дальше ничего делать не надо — выходим
        if (!tvIsPlaying) return;

        // Остальное — как в основном скрипте ???
        float t = 1;
        if (sunLight != null)
        {
            float dot = Vector3.Dot(sunLight.transform.forward, Vector3.down);
            t = Mathf.Clamp01((dot + 0.1f) / 1.1f);
        }

        float brightness = Mathf.Lerp(nightBrightness, dayBrightness, t);
        screenRenderer.material.SetFloat("_ScreenBrightness", brightness);
    }
}