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
        public float balanceTiltDegrees=30,balanceHoldSeconds=.6f;
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
        private bool flashSpawned, flashPreCovered, contestPending;
        private double nextContestAt=60,nextFlashAt=18;
        private AudioSource cueAudio;
        private AudioClip cueClip;
        public HoleSlot FlashHole=>reserved;
        public bool FlashWarning=>reserved!=null&&Model.Flash==SensorArenaModel.FlashPhase.LeadIn;
        public double FlashSeconds=>Math.Max(0,(Model.FlashStartMs-Now)/1000);
        public DwellerColor FlashColour=>flashColor;
        private string notice="";
        public const string GameplayVersion="画面集中版 2026.10.18 / 腕回し対決対応";
        public string EventMessage {get;private set;}="";
        public double EventMessageUntil {get;private set;}
        private readonly System.Collections.Generic.Dictionary<string,string> calibrationIds=new System.Collections.Generic.Dictionary<string,string>();
        private readonly System.Collections.Generic.Dictionary<string,string> calibrationMessages=new System.Collections.Generic.Dictionary<string,string>();
        private readonly System.Collections.Generic.Dictionary<string,double> calibrationDeadlines=new System.Collections.Generic.Dictionary<string,double>();
        private SensorArenaModel.MatchPhase lastAudioPhase;
        private int lastCountdown=-1;
        public string CalibrationStatus(string team)
        {
            if(calibrationIds.ContainsKey(team)&&Now>calibrationDeadlines[team]){calibrationIds.Remove(team);calibrationMessages[team]="傾き調整：返答なし。スマホの送信を確認";}
            return calibrationMessages.TryGetValue(team,out var message)?message:"";
        }
        public async void CalibrateTilt(string team)
        {
            if(IsPlaying)return;
            string id=Guid.NewGuid().ToString("N");calibrationIds[team]=id;calibrationDeadlines[team]=Now+2500;calibrationMessages[team]="傾き調整：スマホの返答待ち…";
            if(!await Receiver.SendFeedback(team,"calibrate","傾きの基準を合わせる",0,id))
            {if(calibrationIds.TryGetValue(team,out var pending)&&pending==id){calibrationIds.Remove(team);calibrationMessages[team]="傾き調整：送信できませんでした";}}
        }
        private void Acknowledge(FeedbackAck ack)
        {
            Model.Acknowledge(ack,Now);
            if(calibrationIds.TryGetValue(ack.player,out var id)&&id==ack.id&&Now<=calibrationDeadlines[ack.player])
            {calibrationIds.Remove(ack.player);calibrationMessages[ack.player]=ack.applied?"傾き調整：完了":"傾き調整：姿勢を取得できません";}
        }
        public void ShowEventResult(string message)
        {EventMessage=message;EventMessageUntil=Now+2200;SetNotice(message);}
#if UNITY_EDITOR
        public void ClearEventResultForVerification(){EventMessageUntil=0;}
#endif
        public void NotifyFlashExpired(HoleSlot hole)
        {
            if(hole!=reserved)return;
            Model.CancelFlash("スピードは時間切れ");reserved=null;flashPreCovered=false;
            ShowEventResult("スピードは時間切れ！ 次のチャンスを狙おう");PlayCue(.65f);
        }
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
            if(GetComponent<CameraDisplaySettings>()==null)gameObject.AddComponent<CameraDisplaySettings>();
            Catalog=Resources.Load<WhackMoleCatalog>("WhackMoleCatalog");
            Holes=FindObjectsByType<HoleSlot>(FindObjectsInactive.Include,FindObjectsSortMode.None).OrderBy(o=>o.name).ToArray();
            Receiver=GetComponent<SensorBridgeReceiver>()??gameObject.AddComponent<SensorBridgeReceiver>();
            Receiver.SampleReceived+=OnSample;Receiver.PresenceChanged+=Model.Presence;
            Receiver.FeedbackAcknowledged+=Acknowledge;
            Model.Feedback+=s=>{_ = Receiver.SendFeedback(s.Player,s.Kind,s.Message,s.DurationMs,s.Id);};
            var background=SceneObject("HudPanel")?.GetComponent<Image>();if(background)background.raycastTarget=false;
            hud=SensorGameHud.Create(this,SceneObject("Canvas"));
            cueAudio=gameObject.AddComponent<AudioSource>();cueAudio.playOnAwake=false;cueAudio.volume=.3f;
            var samples=new float[8820];for(int i=0;i<samples.Length;i++)samples[i]=(float)(Math.Sin(i*2*Math.PI*880/44100)*.35*(1-i/(double)samples.Length));
            cueClip=AudioClip.Create("SpeedyWarning",samples.Length,1,44100,false);cueClip.SetData(samples,0);
        }
        private void OnSample(SensorPacket packet)
        {
            bool accepted=Model.Ingest(packet,Now);
            // Outside a round, keep connection/freshness but do not accumulate playable charge.
            if(!IsPlaying)Model.Red.Charge=Model.Blue.Charge=0;
            else if(accepted&&!Model.MatchBusy)
            {
                var color=packet.player=="red"?DwellerColor.Red:DwellerColor.Blue;
                var player=Model.Get(packet.player);var target=SensorTarget(color);
                if(target!=null&&target.CurrentKind==MoleKind.Sleepy&&player.LastVoiceSeq==packet.seq&&player.LastVoiceEventMs>=target.SpawnTimeMs&&player.LastQuietSeq>target.SpawnSensorSeq&&player.LastQuietSeq<packet.seq&&Model.MicrophoneReady(player,Now))target.Weaken();
            }
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
            Manager.SensorsPaused=false;reserved=null;flashSpawned=flashPreCovered=contestPending=false;
            Model.Reset();hud?.ResetChargeFeedback();nextContestAt=60;nextFlashAt=18;EventMessageUntil=0;lastCountdown=-1;lastAudioPhase=SensorArenaModel.MatchPhase.Idle;notice=$"通常は手で叩く。ぜんまいは{Model.ChargeCost:0}、王様は{Model.KingChargeCost:0}チャージ";
        }
        public bool TryAuthorizeDefeat(DwellerColor color,MoleKind kind)
        {
            if(!IsPlaying)return false;
            if(!SensorGamePolicy.CanDefeat(Model,kind,Team(color),Now))
            {
                var player=Model.Get(Team(color));string name=color==DwellerColor.Red?"赤":"青";
                double cost=SensorGamePolicy.Cost(Model,kind);
                SetNotice(Model.MatchBusy?"対決中は撃破できません":!Model.Ready(player,Now)?name+"：スマホの送信を確認してください":$"{name}：{SensorGamePolicy.Name(kind)}には{cost:0}必要。あと{Math.Ceiling(cost-player.Charge):0}チャージして、もう一度隠してください");
                return false;
            }
            if(SensorGamePolicy.Cost(Model,kind)>0)SetNotice((color==DwellerColor.Red?"赤":"青")+$"：チャージ{SensorGamePolicy.Cost(Model,kind):0}消費！");return true;
        }
        public static string Team(DwellerColor color)=>color==DwellerColor.Red?"red":"blue";
        public void SendHit(DwellerColor color,MoleKind kind=MoleKind.Normal){if(kind==MoleKind.Flash){Model.CancelFlash("スピード成功");reserved=null;flashPreCovered=false;ShowEventResult("スピード成功！ +30点");PlayCue(1.5f);}_ = Receiver.SendFeedback(Team(color),"hit",(color==DwellerColor.Red?"赤":"青")+" +"+((color==DwellerColor.Red?Manager.redPoint:Manager.bluePoint)*SensorGamePolicy.ScoreMultiplier(kind)),1000);}
        public HoleSlot SensorTarget(DwellerColor colour)=>Holes.Where(h=>h.NeedsSensor&&h.CurrentColor==colour).OrderBy(h=>h.SpawnOrder).FirstOrDefault();
        public bool CanSpawn(MoleKind kind,DwellerColor colour)
        {
            var player=Model.Get(Team(colour));
            return kind==MoleKind.Balance?Model.TiltReady(player,Now):kind==MoleKind.Sleepy?Model.MicrophoneReady(player,Now)&&player.Voice.Verified:kind==MoleKind.Charge||kind==MoleKind.King?Model.Ready(player,Now):kind!=MoleKind.Flash;
        }
        private bool AtCapacity()=>FindObjectsByType<DwellerSpawner>(FindObjectsSortMode.None).Any(s=>s.IsAtCapacity);
        public bool IsReserved(HoleSlot hole)=>hole==reserved;
        internal void NotifyTrackingLost(HoleSlot hole)
        {
            // Keep the loss even if tracking returns before the next frame.
            if(hole==reserved&&!flashSpawned)flashPreCovered=true;
        }
        public void SetNotice(string value){notice=value;}
        public void BeginContest()
        {
            if(!IsPlaying||Now<EventMessageUntil)return;
            if(!Model.BeginMatch(Now)){ShowEventResult(Model.Result);return;}
            contestPending=true;Manager.SensorsPaused=true;
        }
        public void BeginFlash()
        {
            var hole=Holes.FirstOrDefault(h=>h.IsTracked&&h.IsEmpty);
            if(!IsPlaying||Now<EventMessageUntil||hole==null||AtCapacity()){SetNotice("認識中の空いている穴と出現枠が必要です");return;}
            if(!Model.BeginFlash(Now)){SetNotice(Model.FlashMessage);return;}
            reserved=hole;flashColor=UnityEngine.Random.value<.5f?DwellerColor.Red:DwellerColor.Blue;flashSpawned=flashPreCovered=false;
            cueAudio.PlayOneShot(cueClip);SetNotice("穴の予告を見て、出てから叩こう！");
        }
        public void ShowChargeMole()
        {
            var hole=Holes.FirstOrDefault(h=>h.IsTracked&&h.IsEmpty&&!IsReserved(h));
            if(!IsPlaying||hole==null||AtCapacity()){SetNotice("認識中の空いている穴と出現枠が必要です");return;}
            var color=UnityEngine.Random.value<.5f?DwellerColor.Red:DwellerColor.Blue;
            hole.Spawn(Catalog.Prefab(MoleKind.Charge,Team(color)),color,5);
        }
        private void LateUpdate()=>AdvanceFrame(Now);
        internal void AdvanceFrame(double now)
        {
            // Check before Tick can move LeadIn to Visible. A pre-spawn loss also
            // invalidates a reservation whose model has already reached Visible.
            if(IsPlaying&&reserved!=null&&!flashSpawned&&(flashPreCovered||!reserved.IsTracked))
            {
                Model.CancelFlash("予告中に穴が隠れたため中止。出てから叩こう");
                ShowEventResult("先隠しで中止：モグラが出てから叩こう！");PlayCue(.65f);
                notice=Model.FlashMessage;reserved=null;flashPreCovered=false;
            }
            Model.AllowSimulated=allowSimulated;Model.Tick(now);
            UpdateEventAudio(now);
            if(IsPlaying)
            {
                Manager.SensorsPaused=Model.MatchBusy;
                if(contestPending&&!Model.MatchBusy)
                {
                    contestPending=false;SensorGamePolicy.RewardWinner(Model);notice=Model.Result;
                    foreach(var hole in Holes)hole.ResetSensorProgress();
                }
                double elapsed=Manager.gameDuration-Manager.RemainingTime;
                if(automaticSensorEvents&&!Model.MatchBusy&&now>=EventMessageUntil&&now>=Model.ResumeUntilMs&&Model.Flash==SensorArenaModel.FlashPhase.Idle&&Model.Ready(Model.Red,now)&&Model.Ready(Model.Blue,now))
                {
                    if(elapsed>=nextContestAt&&Model.CircleReady(Model.Red,now)&&Model.CircleReady(Model.Blue,now)){BeginContest();nextContestAt=elapsed+60;}
                    else if(elapsed>=nextFlashAt){BeginFlash();nextFlashAt=elapsed+(reserved!=null?UnityEngine.Random.Range(12f,22f):2);}
                }
                if(reserved!=null)
                {
                    if(Model.Flash==SensorArenaModel.FlashPhase.Visible&&!flashSpawned)
                    {
                        flashSpawned=reserved.Spawn(Catalog.Prefab(MoleKind.Flash,Team(flashColor)),flashColor,(float)(Model.FlashDurationMs/1000),true);
                        if(flashSpawned)PlayCue(1.8f);
                    }
                    if(Model.Flash==SensorArenaModel.FlashPhase.Idle){if(flashSpawned&&reserved.IsAlive)reserved.ExpireNow();else if(!flashSpawned)ShowEventResult(Model.FlashMessage);reserved=null;flashPreCovered=false;}
                }
            }
            hud?.Refresh(notice);
        }
        private void PlayCue(float pitch){if(cueAudio==null)return;cueAudio.pitch=pitch;cueAudio.PlayOneShot(cueClip);}
        private void UpdateEventAudio(double now)
        {
            if(Model.Match!=lastAudioPhase){if(Model.Match!=SensorArenaModel.MatchPhase.Idle)PlayCue(Model.Match==SensorArenaModel.MatchPhase.Result?1.5f:1);lastAudioPhase=Model.Match;}
            if(Model.Match==SensorArenaModel.MatchPhase.Countdown)
            {int count=(int)Math.Ceiling((Model.MatchStartMs-now)/1000);if(count!=lastCountdown){lastCountdown=count;PlayCue(1.15f);}}
        }
        private void OnDestroy()
        {
            if(Manager!=null){Manager.OnStateChanged-=HandleState;Manager.SensorsPaused=false;}
            if(Instance==this)Instance=null;
            if(cueClip!=null)Destroy(cueClip);
        }
    }
}
