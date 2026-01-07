using UnityEngine;

/// <summary>
/// Pointer for the menu scene: raycasts from mainCamera using the mouse position,
/// toggles ObjectHighlighter, shows tooltip via MenuSceneController and handles clicks.
/// Attach to an always-enabled object in the menu scene (e.g. the same GameObject as MenuSceneController).
/// </summary>
[RequireComponent(typeof(Camera))]
public class MenuPointer : MonoBehaviour
{
    public Camera rayCamera;
    public float maxDistance = 10f;
    public LayerMask interactableMask = ~0;

    MenuSceneController sceneController;
    ObjectHighlighter currentHigh;
    void Start()
    {
        if (rayCamera == null) rayCamera = GetComponent<Camera>() ?? Camera.main;
        sceneController = FindObjectOfType<MenuSceneController>();
        if (sceneController == null)
            Debug.LogWarning("MenuPointer: MenuSceneController not found in scene.");
    }

    void Update()
    {
        if (rayCamera == null) return;

        Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactableMask, QueryTriggerInteraction.Collide))
        {
            var high = hit.collider.GetComponent<ObjectHighlighter>();
            if (high == null) high = hit.collider.GetComponentInParent<ObjectHighlighter>();
            if (currentHigh != high)
            {
                ClearCurrent();
                currentHigh = high;
                if (currentHigh != null)
                {
                    currentHigh.SetHighlighted(true);
                    if (sceneController != null)
                        sceneController.ShowTooltip(currentHigh.objectName);
                    // optional: start hover animation via MenuButtonAction
                    var mba = currentHigh.GetComponent<MenuButtonAction>() ?? currentHigh.GetComponentInParent<MenuButtonAction>();
                    if (mba != null) mba.OnHoverEnter();
                }
            }

            // click handling (left button)
            if (Input.GetMouseButtonDown(0))
            {
                if (currentHigh != null)
                {
                    var mba = currentHigh.GetComponent<MenuButtonAction>() ?? currentHigh.GetComponentInParent<MenuButtonAction>();
                    if (mba != null)
                    {
                        mba.OnClick();
                        return;
                    }

                    // fallback: notify controller
                    sceneController?.OnObjectClicked(currentHigh.gameObject);
                }
            }

            return;
        }

        ClearCurrent();
    }

    void ClearCurrent()
    {
        if (currentHigh != null)
        {
            // stop hover animation
            var mba = currentHigh.GetComponent<MenuButtonAction>() ?? currentHigh.GetComponentInParent<MenuButtonAction>();
            mba?.OnHoverExit();

            currentHigh.SetHighlighted(false);
            currentHigh = null;
        }
        sceneController?.HideTooltip();
    }
}