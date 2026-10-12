using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vuforia;

// シーンの組み替えを一度に行うエディタ用メニュー(ゲームには含まれない)。
// メニュー: Tools → ARWhackAMole → Convert ImageTargets to HoleSlot
public static class HoleSlotSetup
{
    [MenuItem("Tools/ARWhackAMole/Convert ImageTargets to HoleSlot")]
    private static void Convert()
    {
        string[] targetNames = { "ImageTarget1", "ImageTarget2", "ImageTarget3" };
        foreach (string targetName in targetNames)
        {
            GameObject go = GameObject.Find(targetName);
            if (go == null)
            {
                Debug.LogWarning($"HoleSlotSetup: {targetName} が見つかりません");
                continue;
            }

            // 古いハンドラ(Default / Custom)を外す。HoleSlot 自身は残す
            foreach (DefaultObserverEventHandler h in go.GetComponents<DefaultObserverEventHandler>())
            {
                if (!(h is HoleSlot)) Undo.DestroyObjectImmediate(h);
            }

            HoleSlot slot = go.GetComponent<HoleSlot>();
            if (slot == null) slot = Undo.AddComponent<HoleSlot>(go);
            Undo.RecordObject(slot, "Set StatusFilter");
            slot.StatusFilter = DefaultObserverEventHandler.TrackingStatusFilter.Tracked;

            // 最初から置いていたキャラ(名前が Dweller で始まる子)を外す。Hole は残す
            for (int i = go.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = go.transform.GetChild(i);
                if (child.name.StartsWith("Dweller")) Undo.DestroyObjectImmediate(child.gameObject);
            }
            Debug.Log($"HoleSlotSetup: {targetName} を HoleSlot に変更しました");
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
