using UnityEngine;
using UnityEngine.Video;

public class TVController : MonoBehaviour
{
    [Header("Экран телевизора (объект с VideoPlayer)")]
    public VideoPlayer screenPlayer;
    public Renderer screenRenderer; // MeshRenderer или SkinnedMeshRenderer от экрана
    public string textureProperty = "_MainTex"; // Обычно "_MainTex"

    private bool isPlaying = false;
    private Texture initialTexture;

    void Start()
    {
        if (screenRenderer != null)
        {
            initialTexture = screenRenderer.material.GetTexture(textureProperty);
        }
        if (screenPlayer != null)
        {
            screenPlayer.loopPointReached += OnVideoEnd;
        }
    }

    // Вызывается из onPressed события кнопки
    public void ToggleScreen()
    {
        if (screenPlayer == null) return;

        if (!isPlaying)
        {
            screenPlayer.Play();
            isPlaying = true;
        }
        else
        {
            StopScreen();
        }
    }

    public void StopScreen()
    {
        if (screenPlayer == null) return;
        screenPlayer.Stop();
        isPlaying = false;
        RestoreInitialTexture();
    }

    private void OnVideoEnd(VideoPlayer vp)
    {
        StopScreen();
    }

    private void RestoreInitialTexture()
    {
        if (screenRenderer != null && initialTexture != null)
        {
            screenRenderer.material.SetTexture(textureProperty, initialTexture);
        }
    }
}