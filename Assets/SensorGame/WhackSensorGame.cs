using System;
using System.Linq;
using SensorBridge;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WhackAMole.Sensors
{
    // Sensor adapter only. GameManager, HoleSlot, UIController and AudioManager own gameplay.
    public sealed class WhackSensorGame : MonoBehaviour
    {
        public string serverUrl="ws://127.0.0.1:8787/ws", roomKey="demo-room-change-me-2026";
        public bool allowSimulated=false, automaticSensorEvents=true;
        public readonly SensorArenaModel Model=new SensorArenaModel();
        public WhackMoleCatalog Catalog {get;private set;}
        public HoleSlot[] Holes {get;private set;}
        public SensorBridgeReceiver Receiver {get;private set;}
        public bool VerificationMode {get;private set;}
        public GameManager Manager {get;private set;}
        public bool IsPlaying => Manager != null && Manager.State == GameState.Playing;
        public static WhackSensorGame Instance {get;private set;}
        public static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private SensorGameHud hud;
        private HoleSlot reserved;
        private DwellerColor flashColor;
        private bool flashSpawned, contestPending;
        private double nextContestAt=20,nextFlashAt=12;
        private string notice="通常もぐらはチャージ不要。ぜんまいだけチャージ25を消費します";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(FindObjectsByType<HoleSlot>(FindObjectsSortMode.None).Length==0)return;
            var game=FindFirstObjectByType<WhackSensorGame>();
            if(!game)game=(SceneObject("GameManager")??new GameObject("SensorGame")).AddComponent<WhackSensorGame>();
#if UNITY_EDITOR
            if(UnityEditor.SessionState.GetBool("WhackSensorVerification",false))
            {
                UnityEditor.SessionState.SetBool("WhackSensorVerification",false);
                game.VerificationMode=true;game.allowSimulated=true;game.automaticSensorEvents=false;game.roomKey="whack-integration-check-123456";
                game.gameObject.AddComponent<WhackRuntimeVerification>();
            }
#endif
        }
        public static GameObject SceneObject(string name) => Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(o=>o.name==name&&o.scene.IsValid()&&o.scene==SceneManager.GetActiveScene());
        private void Awake()
        {
            Instance=this;Application.runInBackground=true;
            Catalog=Resources.Load<WhackMoleCatalog>("WhackMoleCatalog");
            Holes=FindObjectsByType<HoleSlot>(FindObjectsInactive.Include,FindObjectsSortMode.None).OrderBy(o=>o.name).ToArray();
            Receiver=GetComponent<SensorBridgeReceiver>()??gameObject.AddComponent<SensorBridgeReceiver>();
            Receiver.SampleReceived+=OnSample;Receiver.PresenceChanged+=Model.Presence;
            Receiver.FeedbackAcknowledged+=a=>Model.Acknowledge(a,Now);
            Model.Feedback+=s=>{_ = Receiver.SendFeedback(s.Player,s.Kind,s.Message,s.DurationMs,s.Id);};
            var background=SceneObject("HudPanel")?.GetComponent<Image>();if(background)background.raycastTarget=false;
            hud=SensorGameHud.Create(this,SceneObject("Canvas"));
        }
        private void OnSample(SensorPacket packet)
        {
            Model.Ingest(packet,Now);
            // Outside a round, keep connection/freshness but do not accumulate playable charge.
            if(!IsPlaying)Model.Red.Charge=Model.Blue.Charge=0;
        }
        private void Start()
        {
            Manager=GameManager.Instance;
            if(Manager==null){Debug.LogError("Sensor adapter needs the existing GameManager");enabled=false;return;}
            Manager.OnStateChanged+=HandleState;HandleState(Manager.State);
            Model.AllowSimulated=allowSimulated;Receiver.serverUrl=serverUrl;Receiver.roomKey=roomKey;Receiver.Connect();
        }
        private void HandleState(GameState state)
        {
            Manager.SensorsPaused=false;reserved=null;flashSpawned=contestPending=false;
            Model.Reset();nextContestAt=20;nextFlashAt=12;notice="通常もぐらはチャージ不要。ぜんまいだけチャージ25を消費します";
        }
        public bool TryAuthorizeDefeat(DwellerColor color,MoleKind kind)
        {
            if(!IsPlaying||!SensorGamePolicy.CanDefeat(Model,kind,Team(color),Now)){SetNotice("チャージ不足かスマホの送信停止です。腕を回してからもう一度隠してください");return false;}
            if(kind==MoleKind.Charge)SetNotice((color==DwellerColor.Red?"赤":"青")+"：チャージ25消費");return true;
        }
        public static string Team(DwellerColor color)=>color==DwellerColor.Red?"red":"blue";
        public void SendHit(DwellerColor color){_ = Receiver.SendFeedback(Team(color),"hit",(color==DwellerColor.Red?"赤":"青")+" +"+(color==DwellerColor.Red?Manager.redPoint:Manager.bluePoint),1000);}
        public bool IsReserved(HoleSlot hole)=>hole==reserved;
        public void SetNotice(string value){notice=value;}
        public void BeginContest()
        {
            if(!IsPlaying||!Model.BeginMatch(Now)){SetNotice(Model.Result);return;}
            contestPending=true;Manager.SensorsPaused=true;
        }
        public void BeginFlash()
        {
            var hole=Holes.FirstOrDefault(h=>h.IsTracked&&h.IsEmpty);
            if(!IsPlaying||hole==null){SetNotice("認識中の空いている穴が必要です");return;}
            if(!Model.BeginFlash(Now)){SetNotice(Model.FlashMessage);return;}
            reserved=hole;flashColor=UnityEngine.Random.value<.5f?DwellerColor.Red:DwellerColor.Blue;flashSpawned=false;
        }
        public void ShowChargeMole()
        {
            var hole=Holes.FirstOrDefault(h=>h.IsTracked&&h.IsEmpty&&!IsReserved(h));
            if(!IsPlaying||hole==null){SetNotice("認識中の空いている穴が必要です");return;}
            var color=UnityEngine.Random.value<.5f?DwellerColor.Red:DwellerColor.Blue;
            hole.Spawn(Catalog.Prefab(MoleKind.Charge,Team(color)),color,5);
        }
        private void LateUpdate()
        {
            Model.AllowSimulated=allowSimulated;Model.Tick(Now);
            if(IsPlaying)
            {
                Manager.SensorsPaused=Model.MatchBusy;
                if(contestPending&&!Model.MatchBusy)
                {
                    contestPending=false;SensorGamePolicy.RewardWinner(Model);notice=Model.Result;
                }
                double elapsed=Manager.gameDuration-Manager.RemainingTime;
                if(automaticSensorEvents&&!Model.MatchBusy&&Model.Flash==SensorArenaModel.FlashPhase.Idle&&Model.Ready(Model.Red,Now)&&Model.Ready(Model.Blue,Now))
                {
                    if(elapsed>=nextContestAt){BeginContest();nextContestAt=elapsed+20;}
                    else if(elapsed>=nextFlashAt){BeginFlash();nextFlashAt=elapsed+(reserved!=null?UnityEngine.Random.Range(12f,22f):2);}
                }
                if(reserved!=null)
                {
                    if(Model.Flash==SensorArenaModel.FlashPhase.Visible&&!flashSpawned)
                    {
                        flashSpawned=reserved.Spawn(Catalog.Prefab(MoleKind.Flash,Team(flashColor)),flashColor,(float)(Model.FlashDurationMs/1000),true);
                    }
                    if(Model.Flash==SensorArenaModel.FlashPhase.Idle){if(!flashSpawned)notice=Model.FlashMessage;reserved=null;}
                }
            }
            hud?.Refresh(notice);
        }
        private void OnDestroy()
        {
            if(Manager!=null){Manager.OnStateChanged-=HandleState;Manager.SensorsPaused=false;}
            if(Instance==this)Instance=null;
        }
    }
}
