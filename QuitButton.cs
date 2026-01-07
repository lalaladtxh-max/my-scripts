using UnityEngine;

/// <summary>
/// Небольшой скрипт для UI-кнопки выхода из игры.
/// </summary>
public class QuitButton : MonoBehaviour
{
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}