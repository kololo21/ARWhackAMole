using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhackAMole.Sensors;

[InitializeOnLoad]
public static class WhackSensorSetup
{
    static WhackSensorSetup(){Debug.Log("WHACK_SENSOR_UPSTREAM_REVISION_3");EditorApplication.delayCall+=EnsureCatalog;}
    [MenuItem("Sensor Game/Prepare Mole Catalog")]
    public static void EnsureCatalog()
    {
        const string path="Assets/Resources/WhackMoleCatalog.asset";
        if(AssetDatabase.LoadAssetAtPath<WhackMoleCatalog>(path)!=null)return;
        var catalog=ScriptableObject.CreateInstance<WhackMoleCatalog>();
        catalog.normalRed=Load("Normal_Red");catalog.normalBlue=Load("Normal_Blue");
        catalog.chargeRed=Load("Windup_Red");catalog.chargeBlue=Load("Windup_Blue");
        catalog.flashRed=Load("Speedy_Red");catalog.flashBlue=Load("Speedy_Blue");
        Directory.CreateDirectory("Assets/Resources");AssetDatabase.CreateAsset(catalog,path);AssetDatabase.SaveAssets();
        Debug.Log("WHACK_SENSOR_CATALOG_READY");
    }
    private static GameObject Load(string kind)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Prefabs/Dweller_"+kind+".prefab");
        if(prefab==null)throw new System.InvalidOperationException("Missing mole prefab: "+kind);return prefab;
    }
    [MenuItem("Sensor Game/Run Integration Verification")]
    public static void Verify()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play before starting the isolated verification run");
        if(EditorSceneManager.GetActiveScene().isDirty)throw new System.InvalidOperationException("Save your scene changes before the verification run");
        WhackSensorRulesChecks.RunChecks();EnsureCatalog();
        var path=EditorSceneManager.GetActiveScene().path;
        if(string.IsNullOrEmpty(path))throw new System.InvalidOperationException("Open the saved SampleScene before verification");
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
        Debug.Log("WHACK_SENSOR_OPEN_SCENE: "+path+" start="+(WhackSensorGame.SceneObject("StartButton")!=null));
        SessionState.SetBool("WhackSensorVerification",true);EditorApplication.isPlaying=true;
    }
}
