using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Глобальный raycast наведения по центру экрана.
/// </summary>
public class GazeInteractor : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public Text infoText;
    [Header("Raycast")]
    public float maxDistance = 3f;
    public LayerMask interactableMask = ~0;

    ObjectHighlighter currentHighlighted;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
        if (infoText != null)
            infoText.enabled = false;
    }

    void Update()
    {
        if (playerCamera == null) return;

        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, maxDistance, interactableMask, QueryTriggerInteraction.Ignore))
        {
            var high = hit.collider.GetComponent<ObjectHighlighter>();
            if (high != null)
            {
                if (currentHighlighted != high)
                {
                    ClearCurrent();
                    currentHighlighted = high;
                    currentHighlighted.SetHighlighted(true);

                    if (infoText != null)
                    {
                        infoText.text = currentHighlighted.objectName;
                        infoText.enabled = true;
                    }
                }
                return;
            }
        }
        ClearCurrent();
    }

    void ClearCurrent()
    {
        if (currentHighlighted != null)
        {
            currentHighlighted.SetHighlighted(false);
            currentHighlighted = null;
        }
        if (infoText != null)
            infoText.enabled = false;
    }
}