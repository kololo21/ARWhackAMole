#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Vuforia;
using WhackAMole.Sensors;
// Explicit verification in the real, updated game scene. Sensor packets/tracking are synthetic.
public sealed class WhackRuntimeVerification : MonoBehaviour
{
    private WhackSensorGame game;
    private int phase,redCycles,blueCycles;
    private double started,phaseAt,chargeBefore,timeBefore;
    private string output;
    private HoleSlot flashHole;
    private DwellerSpawner[] spawners;
    private void Start()
    {
        game=GetComponent<WhackSensorGame>();started=phaseAt=WhackSensorGame.Now;
        output=Path.GetFullPath(Path.Combine(Application.dataPath,"../Verification"));Directory.CreateDirectory(output);
        spawners=FindObjectsByType<DwellerSpawner>(FindObjectsSortMode.None);
        foreach(var spawner in spawners)spawner.gameObject.SetActive(false);
        foreach(var hole in game.Holes)hole.GetComponent<ObserverBehaviour>().enabled=false;
        if(VuforiaBehaviour.Instance)VuforiaBehaviour.Instance.enabled=false;
        var originalCamera=Camera.main;if(originalCamera)originalCamera.gameObject.SetActive(false);
        var camera=new GameObject("SensorVerificationCamera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(0,.35f,-.1f);camera.transform.LookAt(new Vector3(0,.02f,.8f));camera.nearClipPlane=.01f;camera.farClipPlane=10;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.12f,.16f);
        game.Manager.gameDuration=60;
        File.WriteAllText(Path.Combine(output,"sensor-integration-result.json"),"{\"passed\":false,\"status\":\"running\"}");
    }
    private void Check(bool yes,string message){if(!yes)throw new Exception(message);}
    private void Found(int i)=>game.Holes[i].SetTrackingForVerification(true);
    private void Cover(int i)=>game.Holes[i].SetTrackingForVerification(false);
    private void Update()
    {
        try
        {
            double now=WhackSensorGame.Now;var m=game.Model;var gm=game.Manager;
            if(now-started>45000)throw new Exception("timeout phase "+phase+": "+m.Result);
            if(phase==0&&m.Ready(m.Red,now)&&m.Ready(m.Blue,now))
            {
                Check(game.Catalog!=null&&game.Holes.Length==3,"updated three-HoleSlot scene and catalog");
                var start=WhackSensorGame.SceneObject("StartButton");Canvas.ForceUpdateCanvases();
                var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,start.GetComponent<RectTransform>().position)};
                var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
                Check(hits.Count>0&&(hits[0].gameObject==start||hits[0].gameObject.transform.IsChildOf(start.transform)),"START pointer accessibility");
                start.GetComponent<Button>().onClick.Invoke();Check(game.IsPlaying&&!WhackSensorGame.SceneObject("TitlePanel").activeSelf,"upstream StartGame and UIController");
                for(int i=0;i<3;i++)Found(i);
                game.Holes[0].Spawn(game.Catalog.normalRed,DwellerColor.Red,30);m.Red.Charge=0;
                Cover(0);Cover(0);Check(gm.RedScore==10&&m.Red.Charge==0,"upstream normal 10 points, offline charge-free and exactly once");
                game.Holes[1].Spawn(game.Catalog.chargeBlue,DwellerColor.Blue,30);m.Blue.Charge=0;
                Cover(1);Check(gm.BlueScore==0&&game.Holes[1].IsAlive,"insufficient charge preserves mole");
                m.Blue.Charge=30;Found(1);Cover(1);Cover(1);
                Check(gm.BlueScore==10&&Math.Abs(m.Blue.Charge-5)<.001,"upstream charge mole 10 points, blue-only spend, no duplicate");
                for(int i=0;i<3;i++)Found(i);m.Red.Charge=m.Blue.Charge=0;
                for(int i=0;i<3;i++)
                {
                    var hole=game.Holes[i];var renderers=hole.GetComponentsInChildren<Renderer>(true);var bounds=new Bounds(hole.transform.position,Vector3.zero);foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                    hole.transform.localScale*=.22f/Mathf.Max(.001f,Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z));hole.transform.SetPositionAndRotation(new Vector3((i-1)*.26f,0,.8f),Quaternion.identity);
                    foreach(var r in hole.GetComponentsInChildren<SkinnedMeshRenderer>(true))r.updateWhenOffscreen=true;
                }
                phase=1;phaseAt=now;
            }
            else if(phase==1&&m.Red.Charge>=35&&m.Blue.Charge>=35)
            {
                Check(WhackSensorGame.SceneObject("RedScoreText").GetComponent<TMP_Text>().text=="RED 10"&&WhackSensorGame.SceneObject("BlueScoreText").GetComponent<TMP_Text>().text=="BLUE 10","upstream individual score UI");
                game.Holes[0].Clear();game.Holes[0].Spawn(game.Catalog.normalRed,DwellerColor.Red,30);
                ScreenCapture.CaptureScreenshot(Path.Combine(output,"sensor-game-charge.png"));chargeBefore=m.Red.Charge;timeBefore=gm.RemainingTime;
                WhackSensorGame.SceneObject("CountContest").GetComponent<Button>().onClick.Invoke();Check(m.MatchBusy&&gm.SensorsPaused,"contest pauses GameManager");
                Cover(0);Check(gm.RedScore==10&&game.Holes[0].IsAlive,"contest does not defeat normal mole");Found(0);
                phase=2;phaseAt=now;
            }
            else if(phase==2&&m.Match==SensorBridge.SensorArenaModel.MatchPhase.Result)
            {
                Check(m.Result=="赤チームの勝ち！"&&m.Red.ShakeCount>m.Blue.ShakeCount&&m.Red.Peak<m.Blue.Peak,"count beats higher acceleration");phase=3;phaseAt=now;
            }
            else if(phase==3&&now-phaseAt>100)
            {
                Check(m.Red.Charge>=Math.Min(100,chargeBefore+25),"winner bonus applied");Check(timeBefore-gm.RemainingTime<.5&&gm.RedScore==10&&gm.BlueScore==10&&!gm.SensorsPaused,"upstream clock resumes with unchanged mole scores");
                redCycles=m.Red.ShakeCount;blueCycles=m.Blue.ShakeCount;ScreenCapture.CaptureScreenshot(Path.Combine(output,"sensor-game-contest.png"));
                game.Holes[0].Clear();WhackSensorGame.SceneObject("FlashMole").GetComponent<Button>().onClick.Invoke();Check(m.Flash!=SensorBridge.SensorArenaModel.FlashPhase.Idle,"warning starts");phase=4;phaseAt=now;
            }
            else if(phase==4)
            {
                foreach(var hole in game.Holes)if(hole.CurrentKind==MoleKind.Flash&&hole.IsAlive)
                {
                    Check(m.Red.CueAck&&m.Blue.CueAck&&hole.Current.activeSelf,"Speedy appears only after both ACKs");flashHole=hole;ScreenCapture.CaptureScreenshot(Path.Combine(output,"sensor-game-flash.png"));phase=5;phaseAt=now;break;
                }
            }
            else if(phase==5&&now-phaseAt>650)
            {
                Check(!flashHole.IsAlive&&gm.RedScore==10&&gm.BlueScore==10,"flash expires without points");gm.EndGame();phase=6;phaseAt=now;
            }
            else if(phase==6&&gm.State==GameState.Result)
            {
                Check(WhackSensorGame.SceneObject("ResultPanel").activeSelf,"upstream result panel");ScreenCapture.CaptureScreenshot(Path.Combine(output,"sensor-game-result.png"));phase=7;phaseAt=now;
            }
            else if(phase==7&&now-phaseAt>700)
            {
                WhackSensorGame.SceneObject("RetryButton").GetComponent<Button>().onClick.Invoke();Check(game.IsPlaying&&gm.RedScore==0&&gm.BlueScore==0&&m.Red.Charge==0&&m.Blue.Charge==0,"retry resets upstream scores and sensor charge");
                for(int i=0;i<3;i++)Found(i);
                foreach(var spawner in spawners)spawner.gameObject.SetActive(true);
                phase=8;phaseAt=now;
            }
            else if(phase==8&&now-phaseAt>2000&&game.Holes.Any(h=>h.IsAlive))
            {
                Check(game.Holes.Count(h=>!h.IsEmpty)<=2,"upstream random maxAlive setting");
                var alive=game.Holes.First(h=>h.IsAlive);var color=alive.CurrentColor;var kind=alive.CurrentKind;
                m.Red.Charge=m.Blue.Charge=100;var before=gm.RemainingTime;
                Cover(System.Array.IndexOf(game.Holes,alive));
                Check((color==DwellerColor.Red?gm.RedScore:gm.BlueScore)==10,"upstream randomly spawned mole uses sensor-aware hand cover");
                Check(m.Get(WhackSensorGame.Team(color)).Charge==(kind==MoleKind.Charge?75:100),"random prefab charge classification");
                Check(WhackSensorGame.SceneObject("BGM").GetComponent<AudioSource>().isPlaying,"upstream game BGM is playing");
                File.WriteAllText(Path.Combine(output,"sensor-integration-result.json"),"{\"passed\":true,\"upstreamCommit\":\"fab3904\",\"scene\":\"Assets/Scenes/SampleScene.unity\",\"syntheticSensors\":true,\"syntheticTracking\":true,\"redScore\":10,\"blueScore\":10,\"redCycles\":"+redCycles+",\"blueCycles\":"+blueCycles+",\"actualCameraTested\":false}");
                Debug.Log("WHACK_SENSOR_INTEGRATION_PASSED: upstream GameManager, HoleSlot, UIController, normal 10 points, charge 10 points, no duplicates, contest clock pause/reward, ACK/Speedy/expiry, result/retry, random spawner and BGM");EditorApplication.isPlaying=false;enabled=false;
            }
        }
        catch(Exception e){File.WriteAllText(Path.Combine(output,"sensor-integration-result.json"),"{\"passed\":false,\"phase\":"+phase+",\"error\":\""+e.Message.Replace("\\","\\\\").Replace("\"","\\\"")+"\"}");Debug.LogError("WHACK_SENSOR_INTEGRATION_FAILED: "+e);EditorApplication.isPlaying=false;enabled=false;}
    }
}
#endif
