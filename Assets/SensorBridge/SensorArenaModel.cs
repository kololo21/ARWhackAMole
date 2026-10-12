using System;

namespace SensorBridge
{
    // No Unity dependencies: the game project can call the same state machine later.
    public sealed class SensorArenaModel
    {
        public enum MatchPhase { Idle, Countdown, Shaking, Settling, Result }
        public enum FlashPhase { Idle, WaitingForPhones, LeadIn, Visible }
        public sealed class Player
        {
            public string Name;
            public bool Connected, Simulated, CueAck, VibrationSupported, VibrationAccepted;
            public double Charge, Peak, LastPacketMs = -100000, LastGameMs = -100000;
            public int Samples, ShakeCount;
            public double LastCountedMs=-100000;
            public long LastSeq = -1;
            public string WarningId;
            public double LastCircleSeconds;
        }
        public sealed class Signal
        {
            public string Player, Kind, Message, Id;
            public double DurationMs;
        }
        public readonly Player Red = new Player { Name="red" }, Blue = new Player { Name="blue" };
        public event Action<Signal> Feedback;
        public bool AllowSimulated;
        public double ChargePerSecond=20, ChargeLimit=100, ChargeCost=25, WarningLeadMs=500, FlashDurationMs=350;
        public MatchPhase Match {get;private set;} = MatchPhase.Idle;
        public FlashPhase Flash {get;private set;} = FlashPhase.Idle;
        public double MatchStartMs {get;private set;}
        public double MatchEndMs {get;private set;}
        public double FlashStartMs {get;private set;}
        public double FlashEndMs {get;private set;}
        public string Result {get;private set;} = "2台のスマホを接続してください";
        public string FlashMessage {get;private set;} = "瞬間表示は待機中";
        public string ActionMessage {get;private set;} = "";
        private double warningSentMs;
        public Player Get(string name) => name=="red"?Red:name=="blue"?Blue:null;
        private static bool Finite(double v) => !double.IsNaN(v)&&!double.IsInfinity(v);
        public bool Ready(Player p,double now) => p.Connected && now-p.LastGameMs<500 && now>=p.LastGameMs && (!p.Simulated||AllowSimulated);
        public bool MatchBusy => Match==MatchPhase.Countdown||Match==MatchPhase.Shaking||Match==MatchPhase.Settling;
        public void Presence(string name,bool connected)
        {
            var p=Get(name);if(p==null)return;p.Connected=connected;
            if(!connected){p.LastGameMs=-100000;p.LastPacketMs=-100000;p.LastSeq=-1;p.CueAck=false;p.LastCircleSeconds=0;}
        }
        public void Ingest(SensorPacket packet,double now)
        {
            var p=Get(packet.player);if(p==null||!p.Connected||packet.seq<=p.LastSeq)return;
            var g=packet.sensors?.motionGame;
            if(!Finite(packet.receivedAtMs)||now-packet.receivedAtMs>500||packet.receivedAtMs>now+100)return;
            p.LastSeq=packet.seq;p.LastPacketMs=packet.receivedAtMs;p.Simulated=packet.simulated;p.LastCircleSeconds=0;
            if(g==null||!g.valid||!g.linear||!Finite(g.peakAcceleration)||g.peakAcceleration<0||!Finite(g.windowMs)||g.windowMs<0||g.windowMs>250||!Finite(g.circleSeconds)||g.circleSeconds<0||g.circleSeconds>g.windowMs/1000+.001||packet.timestampMs-g.timestampMs<0||packet.timestampMs-g.timestampMs>500)return;
            if(g.shakeVersion!=1||g.shakeCount<0||g.shakeCount>1||!Finite(g.shakeAgeMs)||g.shakeAgeMs<0||g.shakeAgeMs>g.windowMs||!Finite(g.shakeSpanMs)||g.shakeCount>0&&(g.shakeSpanMs<600||g.shakeSpanMs>2000))return;
            p.LastGameMs=packet.receivedAtMs;
            if(packet.simulated&&!AllowSimulated)return;
            if(MatchBusy)
            {
                // Use one relay clock, never compare the two phone clocks. Discard packet
                // windows straddling the start, so countdown motion cannot win the round.
                if(packet.receivedAtMs-g.windowMs>=MatchStartMs&&packet.receivedAtMs<=MatchEndMs){p.Peak=Math.Max(p.Peak,g.peakAcceleration);p.Samples++;}
                var completed=packet.receivedAtMs-g.shakeAgeMs;
                if(g.shakeCount==1&&completed-g.shakeSpanMs>=MatchStartMs&&completed<=MatchEndMs&&completed-p.LastCountedMs>=600){p.ShakeCount++;p.LastCountedMs=completed;}
            }
            else {p.LastCircleSeconds=g.circleSeconds;p.Charge=Math.Min(ChargeLimit,p.Charge+g.circleSeconds*ChargePerSecond);}
        }
        private void Send(Player p,string kind,string message,double duration,string id=null)
        {
            Feedback?.Invoke(new Signal {Player=p.Name,Kind=kind,Message=message,DurationMs=duration,Id=id??Guid.NewGuid().ToString("N")});
        }
        public bool BeginMatch(double now)
        {
            if(MatchBusy||Flash!=FlashPhase.Idle||!Ready(Red,now)||!Ready(Blue,now)){Result="両チームの新しいゲーム用データが必要です。スマホを再読み込みして送信してください";return false;}
            Red.Peak=Blue.Peak=0;Red.Samples=Blue.Samples=0;Red.ShakeCount=Blue.ShakeCount=0;Red.LastCountedMs=Blue.LastCountedMs=-100000;
            MatchStartMs=now+3000;MatchEndMs=MatchStartMs+5000;Match=MatchPhase.Countdown;Result="3秒後に、軽い往復の回数で5秒勝負！";
            Send(Red,"countdown",Result,3000);Send(Blue,"countdown",Result,3000);return true;
        }
        public bool BeginFlash(double now)
        {
            if(MatchBusy||Flash!=FlashPhase.Idle||!Ready(Red,now)||!Ready(Blue,now)){FlashMessage="2台の送信を確認してから開始してください";return false;}
            Red.CueAck=Blue.CueAck=false;Red.WarningId=Guid.NewGuid().ToString("N");Blue.WarningId=Guid.NewGuid().ToString("N");
            warningSentMs=now;Flash=FlashPhase.WaitingForPhones;FlashMessage="スマホの合図受付を確認中";
            Send(Red,"warning","まもなく瞬間出現！",1500,Red.WarningId);Send(Blue,"warning","まもなく瞬間出現！",1500,Blue.WarningId);return true;
        }
        public void Acknowledge(FeedbackAck ack,double now)
        {
            var p=Get(ack.player);if(p==null||Flash!=FlashPhase.WaitingForPhones||ack.id!=p.WarningId||!ack.enabled||!ack.screenVisible||now-warningSentMs>2000)return;
            p.CueAck=true;p.VibrationSupported=ack.vibrationSupported;p.VibrationAccepted=ack.vibrationAccepted;
            if(Red.CueAck&&Blue.CueAck){Flash=FlashPhase.LeadIn;FlashStartMs=now+WarningLeadMs;FlashEndMs=FlashStartMs+FlashDurationMs;FlashMessage="合図受付済み → まもなく出現";}
        }
        public bool Spend(string player,double now)
        {
            var p=Get(player);if(p==null||!Ready(p,now)||MatchBusy||p.Charge<ChargeCost){ActionMessage="チャージ不足、送信停止、またはシェイク勝負中です";return false;}
            p.Charge=Math.Max(0,p.Charge-ChargeCost);ActionMessage=(p==Red?"赤":"青")+"：チャージを"+ChargeCost.ToString("0")+"消費";
            Send(p,"hit",ActionMessage,1000);return true;
        }
        public void Tick(double now)
        {
            if(MatchBusy&&(!Ready(Red,now)||!Ready(Blue,now)))
            {
                Match=MatchPhase.Result;Result="通信または加速度データが途切れたため無効。再試行してください";return;
            }
            if(Match==MatchPhase.Countdown&&now>=MatchStartMs){Match=MatchPhase.Shaking;Result="5秒間、スマホを軽く振って戻してください。強さは加点しません！";Send(Red,"contest",Result,5000);Send(Blue,"contest",Result,5000);}
            if(Match==MatchPhase.Shaking&&now>=MatchEndMs){Match=MatchPhase.Settling;Result="集計中…";}
            if(Match==MatchPhase.Settling&&now>=MatchEndMs+250)
            {
                Match=MatchPhase.Result;
                Result=Red.Samples==0||Blue.Samples==0?"測定不足のため無効":Red.ShakeCount==Blue.ShakeCount?"引き分け":Red.ShakeCount>Blue.ShakeCount?"赤チームの勝ち！":"青チームの勝ち！";
            }
            if(Flash!=FlashPhase.Idle&&(!Ready(Red,now)||!Ready(Blue,now))){Flash=FlashPhase.Idle;FlashMessage="通信が途切れたため瞬間表示を中止";}
            if(Flash==FlashPhase.WaitingForPhones&&now-warningSentMs>=2000){Flash=FlashPhase.Idle;FlashMessage="合図の返答なし。スマホでUnityの合図を許可してください";}
            if(Flash==FlashPhase.LeadIn&&now>=FlashStartMs){Flash=FlashPhase.Visible;FlashMessage="出現！";}
            if(Flash==FlashPhase.Visible&&now>=FlashEndMs){Flash=FlashPhase.Idle;FlashMessage="瞬間もぐらの表示終了";}
        }
        public void Reset()
        {
            Match=MatchPhase.Idle;Flash=FlashPhase.Idle;Red.Charge=Blue.Charge=0;Red.Peak=Blue.Peak=0;Red.ShakeCount=Blue.ShakeCount=0;Red.CueAck=Blue.CueAck=false;Result="リセットしました";ActionMessage="";FlashMessage="瞬間表示は待機中";
        }
    }
}
