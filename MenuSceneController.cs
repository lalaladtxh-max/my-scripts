using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// ?????????? ???? ???????? ??:
/// - ??????/????????? ??????
/// - ????????/???????? ????????
/// - ????????? ?????? ?? ???????? ????
/// </summary>
public class MenuSceneController : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public TextMeshProUGUI tooltipText;
    public GameObject settingsPanel;

    [Header("Game")]
    [Tooltip("??? ????? ? ?????")]
    public string gameSceneName = "GameScene";

    [Header("Music")]
    [Tooltip("Tag ???????? ??????? ?????? (???? ????????????) ")]
    public string musicTargetTag = "MusicTarget";
    const string persistentMusicName = "PersistentMusic_Player";

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        HideTooltip();
        if (settingsPanel != null) settingsPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    #region Tooltip
    public void ShowTooltip(string text)
    {
        if (tooltipText == null) return;
        tooltipText.text = text;
        tooltipText.gameObject.SetActive(true);
    }

    public void HideTooltip()
    {
        if (tooltipText == null) return;
        tooltipText.gameObject.SetActive(false);
    }
    #endregion

    #region Settings
    public void OpenSettings()
    {
        if (settingsPanel == null) return;
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel == null) return;
        settingsPanel.SetActive(false);
    }
    #endregion

    #region Music
    /// <summary>
    /// Toggle music by trying several strategies:
    /// - Persistent music GameObject created by LoadingScreenController (name PersistentMusic_Player)
    /// - Object with given musicTargetTag that has AudioSource
    /// </summary>
    public void ToggleMusic()
    {
        // Try persistent music object
        var persistent = GameObject.Find(persistentMusicName);
        if (persistent != null)
        {
            var src = persistent.GetComponent<AudioSource>();
            if (src != null)
            {
                if (src.isPlaying) src.Pause();
                else src.UnPause(); // resume if paused
                return;
            }
        }

        // Try tagged music target
        if (!string.IsNullOrEmpty(musicTargetTag))
        {
            var objs = GameObject.FindGameObjectsWithTag(musicTargetTag);
            if (objs != null && objs.Length > 0)
            {
                var src = objs[0].GetComponent<AudioSource>();
                if (src != null)
                {
                    if (src.isPlaying) src.Pause();
                    else src.UnPause();
                    return;
                }
            }
        }

        // Fallback: find any AudioSource in scene that looks like music (first looping audio source)
        var all = FindObjectsOfType<AudioSource>();
        foreach (var a in all)
        {
            if (a.loop)
            {
                if (a.isPlaying) a.Pause(); else a.UnPause();
                return;
            }
        }
    }
    #endregion

    #region Object click handling
    /// <summary>
    /// ?????????? ????? ?? MenuPointer. ???????? ????? MenuButtonAction ? ????????? ????????.
    /// </summary>
    public void OnObjectClicked(GameObject go)
    {
        if (go == null) return;
        var action = go.GetComponent<MenuButtonAction>();
        if (action != null)
        {
            PerformAction(action);
            return;
        }

        // ???? ???? InteractableButton ? onPressed listeners ? ???????????? ??? ??? fallback
        var ib = go.GetComponent<InteractableButton>();
        if (ib != null)
        {
            ib.onPressed?.Invoke();
        }
    }

    void PerformAction(MenuButtonAction action)
    {
        switch (action.buttonType)
        {
            case MenuButtonAction.ButtonType.StartGame:
                StartGameSequence();
                break;
            case MenuButtonAction.ButtonType.ToggleMusic:
                ToggleMusic();
                action.InvokeOnPressed(); // optional feedback
                break;
            case MenuButtonAction.ButtonType.OpenSettings:
                OpenSettings();
                action.InvokeOnPressed();
                break;
            case MenuButtonAction.ButtonType.ToggleLight:
                // Try to find LampSwapController in scene and toggle the lamp
                var lamp = FindObjectOfType<LampSwapController>();
                if (lamp != null)
                {
                    lamp.ToggleLamp();
                    action.InvokeOnPressed();
                    return;
                }
                // Fallback: try LightController
                var lightCtrl = FindObjectOfType<LightController>();
                if (lightCtrl != null)
                {
                    lightCtrl.Toggle();
                    action.InvokeOnPressed();
                    return;
                }
                break;
            case MenuButtonAction.ButtonType.QuitGame:
                // Try to find QuitButton in scene and call Quit(), otherwise fallback to Application.Quit
                var qb = FindObjectOfType<QuitButton>();
                if (qb != null)
                {
                    qb.Quit();
                }
                else
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }
                action.InvokeOnPressed();
                break;
        }
    }

    void StartGameSequence()
    {
        // ??????? ????? CameraSeatAnimator ?? ???????? ?????? ??? ? ?????
        CameraSeatAnimator animator = null;
        if (mainCamera != null)
            animator = mainCamera.GetComponent<CameraSeatAnimator>();
        if (animator == null)
            animator = FindObjectOfType<CameraSeatAnimator>();

        if (animator != null)
        {
            animator.PlayStandUpAndLoadScene(gameSceneName);
        }
        else
        {
            // ?????? ??????????? ?? SceneCameraBinder ? ?????? ????????? ?????
            SceneManager.LoadScene(gameSceneName);
        }
    }
    #endregion
}