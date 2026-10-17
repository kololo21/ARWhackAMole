#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using Vuforia;
using WhackAMole.Sensors;
// Explicit verification in the real, updated game scene. Sensor packets/tracking are synthetic.
public sealed class WhackRuntimeVerification : MonoBehaviour
{
    private WhackSensorGame game;
    private int phase,redCirclePoints,blueCirclePoints,orientationMode;
    private readonly System.Collections.Generic.HashSet<SensorBridge.SensorArenaModel.MatchPhase> capturedContest=new System.Collections.Generic.HashSet<SensorBridge.SensorArenaModel.MatchPhase>();
    private double started,phaseAt,chargeBefore,timeBefore;
    private string output;
    private HoleSlot flashHole;
    private DwellerSpawner[] spawners;
    private int spends;
    private RenderTexture batchTarget;
    private Vector3 retainedHelmetPosition,retainedChargePosition;
    private void Start()
    {
        game=GetComponent<WhackSensorGame>();started=phaseAt=WhackSensorGame.Now;
        game.Model.ChargeSpent+=(player,amount,before)=>spends++;
        output=Path.GetFullPath(Path.Combine(Application.dataPath,"../Verification"));Directory.CreateDirectory(output);
        spawners=FindObjectsByType<DwellerSpawner>(FindObjectsSortMode.None);
        foreach(var spawner in spawners)spawner.gameObject.SetActive(false);
        foreach(var hole in game.Holes)hole.GetComponent<ObserverBehaviour>().enabled=false;
        if(VuforiaBehaviour.Instance)VuforiaBehaviour.Instance.enabled=false;
        var originalCamera=Camera.main;if(originalCamera)originalCamera.gameObject.SetActive(false);
        var camera=new GameObject("SensorVerificationCamera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(0,.35f,-.1f);camera.transform.LookAt(new Vector3(0,.02f,.8f));camera.nearClipPlane=.01f;camera.farClipPlane=10;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.12f,.16f);
        camera.gameObject.AddComponent<AudioListener>();
        if(Application.isBatchMode)
        {
            batchTarget=new RenderTexture(1600,900,24);camera.targetTexture=batchTarget;
            var canvas=WhackSensorGame.SceneObject("Canvas").GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;
        }
        game.Manager.gameDuration=60;
        File.WriteAllText(Path.Combine(output,"sensor-integration-result.json"),"{\"passed\":false,\"status\":\"running\"}");
    }
    private void Check(bool yes,string message){if(!yes)throw new Exception(message);}
    private void Screenshot(string filename,bool renderNow=false)
    {
        if(!Application.isBatchMode&&!renderNow){ScreenCapture.CaptureScreenshot(Path.Combine(output,filename));return;}
        var canvas=WhackSensorGame.SceneObject("Canvas").GetComponent<Canvas>();var camera=Camera.main;
        var mode=canvas.renderMode;var previousCamera=canvas.worldCamera;float plane=canvas.planeDistance;
        var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;bool previousEnabled=canvas.enabled;
        var items=canvas.GetComponentsInChildren<Transform>(true);var layers=items.Select(t=>t.gameObject.layer).ToArray();
        var overlay=new GameObject("VerificationOverlayCamera").AddComponent<Camera>();
        overlay.CopyFrom(camera);overlay.enabled=false;overlay.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);
        overlay.clearFlags=CameraClearFlags.SolidColor;overlay.backgroundColor=Color.clear;overlay.cullingMask=1<<5;
        overlay.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);
        var texture=new Texture2D(1600,900,TextureFormat.RGBA32,false);
        try
        {
            // Screen-space overlay is outside camera post-processing. Bake the AR
            // and UI separately, then composite the premultiplied UI over the AR.
            canvas.enabled=false;camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();var ar=texture.GetPixels32();
            foreach(var item in items)item.gameObject.layer=5;
            canvas.enabled=true;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=overlay;canvas.planeDistance=.5f;
            overlay.targetTexture=target;Canvas.ForceUpdateCanvases();
            WhackSensorGame.SceneObject("SensorGameHUD").GetComponent<SensorGameHud>().Refresh(game.Model.ActionMessage);
            Canvas.ForceUpdateCanvases();overlay.Render();RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();var dark=texture.GetPixels32();
            // Recover UI coverage (including the full-screen event shade) from
            // a black/white matte, independent of the UI shader alpha blend.
            overlay.backgroundColor=Color.white;overlay.Render();RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();var light=texture.GetPixels32();
            Check(light[0].r-dark[0].r>10,"verification UI matte preserves the camera clear colour");
            var linear=new float[256];var encoded=new byte[4096];bool gamma=QualitySettings.activeColorSpace==ColorSpace.Gamma;
            for(int i=0;i<linear.Length;i++)linear[i]=gamma?i/255f:Mathf.GammaToLinearSpace(i/255f);
            for(int i=0;i<encoded.Length;i++)encoded[i]=(byte)Mathf.RoundToInt(255*(gamma?i/4095f:Mathf.LinearToGammaSpace(i/4095f)));
            for(int i=0;i<ar.Length;i++)
                ar[i]=new Color32(Composite(ar[i].r,dark[i].r,light[i].r,linear,encoded),Composite(ar[i].g,dark[i].g,light[i].g,linear,encoded),Composite(ar[i].b,dark[i].b,light[i].b,linear,encoded),255);
            texture.SetPixels32(ar);texture.Apply();File.WriteAllBytes(Path.Combine(output,filename),texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=previousTarget;RenderTexture.active=previousActive;
            canvas.renderMode=mode;canvas.worldCamera=previousCamera;canvas.planeDistance=plane;canvas.enabled=previousEnabled;
            for(int i=0;i<items.Length;i++)items[i].gameObject.layer=layers[i];
            DestroyImmediate(overlay.gameObject);DestroyImmediate(target);DestroyImmediate(texture);
            // A forced capture temporarily uses a different resolution/camera.
            // Restore screen-space anchors as well as the Canvas properties.
            Canvas.ForceUpdateCanvases();
            WhackSensorGame.SceneObject("SensorGameHUD").GetComponent<SensorGameHud>().Refresh(game.Model.ActionMessage);
            Canvas.ForceUpdateCanvases();
        }
    }
    private static byte Composite(byte scene,byte dark,byte light,float[] linear,byte[] encoded)
        =>encoded[Mathf.RoundToInt(Mathf.Clamp01(linear[dark]+linear[scene]*Mathf.Clamp01(linear[light]-linear[dark]))*4095)];
    private void Found(int i)=>game.Holes[i].SetTrackingForVerification(true);
    private void Cover(int i)=>game.Holes[i].SetTrackingForVerification(false);
    private void Update()
    {
        try
        {
            double now=WhackSensorGame.Now;var m=game.Model;var gm=game.Manager;
            if(phase==2&&capturedContest.Add(m.Match))
            {
                Screenshot("contest-"+m.Match.ToString().ToLowerInvariant()+".png",true);
                Check(WhackSensorGame.SceneObject("EventPanel").activeSelf,"Unity event panel shows each contest stage");
            }
            if(now-started>65000)throw new Exception("timeout phase "+phase+": "+m.Result);
            if(phase==0&&m.Ready(m.Red,now)&&m.Ready(m.Blue,now)&&m.Red.Voice.Calibrated&&m.Blue.Voice.Calibrated)
            {
                Check(game.Catalog!=null&&game.Holes.Length==3,"updated three-HoleSlot scene and catalog");
                CameraDisplayChecks.Run(output);
                _=game.Receiver.SendFeedback("red","test","verify-voice",1000);
                _=game.Receiver.SendFeedback("blue","test","verify-voice",1000);
                game.CalibrateTilt("red");phase=23;phaseAt=now;
            }
            else if(phase==23&&m.Red.Voice.Verified&&m.Blue.Voice.Verified&&game.CalibrationStatus("red").Contains("完了"))
            {
                Check(game.CanSpawn(MoleKind.Sleepy,DwellerColor.Red),"voice preparation enables Sleepy after low-RMS test");
                Screenshot("preparation.png",true);phase=20;phaseAt=now;
            }
            else if(phase==20&&now-phaseAt>150)
            {
                // Let overlay canvases rebuild after the isolated camera render.
                var start=WhackSensorGame.SceneObject("StartButton");Screenshot("character-start.png");Canvas.ForceUpdateCanvases();
                var startCanvas=start.GetComponentInParent<Canvas>().rootCanvas;
                var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(startCanvas.renderMode==RenderMode.ScreenSpaceOverlay?null:startCanvas.worldCamera,start.GetComponent<RectTransform>().position)};
                var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
                Debug.Log("START_VERIFY: position="+pointer.position+" active="+start.activeInHierarchy+" hits="+string.Join(",",hits.Select(h=>h.gameObject.name)));
                Check(hits.Count>0&&(hits[0].gameObject==start||hits[0].gameObject.transform.IsChildOf(start.transform)),"START pointer accessibility");
                start.GetComponent<Button>().onClick.Invoke();Check(game.IsPlaying&&!WhackSensorGame.SceneObject("TitlePanel").activeSelf,"upstream StartGame and UIController");
                CameraDisplaySettings.Instance.Configure(true,true);Check(!CameraDisplaySettings.Instance.IsFlipped,"camera controls are locked during a round");
                for(int i=0;i<3;i++)Found(i);
                game.Holes[0].Spawn(game.Catalog.normalRed,DwellerColor.Red,30);m.Red.Charge=0;
                Cover(0);Cover(0);Check(gm.RedScore==10&&m.Red.Charge==0,"upstream normal 10 points, offline charge-free and exactly once");
                game.Holes[1].Spawn(game.Catalog.chargeBlue,DwellerColor.Blue,30);m.Blue.Charge=0;
                Cover(1);Check(gm.BlueScore==0&&game.Holes[1].IsAlive,"insufficient charge preserves mole");
                CheckRetainedDisplay(game.Holes[1]);
                Check(game.Holes[1].Instruction.Contains("チャージ不足"),"failed charge hit clearly says insufficient charge");
                m.Blue.Charge=49;Found(1);Cover(1);
                Check(gm.BlueScore==0&&game.Holes[1].IsAlive&&m.Blue.Charge==49&&spends==0,"49 charge cannot defeat or spend");
                m.Blue.Charge=60;Found(1);Cover(1);Cover(1);
                Check(gm.BlueScore==30&&Math.Abs(m.Blue.Charge-10)<.001&&spends==1,"upstream charge mole 10 points, blue-only 50 spend, no duplicate");
                Check(WhackSensorGame.SceneObject("BlueChargeAction").GetComponent<TMP_Text>().text.Contains("-50"),"visible blue spend feedback");
                for(int i=0;i<3;i++)Found(i);m.Red.Charge=0;
                for(int i=0;i<3;i++)
                {
                    var hole=game.Holes[i];var renderers=hole.GetComponentsInChildren<Renderer>(true);var bounds=new Bounds(hole.transform.position,Vector3.zero);foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                    hole.transform.localScale*=.22f/Mathf.Max(.001f,Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z));hole.transform.SetPositionAndRotation(new Vector3((i-1)*.26f,0,.8f),Quaternion.identity);
                    foreach(var r in hole.GetComponentsInChildren<SkinnedMeshRenderer>(true))r.updateWhenOffscreen=true;
                }
                Screenshot("sensor-game-spend.png");
                phase=1;phaseAt=now;
            }
            else if(phase==1&&m.Red.Charge>=35&&m.Blue.Charge>=35)
            {
                Check(WhackSensorGame.SceneObject("RedScoreText").GetComponent<TMP_Text>().text=="RED 10"&&WhackSensorGame.SceneObject("BlueScoreText").GetComponent<TMP_Text>().text=="BLUE 30","upstream individual score UI");
                game.Holes[0].Clear();game.Holes[0].Spawn(game.Catalog.normalRed,DwellerColor.Red,30);
                Screenshot("sensor-game-charge.png");chargeBefore=m.Red.Charge;timeBefore=gm.RemainingTime;
                WhackSensorGame.SceneObject("CountContest").GetComponent<Button>().onClick.Invoke();Check(m.MatchBusy&&gm.SensorsPaused,"contest pauses GameManager");
                Cover(0);Check(gm.RedScore==10&&game.Holes[0].IsAlive,"contest does not defeat normal mole");Found(0);
                phase=2;phaseAt=now;
            }
            else if(phase==2&&m.Match==SensorBridge.SensorArenaModel.MatchPhase.Result)
            {
                Check(m.Result=="赤チームの勝ち！"&&m.Red.CirclePoints>m.Blue.CirclePoints&&m.Red.Peak<m.Blue.Peak,"rotation points beat higher acceleration");Screenshot("contest-result.png",true);Check(gm.SensorsPaused,"contest result keeps board paused");phase=3;phaseAt=now;
            }
            else if(phase==3&&m.Match==SensorBridge.SensorArenaModel.MatchPhase.Idle)
            {
                Check(m.Red.Charge>=Math.Min(100,chargeBefore+25),"winner bonus applied");Check(timeBefore-gm.RemainingTime<.5&&gm.RedScore==10&&gm.BlueScore==30&&!gm.SensorsPaused,"upstream clock resumes with unchanged mole scores");
                redCirclePoints=m.Red.CirclePoints;blueCirclePoints=m.Blue.CirclePoints;Screenshot("sensor-game-contest.png",true);
                game.Holes[0].Clear();WhackSensorGame.SceneObject("FlashMole").GetComponent<Button>().onClick.Invoke();Check(m.Flash!=SensorBridge.SensorArenaModel.FlashPhase.Idle,"warning starts");phase=4;phaseAt=now;
            }
            else if(phase==4)
            {
                foreach(var hole in game.Holes)if(hole.CurrentKind==MoleKind.Flash&&hole.IsAlive)
                {
                    Check(hole.Current.activeSelf,"Speedy appears after the PC warning");flashHole=hole;Screenshot("sensor-game-flash.png");phase=5;phaseAt=now;break;
                }
            }
            else if(phase==5&&now-phaseAt>2000)
            {
                Check(!flashHole.IsAlive&&gm.RedScore==10&&gm.BlueScore==30,"flash expires without points");gm.EndGame();phase=6;phaseAt=now;
            }
            else if(phase==6&&gm.State==GameState.Result)
            {
                WhackSensorGame.SceneObject("SensorGameHUD").GetComponent<SensorGameHud>().Refresh("");
                Check(WhackSensorGame.SceneObject("CameraSettingsPanel").activeSelf,"camera settings available before retry");
                CameraDisplaySettings.Instance.Configure(true,false);Check(CameraDisplaySettings.Instance.FlipHorizontal,"result permits next-round configuration");CameraDisplaySettings.Instance.Configure(false,false);
                Check(WhackSensorGame.SceneObject("ResultPanel").activeSelf,"upstream result panel");Screenshot("sensor-game-result.png",true);
                var retry=WhackSensorGame.SceneObject("RetryButton").GetComponent<RectTransform>();
                var retryCanvas=retry.GetComponentInParent<Canvas>();
                var retryPointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(retryCanvas.renderMode==RenderMode.ScreenSpaceOverlay?null:retryCanvas.worldCamera,retry.position)};
                var retryHits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(retryPointer,retryHits);
                Check(retryHits.Count>0&&(retryHits[0].gameObject==retry.gameObject||retryHits[0].gameObject.transform.IsChildOf(retry)),"retry button stays accessible above preparation controls");phase=7;phaseAt=now;
            }
            else if(phase==7&&now-phaseAt>700)
            {
                WhackSensorGame.SceneObject("RetryButton").GetComponent<Button>().onClick.Invoke();Check(game.IsPlaying&&gm.RedScore==0&&gm.BlueScore==0&&m.Red.Charge==0&&m.Blue.Charge==0,"retry resets upstream scores and sensor charge");
                Check(!WhackSensorGame.SceneObject("BlueChargeAction").GetComponent<TMP_Text>().text.Contains("消費！"),"retry clears spend feedback");
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
                Check(m.Get(WhackSensorGame.Team(color)).Charge==(kind==MoleKind.Charge?100-m.ChargeCost:100),"random prefab charge classification");
                Check(WhackSensorGame.SceneObject("BGM").GetComponent<AudioSource>().isPlaying,"upstream game BGM is playing");
                foreach(var spawner in spawners)spawner.gameObject.SetActive(false);
                gm.StartGame();ClearBoard();
                Check(game.Holes[0].Spawn(game.Catalog.helmetRed,DwellerColor.Red,15),"helmet spawn");
                phase=22;phaseAt=now;
            }
            else if(phase==22&&now-phaseAt>600)
            {
                Screenshot("helmet-before-hit.png",true);
                retainedHelmetPosition=game.Holes[0].transform.position;
                Cover(0);Cover(0);Check(game.Holes[0].IsAlive&&gm.RedScore==0,"helmet first hit breaks armour without points");
                Check(game.Holes[0].Current.GetComponents<HelmetBreakEffect>().Length==1,"repeated cover starts only one helmet break effect");
                CheckRetainedDisplay(game.Holes[0]);
                Check(game.Holes[0].Instruction.Contains("装甲破壊"),"first helmet hit says armour broken");
                Check(game.Holes[1].Spawn(game.Catalog.chargeBlue,DwellerColor.Blue,15),"feedback charge mole spawn");m.Blue.Charge=0;
                retainedChargePosition=game.Holes[1].transform.position;Cover(1);Cover(1);CheckRetainedDisplay(game.Holes[1]);
                Check(!game.Holes[1].Spawn(game.Catalog.normalBlue,DwellerColor.Blue,15),"retained display cannot spawn under a covered marker");
                game.Holes[0].transform.position+=Vector3.one;game.Holes[1].transform.position+=Vector3.one;
                phase=9;phaseAt=now;
            }
            else if(phase==9&&now-phaseAt>200)
            {
                CheckRetainedDisplay(game.Holes[0]);CheckRetainedDisplay(game.Holes[1]);
                Check(Vector3.Distance(game.Holes[0].transform.position,retainedHelmetPosition)<.001f&&Vector3.Distance(game.Holes[1].transform.position,retainedChargePosition)<.001f,"covered holes keep their last tracked poses");
                WhackSensorGame.SceneObject("SensorGameHUD").GetComponent<SensorGameHud>().Refresh("");
                foreach(int i in new[]{0,1})Check(WhackSensorGame.SceneObject("Instruction_"+game.Holes[i].name).activeSelf,"covered enemy instructions remain visible");
                Check(game.Holes[0].Current.GetComponent<HelmetBreakEffect>().IsPlaying,"helmet flies away while body and hole remain visible");
                Screenshot("helmet-break-effect.png",true);
                Screenshot("hole-feedback-retained.png",true);Screenshot("character-helmet.png");
                m.Blue.Charge=100;Cover(1);Check(gm.BlueScore==0&&game.Holes[1].IsAlive&&m.Blue.Charge==100,"charging under the hand does not count as another hit");
                game.Holes[1].ExpireNow();CheckRetainedDisplay(game.Holes[1]);Check(gm.BlueScore==0,"expiry under the hand does not award points");
                game.Holes[1].Clear();Found(1);Found(0);phase=21;phaseAt=now;
            }
            else if(phase==21&&now-phaseAt>750)
            {
                var helmetEffect=game.Holes[0].Current.GetComponent<HelmetBreakEffect>();
                Check(!helmetEffect.IsPlaying&&game.Holes[0].Current.transform.Find("HelmetBreakFragments")==null,"helmet break finishes and removes fragments without defeating the mole");
                Screenshot("helmet-break-finished.png",true);
                Cover(0);Cover(0);Check(gm.RedScore==20,"helmet second hit scores once");CheckRetainedDisplay(game.Holes[0]);
                Debug.Log("WHACK_HELMET_EFFECT_CHECKS_PASSED: first-hit-only effect, retained body/hole, debris cleanup, second-hit score");
                game.Holes[0].Clear();Found(0);game.Holes[0].Spawn(game.Catalog.balanceBlue,DwellerColor.Blue,15);
                Cover(0);Check(game.Holes[0].IsAlive&&gm.BlueScore==0,"balance blocks cover before tilt");Found(0);
                _=game.Receiver.SendFeedback("blue","test","verify-tilt",1000);phase=10;phaseAt=now;
            }
            else if(phase==10&&now-phaseAt>300)
            {
                Check(!game.Holes[0].IsWeakened,"short tilt does not weaken");phase=11;
            }
            else if(phase==11&&game.Holes[0].IsWeakened)
            {
                Screenshot("character-balance.png");Cover(0);Cover(0);Check(gm.BlueScore==30,"held tilt then cover scores once");
                game.Holes[0].Clear();Found(0);
                game.Holes[1].Spawn(game.Catalog.sleepyRed,DwellerColor.Red,15);game.Holes[2].Spawn(game.Catalog.sleepyRed,DwellerColor.Red,15);
                Cover(1);Check(game.Holes[1].IsAlive&&gm.RedScore==20,"sleepy blocks cover before voice");Found(1);
                _=game.Receiver.SendFeedback("blue","test","verify-voice",1000);phase=12;phaseAt=now;
            }
            else if(phase==12&&now-phaseAt>450)
            {
                Check(!game.Holes[1].IsWeakened&&!game.Holes[2].IsWeakened,"other team's voice cannot wake red");
                Screenshot("sleepy-awaiting-voice.png",true);_=game.Receiver.SendFeedback("red","test","verify-voice",1000);phase=13;phaseAt=now;
            }
            else if(phase==13&&game.Holes[1].IsWeakened)
            {
                Check(!game.Holes[2].IsWeakened,"one voice wakes only the oldest same-colour enemy");
                Screenshot("character-sleepy.png");Cover(1);Cover(1);Check(gm.RedScore==50,"voice then cover scores once");
                ClearBoard();m.Blue.Charge=74;
                game.Holes[0].Spawn(game.Catalog.kingBlue,DwellerColor.Blue,15);Cover(0);
                Check(game.Holes[0].IsAlive&&m.Blue.Charge==74&&gm.BlueScore==30,"king rejects 74");Found(0);
                m.Blue.Charge=100;Cover(0);Cover(0);Check(m.Blue.Charge==25&&gm.BlueScore==80,"king spends 75 and scores 50 once");
                Screenshot("character-king-spend.png");ClearBoard();
                game.BeginFlash();Check(game.FlashWarning,"PC warning before speed enemy");phase=14;phaseAt=now;
            }
            else if(phase==14&&now-phaseAt>100)
            {
                Screenshot("character-speed-warning.png",true);Cover(Array.IndexOf(game.Holes,game.FlashHole));phase=19;phaseAt=now;
            }
            else if(phase==19&&now-phaseAt>100)
            {
                Check(m.Flash==SensorBridge.SensorArenaModel.FlashPhase.Idle&&game.FlashHole==null,"pre-cover cancels speed warning");
                Screenshot("speed-precover.png",true);CheckFlashPreCoverBoundaries();
                for(int i=0;i<3;i++)Found(i);game.BeginFlash();Check(game.FlashWarning,"restart PC warning");phase=15;
            }
            else if(phase==15&&game.Holes.Any(h=>h.IsAlive&&h.CurrentKind==MoleKind.Flash))
            {
                var hole=game.Holes.First(h=>h.IsAlive&&h.CurrentKind==MoleKind.Flash);int score=hole.CurrentColor==DwellerColor.Red?gm.RedScore:gm.BlueScore;
                var color=hole.CurrentColor;Cover(Array.IndexOf(game.Holes,hole));Cover(Array.IndexOf(game.Holes,hole));
                Check((color==DwellerColor.Red?gm.RedScore:gm.BlueScore)==score+30,"speed cover scores 30 once");
                Screenshot("speed-success.png",true);Check(game.FlashHole==null&&game.EventMessage.Contains("成功"),"successful Speedy immediately shows result instead of hit prompt");gm.EndGame();for(int i=0;i<3;i++)Cover(i);
                Check(game.Holes.All(h=>!h.HasDisplayPose&&!h.HasInteractionFeedback),"result clears retained poses and interaction feedback");
                phase=16;phaseAt=now;
            }
            else if(phase==16&&gm.State==GameState.Result&&now-phaseAt>100)
            {
                WhackSensorGame.SceneObject("RetryButton").GetComponent<Button>().onClick.Invoke();
                Check(game.IsPlaying&&game.Holes.All(h=>!h.IsAlive)&&gm.RedScore==0&&gm.BlueScore==0&&m.Red.Charge==0&&m.Blue.Charge==0&&game.FlashHole==null,"all character and warning states reset on retry");
                Check(game.Holes.All(h=>!h.HasDisplayPose),"retry does not show holes before reacquisition");
                Debug.Log("WHACK_RETAINED_HOLE_CHECKS_PASSED: visible rejected hits/armour, retained poses/labels, no covered re-hit/spawn, expiry and retry reset");
                phase=25;phaseAt=now;gm.StartGame();ClearBoard();game.Holes[0].Spawn(game.Catalog.helmetRed,DwellerColor.Red,15);Cover(0);Cover(0);
            }
            else if(phase==25&&now-phaseAt>200)
            {
                CheckHelmetEffectScale("helmet-early.png");ClearBoard();game.Holes[0].Spawn(game.Catalog.helmetBlue,DwellerColor.Blue,15);phase=26;phaseAt=now;
            }
            else if(phase==26&&now-phaseAt>150)
            {
                Cover(0);Found(0);Cover(0);Check(game.Holes[0].Current.GetComponents<HelmetBreakEffect>().Length==1&&gm.BlueScore==0,"rapid helmet reacquisition does not replay break or defeat");phase=27;phaseAt=now;
            }
            else if(phase==27&&now-phaseAt>200)
            {
                CheckHelmetEffectScale("helmet-emerging.png");ClearBoard();
                gm.EndGame();orientationMode=0;phase=28;phaseAt=now;
            }
            else if(phase==28)
            {
                CameraDisplaySettings.Instance.Configure((orientationMode&1)!=0,(orientationMode&2)!=0);
                gm.StartGame();ClearBoard();game.Holes[0].Spawn(game.Catalog.helmetRed,DwellerColor.Red,15);
                game.BeginFlash();Check(game.FlashWarning,"orientation check reserves target hole");phase=29;phaseAt=now;
            }
            else if(phase==29&&now-phaseAt>600)
            {
                Cover(0);phase=30;phaseAt=now;
            }
            else if(phase==30&&now-phaseAt>200)
            {
                // Check the live layout before the offscreen capture changes resolution.
                Canvas.ForceUpdateCanvases();
                WhackSensorGame.SceneObject("SensorGameHUD").GetComponent<SensorGameHud>().Refresh(m.ActionMessage);
                var camera=Camera.main;var canvas=WhackSensorGame.SceneObject("Canvas").GetComponent<Canvas>();
                var screen=CameraDisplaySettings.ToDisplayPoint(camera.WorldToScreenPoint(game.FlashHole.transform.position),camera);
                Vector2 expected;RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,screen,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out expected);
                var target=(RectTransform)WhackSensorGame.SceneObject("FlashTarget").transform;
                Check(Vector2.Distance(target.anchoredPosition,expected+new Vector2(0,30))<.1f,"speed ring follows displayed hole in orientation "+orientationMode+": actual="+target.anchoredPosition+" expected="+(expected+new Vector2(0,30)));
                Screenshot("helmet-speed-orientation-"+orientationMode+".png",true);
                gm.EndGame();orientationMode++;phase=orientationMode<4?28:31;
            }
            else if(phase==31)
            {
                CameraDisplaySettings.Instance.Configure(false,false);
                Debug.Log("WHACK_FOCUS_UI_CHECKS_PASSED: low-RMS preparation, remote tilt acknowledgement, central contest stages and reward hold, Speedy outcomes, early helmet effects");
                File.WriteAllText(Path.Combine(output,"sensor-integration-result.json"),"{\"passed\":true,\"scene\":\"Assets/Scenes/SampleScene.unity\",\"syntheticSensors\":true,\"syntheticTracking\":true,\"chargeCost\":"+m.ChargeCost+",\"kingCost\":"+m.KingChargeCost+",\"characters\":7,\"helmet\":true,\"balance\":true,\"sleepy\":true,\"pcSpeedWarning\":true,\"redCirclePoints\":"+redCirclePoints+",\"blueCirclePoints\":"+blueCirclePoints+",\"actualCameraTested\":false}");
                Debug.Log("WHACK_SENSOR_INTEGRATION_PASSED: all seven kinds, helmet two hits, held tilt, voice targeting, 50/75 charge, score multipliers, PC warning/expiry/pre-cover, retry, random spawn, contest and BGM");EditorApplication.isPlaying=false;enabled=false;
            }
        }
        catch(Exception e){File.WriteAllText(Path.Combine(output,"sensor-integration-result.json"),"{\"passed\":false,\"phase\":"+phase+",\"error\":\""+e.Message.Replace("\\","\\\\").Replace("\"","\\\"")+"\"}");Debug.LogError("WHACK_SENSOR_INTEGRATION_FAILED: "+e);EditorApplication.isPlaying=false;enabled=false;}
    }
    private void ClearBoard(){game.ClearEventResultForVerification();for(int i=0;i<game.Holes.Length;i++){game.Holes[i].Clear();Found(i);}}
    private void CheckHelmetEffectScale(string filename)
    {
        var hole=game.Holes[0];var effect=hole.Current.GetComponent<HelmetBreakEffect>();
        Check(effect.IsPlaying&&hole.IsAlive,"early helmet effect retains live body");
        var fragments=hole.Current.transform.Find("HelmetBreakFragments");
        Check(fragments!=null&&fragments.childCount==8,"helmet effect has its shards");
        Screenshot(filename,true);
        Check(fragments.GetChild(0).lossyScale.magnitude>.005f,"early helmet shards use the full-size model reference: "+fragments.GetChild(0).lossyScale.magnitude);
    }
    private void CheckRetainedDisplay(HoleSlot hole)
    {
        Check(!hole.IsTracked&&hole.HasDisplayPose,"display retention does not fake tracking");
        var renderers=hole.GetComponentsInChildren<Renderer>(true);
        Check(renderers.Length>0&&renderers.All(r=>r.enabled),"hole and mole renderers stay enabled while covered");
    }
    private void CheckFlashPreCoverBoundaries()
    {
        var model=game.Model;double lead=model.WarningLeadMs;
        int red=game.Manager.RedScore,blue=game.Manager.BlueScore;
        double redCharge=model.Red.Charge,blueCharge=model.Blue.Charge;
        try
        {
            model.WarningLeadMs=100;
            for(int scenario=0;scenario<3;scenario++)
            {
                ClearBoard();game.BeginFlash();var hole=game.FlashHole;
                Check(hole!=null&&game.FlashWarning,"boundary regression starts a fresh reservation");
                int index=Array.IndexOf(game.Holes,hole);double boundary=model.FlashStartMs;
                game.AdvanceFrame(boundary-1);Check(game.FlashWarning,"one millisecond before start is still warning");
                Cover(index);
                // Cover+Found can also arrive between consecutive LateUpdate calls.
                if(scenario==1)Found(index);
                // The cancellation must survive a model transition to Visible.
                if(scenario==2)model.Tick(boundary+1);
                game.AdvanceFrame(boundary+1);
                Check(model.Flash==SensorBridge.SensorArenaModel.FlashPhase.Idle&&game.FlashHole==null,
                    "pre-cover at start boundary releases reservation, scenario "+scenario);
                Found(index);game.AdvanceFrame(boundary+2);
                Check(game.Holes.All(h=>!h.IsAlive)&&game.FlashHole==null,"reacquiring inside 900ms cannot spawn a cancelled Speedy");
                Check(game.Manager.RedScore==red&&game.Manager.BlueScore==blue&&model.Red.Charge==redCharge&&model.Blue.Charge==blueCharge,
                    "boundary cancellation neither scores nor spends charge");
            }
            Debug.Log("WHACK_FLASH_BOUNDARY_CHECKS_PASSED: pre-cover across start, same-frame reacquisition, already-visible model, no late spawn/score/spend");
        }
        finally
        {
            model.WarningLeadMs=lead;model.CancelFlash("boundary verification cleanup");game.AdvanceFrame(WhackSensorGame.Now);ClearBoard();
        }
    }
    private void OnDestroy(){if(batchTarget!=null)DestroyImmediate(batchTarget);}
}
#endif
