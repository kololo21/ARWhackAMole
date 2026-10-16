using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhackAMole.Sensors;

[InitializeOnLoad]
public static class WhackSensorSetup
{
    static WhackSensorSetup()
    {
        Debug.Log("WHACK_SENSOR_UPSTREAM_REVISION_3");EditorApplication.delayCall+=EnsureCatalog;
        if(Application.isBatchMode&&SessionState.GetBool("WhackSensorBatchVerification",false))EditorApplication.update+=CheckBatch;
    }
    // CLI entry point for the same saved-scene verification used by the menu.
    public static void VerifyBatch()
    {
        if(!Application.isBatchMode)throw new System.InvalidOperationException("Use the verification menu in an interactive editor");
        if(EditorSceneManager.GetActiveScene().isDirty)throw new System.InvalidOperationException("Unsaved scene changes must be preserved");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",OpenSceneMode.Single);
        SessionState.SetBool("WhackSensorBatchVerification",true);SessionState.SetBool("WhackSensorBatchStarted",false);
        SessionState.SetString("WhackSensorBatchStartMs",WhackSensorGame.Now.ToString(System.Globalization.CultureInfo.InvariantCulture));
        EditorApplication.update+=CheckBatch;Verify();
    }
    private static void CheckBatch()
    {
        double start=double.Parse(SessionState.GetString("WhackSensorBatchStartMs","0"),System.Globalization.CultureInfo.InvariantCulture);
        if(WhackSensorGame.Now-start>120000){Debug.LogError("WHACK_SENSOR_BATCH_TIMEOUT");EditorApplication.Exit(1);return;}
        if(EditorApplication.isPlaying){SessionState.SetBool("WhackSensorBatchStarted",true);return;}
        if(EditorApplication.isPlayingOrWillChangePlaymode||!SessionState.GetBool("WhackSensorBatchStarted",false))return;
        string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Verification/sensor-integration-result.json"));
        bool passed=File.Exists(path)&&File.ReadAllText(path).Contains("\"passed\":true");
        SessionState.SetBool("WhackSensorBatchVerification",false);EditorApplication.update-=CheckBatch;EditorApplication.Exit(passed?0:1);
    }
    [MenuItem("Sensor Game/Prepare Mole Catalog")]
    public static void EnsureCatalog()
    {
        const string path="Assets/Resources/WhackMoleCatalog.asset";
        var catalog=AssetDatabase.LoadAssetAtPath<WhackMoleCatalog>(path);
        bool create=catalog==null,changed=false;
        if(create)catalog=ScriptableObject.CreateInstance<WhackMoleCatalog>();
        Fill(ref catalog.normalRed,"Normal_Red",ref changed);Fill(ref catalog.normalBlue,"Normal_Blue",ref changed);
        Fill(ref catalog.chargeRed,"Windup_Red",ref changed);Fill(ref catalog.chargeBlue,"Windup_Blue",ref changed);
        Fill(ref catalog.flashRed,"Speedy_Red",ref changed);Fill(ref catalog.flashBlue,"Speedy_Blue",ref changed);
        Fill(ref catalog.helmetRed,"Helmet_Red",ref changed);Fill(ref catalog.helmetBlue,"Helmet_Blue",ref changed);
        Fill(ref catalog.balanceRed,"Balance_Red",ref changed);Fill(ref catalog.balanceBlue,"Balance_Blue",ref changed);
        Fill(ref catalog.sleepyRed,"Sleepy_Red",ref changed);Fill(ref catalog.sleepyBlue,"Sleepy_Blue",ref changed);
        Fill(ref catalog.kingRed,"King_Red",ref changed);Fill(ref catalog.kingBlue,"King_Blue",ref changed);
        if(create){Directory.CreateDirectory("Assets/Resources");AssetDatabase.CreateAsset(catalog,path);}
        if(create||changed){EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);}
        Debug.Log("WHACK_SENSOR_CATALOG_READY");
    }
    private static void Fill(ref GameObject field,string name,ref bool changed){if(field!=null)return;field=Load(name);changed=true;}
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
