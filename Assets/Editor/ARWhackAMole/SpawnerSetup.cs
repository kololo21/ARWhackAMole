using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// メニュー: Tools → ARWhackAMole → Create DwellerSpawner
// 空のオブジェクト DwellerSpawner を作って DwellerSpawner を付ける(付けた瞬間に Reset が欄を埋める)
public static class SpawnerSetup
{
    [MenuItem("Tools/ARWhackAMole/Create DwellerSpawner")]
    private static void Create()
    {
        if (Object.FindAnyObjectByType<DwellerSpawner>() != null)
        {
            Debug.LogWarning("SpawnerSetup: DwellerSpawner はすでにシーンにあります");
            return;
        }
        GameObject go = new GameObject("DwellerSpawner");
        Undo.RegisterCreatedObjectUndo(go, "Create DwellerSpawner");
        Undo.AddComponent<DwellerSpawner>(go);
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("SpawnerSetup: DwellerSpawner を作りました");
    }
}
