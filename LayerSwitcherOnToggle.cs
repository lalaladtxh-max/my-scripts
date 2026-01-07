using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LayerSwitcherOnToggle (переработанный).
/// Этот компонент применяет "блокирующий" слой к указанным целевым объектам, когда сам компонент/его GameObject
/// становится активным (галочка в инспекторе включена), и восстанавливает исходные слои при отключении.
/// 
/// Как использовать:
/// - Повесьте этот скрипт на тот GameObject, у которого вы будете переключать галочку в инспекторе
///   (та самая checkbox рядом с именем объекта, Active/Inactive).
/// - В поле Targets укажите корни объектов, слои у которых вы хотите временно менять.
///   Если applyRecursively = true — слой будет применён ко всем детям указанных корней.
/// - В blockedLayerName укажите имя слоя, в который переводятся объекты при активации (по умолчанию "Ignore Raycast").
///   Если имя не найдено, будет использован blockedLayerIndex.
/// 
/// Поведение:
/// - При включении компонента / активации GameObject (OnEnable) — ApplyBlock() вызывается и целевые объекты получают blockedLayer.
/// - При отключении компонента / деактивации GameObject (OnDisable) — RemoveBlock() возвращает прежние слои.
/// - При уничтожении объекта (OnDestroy) — также снимает блокировку, если она была установлена.
/// - Поддерживается несколько независимых блокировщиков: менеджер хранит счётчик блокировок и восстанавливает
///   исходный слой только когда все блокировки для данного объекта сняты.
/// </summary>
public class LayerSwitcherOnToggle : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("Корни объектов, для которых нужно временно поменять слой.")]
    public GameObject[] targets;

    [Header("Layer settings")]
    [Tooltip("Имя слоя, в который переводим объекты при активации. Если пусто или не найдено, используется blockedLayerIndex.")]
    public string blockedLayerName = "Ignore Raycast";
    [Tooltip("Индекс слоя, используемый если имя слоя не найдено.")]
    public int blockedLayerIndex = 2;

    [Header("Options")]
    [Tooltip("Если true — применяет изменение слоя ко всем детям targets (GetComponentsInChildren).")]
    public bool applyRecursively = true;

    // Решённый индекс слоя (вычисляется в Awake/OnValidate)
    int resolvedBlockedLayer;

    void Awake()
    {
        ResolveBlockedLayer();
    }

    void OnValidate()
    {
        // Обновляем resolvedBlockedLayer в редакторе при изменении полей
        ResolveBlockedLayer();
    }

    void OnEnable()
    {
        // Когда компонент/объект активируется — применяем блокировку
        ApplyBlock();
    }

    void OnDisable()
    {
        // Когда компонент/объект деактивируется — снимаем блокировку
        RemoveBlock();
    }

    void OnDestroy()
    {
        // На случай уничтожения (чтобы не оставить объекты заблокированными)
        RemoveBlock();
    }

    void ResolveBlockedLayer()
    {
        if (!string.IsNullOrEmpty(blockedLayerName))
        {
            int l = LayerMask.NameToLayer(blockedLayerName);
            if (l >= 0) resolvedBlockedLayer = l;
            else resolvedBlockedLayer = blockedLayerIndex;
        }
        else
        {
            resolvedBlockedLayer = blockedLayerIndex;
        }
    }

    void ApplyBlock()
    {
        if (targets == null || targets.Length == 0) return;

        ResolveBlockedLayer();

        foreach (var t in targets)
            LayerBlockerManager.Block(t, resolvedBlockedLayer, applyRecursively);
    }

    void RemoveBlock()
    {
        if (targets == null || targets.Length == 0) return;

        foreach (var t in targets)
            LayerBlockerManager.Unblock(t, applyRecursively);
    }
}

/// <summary>
/// Внутренний менеджер, который хранит исходные слои и счётчик блокировок для каждого GameObject.
/// Восстанавливает исходный слой только когда все блокировки сняты.
/// </summary>
static class LayerBlockerManager
{
    class Info
    {
        public int originalLayer;
        public int count;
    }

    // Храним слабые ссылки на объекты не используем — используем прямые ссылки на GameObject.
    // Внимание: удержание ссылки предотвращает сборку объекта до очистки записи, однако мы очищаем запись
    // в Unblock при восстановлении. Если объект уничтожается, Unblock попытается восстановить и удалить запись.
    static readonly Dictionary<GameObject, Info> states = new Dictionary<GameObject, Info>();

    public static void Block(GameObject root, int blockedLayer, bool recursive)
    {
        if (root == null) return;

        Transform[] transforms;
        if (recursive)
            transforms = root.GetComponentsInChildren<Transform>(true);
        else
            transforms = new Transform[] { root.transform };

        foreach (var tr in transforms)
        {
            var go = tr.gameObject;
            if (go == null) continue;

            if (states.TryGetValue(go, out var info))
            {
                // Уже заблокирован — увеличиваем счётчик
                info.count++;
            }
            else
            {
                // Сохраняем исходный слой и применяем блокирующий слой
                info = new Info { originalLayer = go.layer, count = 1 };
                states.Add(go, info);
                try
                {
                    go.layer = blockedLayer;
                }
                catch
                {
                    // если объект уничтожен в процессе — удаляем запись и продолжаем
                    states.Remove(go);
                }
            }
        }
    }

    public static void Unblock(GameObject root, bool recursive)
    {
        if (root == null) return;

        Transform[] transforms;
        if (recursive)
            transforms = root.GetComponentsInChildren<Transform>(true);
        else
            transforms = new Transform[] { root.transform };

        foreach (var tr in transforms)
        {
            var go = tr.gameObject;
            if (go == null) continue;

            if (states.TryGetValue(go, out var info))
            {
                info.count--;
                if (info.count <= 0)
                {
                    // Восстанавливаем исходный слой и удаляем запись
                    try
                    {
                        go.layer = info.originalLayer;
                    }
                    catch
                    {
                        // если объект уничтожен — ничего не делаем
                    }
                    states.Remove(go);
                }
            }
            // если записи не было — значит никто не блокировал этот объект (ничего делать не нужно)
        }
    }
}