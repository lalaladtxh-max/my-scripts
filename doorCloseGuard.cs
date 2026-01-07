using UnityEngine;

/// <summary>
/// ????????? ????????? InteractableButton ?? mainDoor, ???? innerDoor ??????.
/// ??? ???????? innerDoor ??????????????? ?????????? (????????) enabled-????????? ??????????.
/// ?? ??????? ????????? DoorController/InteractableButton.
///
/// Добавлена блокировка открытия innerDoor (морозилка) на время, пока mainDoor не открыт.
/// То есть если mainDoor не открыт (закрывается/закрыта) и innerDoor в данный момент закрыта,
/// то InteractableButton у innerDoor будет отключён до тех пор, пока mainDoor не станет открытой.
/// Это предотвращает ситуацию, когда во время закрытия основной двери можно нажать открыть морозильную дверцу
/// и она "торчит" сквозь текстуру закрытой основной двери.
/// </summary>
public class DoorCloseGuard : MonoBehaviour
{
    [Tooltip("Main (???????) ????? ? ??, ?? ??????? ????? ????????? InteractableButton")]
    public DoorController mainDoor;

    [Tooltip("Inner (???????????) ????? ? ???? ??? ???????, mainDoor.InteractableButton ????? ????????")]
    public DoorController innerDoor;

    [Tooltip("???? ????? ? ????? ?????????????? ?????? ???? ????????? InteractableButton. " +
             "???? ?????, ?????? ?????????? ????? ??? ?? mainDoor (??? ? ?? ?????).")]
    public InteractableButton mainButton;

    [Tooltip("???????? ??????????? ?????????? ? ???????")]
    public bool logBlocked = false;

    bool prevInnerOpen = false;
    bool initialButtonEnabled = true;
    bool haveButton = false;

    // --- additions for inner button blocking ---
    InteractableButton innerButton = null;
    bool haveInnerButton = false;
    bool initialInnerButtonEnabled = true;
    bool prevMainOpen = false;
    // ------------------------------------------------

    void Start()
    {
        // ??????? ????????????? ????? ??????, ???? ?? ??????
        if (mainDoor == null)
        {
            mainDoor = GetComponent<DoorController>();
            if (mainDoor == null && logBlocked)
                Debug.LogWarning("[DoorCloseGuard] mainDoor ?? ????? ? ?? ?????? ?? ??? ?? ???????.");
        }

        if (mainButton == null && mainDoor != null)
        {
            mainButton = mainDoor.GetComponent<InteractableButton>();
            if (mainButton == null)
                mainButton = mainDoor.GetComponentInChildren<InteractableButton>();
        }

        if (mainButton != null)
        {
            haveButton = true;
            initialButtonEnabled = mainButton.enabled;
        }
        else
        {
            haveButton = false;
            if (logBlocked)
                Debug.LogWarning("[DoorCloseGuard] InteractableButton ??? mainDoor ?? ??????. ?????? ?????? ?? ????? ?????????.");
        }

        // Find inner button (if any) and remember its initial state
        if (innerDoor != null)
        {
            innerButton = innerDoor.GetComponent<InteractableButton>();
            if (innerButton == null)
                innerButton = innerDoor.GetComponentInChildren<InteractableButton>();

            if (innerButton != null)
            {
                haveInnerButton = true;
                initialInnerButtonEnabled = innerButton.enabled;
            }
            else
            {
                haveInnerButton = false;
                if (logBlocked)
                    Debug.LogWarning("[DoorCloseGuard] InteractableButton ??? innerDoor ?? ??????. ?????? ?????? ?? ????? ?????????.");
            }
        }

        prevInnerOpen = innerDoor != null && innerDoor.IsOpen();
        prevMainOpen = mainDoor != null && mainDoor.IsOpen();

        // ????????? ????????? ????????? (???? ??????????? ?????????? ??????)
        if (haveButton && prevInnerOpen)
        {
            mainButton.enabled = false;
            if (logBlocked)
                Debug.Log($"[DoorCloseGuard] ?????: ???????? InteractableButton ?? '{mainDoor.gameObject.name}', ?.?. '{innerDoor.gameObject.name}' ??????.");
        }

        // Initial block for innerButton if main is not open and inner is closed
        if (haveInnerButton)
        {
            bool mainOpen = mainDoor != null && mainDoor.IsOpen();
            bool innerOpen = innerDoor != null && innerDoor.IsOpen();

            if (!mainOpen && !innerOpen)
            {
                innerButton.enabled = false;
                if (logBlocked)
                    Debug.Log($"[DoorCloseGuard] ?????: ???????? InteractableButton ?? '{innerDoor.gameObject.name}' ?-?? mainDoor ?? ??????.");
            }
        }
    }

    void Update()
    {
        if (innerDoor == null && mainDoor == null) return;
        if (!haveButton && !haveInnerButton) return;

        bool curInnerOpen = innerDoor != null && innerDoor.IsOpen();
        bool curMainOpen = mainDoor != null && mainDoor.IsOpen();

        // ???????: ????????? ????? ????????? -> ????????? ?????? (main button)
        if (!prevInnerOpen && curInnerOpen)
        {
            if (haveButton)
            {
                mainButton.enabled = false;
                if (logBlocked)
                    Debug.Log($"[DoorCloseGuard] ???????? InteractableButton ?? '{mainDoor.gameObject.name}' (?????????? ????? ???????).");
            }
        }

        // ???????: ?????????? ????? ????????? -> ??????????????? ???????? ????????? (main button)
        if (prevInnerOpen && !curInnerOpen)
        {
            if (haveButton)
            {
                mainButton.enabled = initialButtonEnabled;
                if (logBlocked)
                    Debug.Log($"[DoorCloseGuard] ??????????? InteractableButton ?? '{mainDoor.gameObject.name}' -> enabled = {initialButtonEnabled} (?????????? ????? ???????).");
            }
        }

        // --- new logic: блокировка открытия innerDoor, пока mainDoor не открыт ---
        if (haveInnerButton && innerDoor != null && innerButton != null && mainDoor != null)
        {
            // Мы хотим запретить ОТКРЫТИЕ innerDoor, когда mainDoor не открыт.
            // Поскольку InteractableButton переключает состояние (open/close), мы не можем различить "открыть" и "закрыть"
            // до клика. Поэтому применяем простое правило:
            // - если mainDoor не открыт AND innerDoor сейчас ЗАКРЫТ -> блокируем innerButton полностью (нельзя открыть)
            // - если innerDoor открыт -> оставляем innerButton включённым, чтобы можно было его закрыть
            // - как только mainDoor снова открыт, возвращаем innerButton в первоначальное состояние
            if (!curMainOpen && !curInnerOpen)
            {
                if (innerButton.enabled)
                {
                    innerButton.enabled = false;
                    if (logBlocked)
                        Debug.Log($"[DoorCloseGuard] Блокировка: InteractableButton у '{innerDoor.gameObject.name}' отключён пока '{mainDoor.gameObject.name}' не открыт.");
                }
            }
            else
            {
                // main открыт, или inner уже открыт -> вернуть прежнее состояние (если необходимо)
                if (innerButton.enabled != initialInnerButtonEnabled)
                {
                    innerButton.enabled = initialInnerButtonEnabled;
                    if (logBlocked)
                        Debug.Log($"[DoorCloseGuard] Снятие блокировки: InteractableButton у '{innerDoor.gameObject.name}' -> enabled = {initialInnerButtonEnabled}.");
                }
            }
        }
        // ---------------------------------------------------------------

        prevInnerOpen = curInnerOpen;
        prevMainOpen = curMainOpen;
    }
}