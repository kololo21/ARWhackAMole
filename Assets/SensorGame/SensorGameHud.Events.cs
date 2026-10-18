using System;
using SensorBridge;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WhackAMole.Sensors
{
    public sealed partial class SensorGameHud
    {
        private GameObject playPanel,preparationPanel,diagnosticsPanel,eventPanel,eventShade;
        private TMP_Text eventTitle,eventValue,eventHelp,prepareRed,prepareBlue,diagnosticToggle,diagnosticKind,diagnosticTeam;
        private Image contestRedBar,contestBlueBar,flashRing,flashFill;
        private RectTransform flashTarget;
        private TMP_Text flashArrow;
        private Texture2D ringTexture;
        private Sprite ringSprite;
        private RectTransform retryRect;
        private Vector2 retryPosition;
        private Vector3 retryScale;
        private bool diagnosticsVisible;
        private readonly System.Collections.Generic.Dictionary<HoleSlot,Image> voiceBars=new System.Collections.Generic.Dictionary<HoleSlot,Image>();
        private MoleKind selectedKind=MoleKind.Helmet;
        private DwellerColor selectedTeam=DwellerColor.Red;
        private string ConnectionStatus(SensorArenaModel.Player p,double now)
            =>game.Model.Ready(p,now)?"送信OK":p.Connected?"センサー入力待ち":"スマホ未接続";
        private RectTransform Panel(string name,Vector2 min,Vector2 max,Vector2 low,Vector2 high,Color color)
        {
            var rect=Element(name,transform,min,max,low,high);
            var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return rect;
        }
        private void BuildPlayPanel()
        {
            var panel=Panel("SensorPanel",new Vector2(0,0),new Vector2(1,0),new Vector2(22,20),new Vector2(-22,175),new Color(.035f,.055f,.075f,.94f));playPanel=panel.gameObject;
            red=Text("RedCharge",panel,"RED",25,new Vector2(20,110),new Vector2(680,150),new Color(1,.4f,.4f));
            blue=Text("BlueCharge",panel,"BLUE",25,new Vector2(790,110),new Vector2(1470,150),new Color(.4f,.65f,1));
            redFill=Bar(panel,"RedChargeBar",20,92,new Color(.95f,.25f,.3f),redDisplay);
            blueFill=Bar(panel,"BlueChargeBar",790,92,new Color(.2f,.5f,1),blueDisplay);
            redDisplay.Action=Text("RedChargeAction",panel,"",23,new Vector2(20,50),new Vector2(750,88),Color.white);
            blueDisplay.Action=Text("BlueChargeAction",panel,"",23,new Vector2(790,50),new Vector2(1510,88),Color.white);
            status=Text("SensorStatus",panel,"",18,new Vector2(20,6),new Vector2(770,42),new Color(.78f,.85f,.9f));
            legend=Text("MoleLegend",panel,"",18,new Vector2(790,6),new Vector2(1520,42),new Color(.78f,.85f,.9f));
            foreach(var r in new[]{blue.rectTransform,(RectTransform)blueFill.transform.parent,blueDisplay.Action.rectTransform,legend.rectTransform})RightColumn(r);
            var diag=Panel("DiagnosticsPanel",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-750,185),new Vector2(750,342),new Color(.035f,.055f,.075f,.98f));diagnosticsPanel=diag.gameObject;
            Text("GameplayVersion",diag,WhackSensorGame.GameplayVersion,18,new Vector2(12,120),new Vector2(1480,155),Color.white);
            contest=Button("CountContest",diag,"5秒対決",new Vector2(12,68),new Vector2(235,115),game.BeginContest);
            charge=Button("ChargeMole",diag,"ぜんまいを出す",new Vector2(250,68),new Vector2(505,115),game.ShowChargeMole);
            flash=Button("FlashMole",diag,"スピードを出す",new Vector2(520,68),new Vector2(775,115),game.BeginFlash);
            diagnosticKind=Button("DiagnosticKind",diag,"種類：ヘルメット",new Vector2(12,12),new Vector2(330,59),()=>
            {selectedKind=(MoleKind)(((int)selectedKind+1)%7);diagnosticKind.text="種類："+SensorGamePolicy.Name(selectedKind);}).GetComponentInChildren<TMP_Text>();
            diagnosticTeam=Button("DiagnosticTeam",diag,"色：赤",new Vector2(345,12),new Vector2(510,59),()=>
            {selectedTeam=selectedTeam==DwellerColor.Red?DwellerColor.Blue:DwellerColor.Red;diagnosticTeam.text=selectedTeam==DwellerColor.Red?"色：赤":"色：青";}).GetComponentInChildren<TMP_Text>();
            Button("DiagnosticSpawn",diag,"指定した敵を出す",new Vector2(525,12),new Vector2(800,59),()=>
            {foreach(var hole in game.Holes)if(hole.IsTracked&&hole.IsEmpty){hole.Spawn(game.Catalog.Prefab(selectedKind,WhackSensorGame.Team(selectedTeam)),selectedTeam,15,selectedKind==MoleKind.Flash);break;}});
        }
        private void BuildPreparation()
        {
            var retry=WhackSensorGame.SceneObject("RetryButton");
            if(retry!=null){retryRect=retry.GetComponent<RectTransform>();retryPosition=retryRect.anchoredPosition;retryScale=retryRect.localScale;}
            var panel=Panel("SensorPreparation",new Vector2(0,0),new Vector2(1,0),new Vector2(22,20),new Vector2(-22,340),new Color(.035f,.055f,.075f,.96f));preparationPanel=panel.gameObject;
            prepareRed=Text("PrepareRed",panel,"",21,new Vector2(20,100),new Vector2(755,265),new Color(1,.55f,.5f));
            prepareBlue=Text("PrepareBlue",panel,"",21,new Vector2(790,100),new Vector2(1520,265),new Color(.5f,.75f,1));
            RightColumn(prepareBlue.rectTransform);
            foreach(string team in new[]{"red","blue"})
            {
                float x=team=="red"?20:790;
                Button("CalibrateTilt_"+team,panel,"傾きを合わせる",new Vector2(x,53),new Vector2(x+225,96),()=>game.CalibrateTilt(team));
                Button("CalibrateVoice_"+team,panel,"周囲音を測り直す",new Vector2(x+235,53),new Vector2(x+490,96),()=>game.Model.Get(team).Voice.Recalibrate());
                Button("VoiceMore_"+team,panel,"声を拾いやすく",new Vector2(x,5),new Vector2(x+225,48),()=>game.Model.Get(team).Voice.AdjustSensitivity(.8));
                Button("VoiceLess_"+team,panel,"雑音を拾いにくく",new Vector2(x+235,5),new Vector2(x+490,48),()=>game.Model.Get(team).Voice.AdjustSensitivity(1.25));
                if(team=="blue")foreach(string prefix in new[]{"CalibrateTilt_","CalibrateVoice_","VoiceMore_","VoiceLess_"})RightColumn((RectTransform)panel.Find(prefix+team));
            }
            diagnosticToggle=Button("ToggleDiagnostics",panel,"診断：OFF",new Vector2(1320,8),new Vector2(1520,48),()=>
            {diagnosticsVisible=!diagnosticsVisible;diagnosticToggle.text="診断："+(diagnosticsVisible?"ON":"OFF");}).GetComponentInChildren<TMP_Text>();
            var toggle=(RectTransform)diagnosticToggle.transform.parent;toggle.anchorMin=toggle.anchorMax=new Vector2(1,0);toggle.anchoredPosition=new Vector2(-120,28);
            Text("PrepareVersion",panel,WhackSensorGame.GameplayVersion,16,new Vector2(20,275),new Vector2(1500,315),new Color(.65f,.72f,.8f));
        }
        private static void RightColumn(RectTransform rect)
        {rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.anchoredPosition-=new Vector2(770,0);}
        private string Preparation(SensorArenaModel.Player p,double now)
        {
            var m=game.Model;string team=p==m.Red?"RED":"BLUE";
            string mic=!m.MicrophoneReady(p,now)?"スマホでマイクを許可":!p.Voice.Calibrated?$"2秒ほど静かに：周囲音を測定中 {p.Voice.Progress:P0}":!p.Voice.Verified?(p.Voice.Baseline>.03?"周囲音が大きめ：マイクを近づけ、静かに→『はい』":"一度静かにして、普通の声で『はい』"):"声の準備OK。Unity画面を見て遊ぼう";
            return $"{team}：{ConnectionStatus(p,now)} ／ 姿勢：{(m.TiltReady(p,now)?"OK":"待機")}\n{mic}\n声 {p.MicrophoneRms:0.0000} / 目安 {p.Voice.Threshold:0.0000}（周囲 {p.Voice.Baseline:0.0000}）\n腕回し対決：{(m.CircleReady(p,now)?"入力OK":"回転センサー待ち／スマホの許可を確認")}\n{game.CalibrationStatus(p.Name)}"+(!p.Voice.Verified&&p.Voice.Calibrated?"\n反応しない時は下の感度調整／周囲音の再測定":"");
        }
        private void RefreshPreparation(double now)
        {
            bool result=game.Manager!=null&&game.Manager.State==GameState.Result;
            if(retryRect!=null){retryRect.anchoredPosition=retryPosition+(result?new Vector2(0,60):Vector2.zero);retryRect.localScale=retryScale*(result ? .75f : 1);}
            if(!game.IsPlaying){prepareRed.text=Preparation(game.Model.Red,now);prepareBlue.text=Preparation(game.Model.Blue,now);}
        }
        private void BuildEventPanel()
        {
            eventShade=Panel("EventShade",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(0,.01f,.025f,.67f)).gameObject;
            var panel=Panel("EventPanel",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-650,-200),new Vector2(650,230),new Color(.03f,.07f,.11f,.97f));eventPanel=panel.gameObject;
            eventTitle=Text("EventTitle",panel,"",44,new Vector2(30,340),new Vector2(1270,420),Color.white);eventTitle.alignment=TextAlignmentOptions.Center;
            eventValue=Text("EventValue",panel,"",80,new Vector2(30,160),new Vector2(1270,345),new Color(1,.84f,.32f));eventValue.alignment=TextAlignmentOptions.Center;
            eventHelp=Text("EventHelp",panel,"",30,new Vector2(30,20),new Vector2(1270,155),Color.white);eventHelp.alignment=TextAlignmentOptions.Center;
            contestRedBar=ContestBar(panel,"ContestRed",new Vector2(80,168),new Vector2(620,180),new Color(.95f,.3f,.3f));
            contestBlueBar=ContestBar(panel,"ContestBlue",new Vector2(680,168),new Vector2(1220,180),new Color(.3f,.6f,1));
            flashTarget=Element("FlashTarget",transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-100,-50),new Vector2(100,150));
            ringTexture=new Texture2D(96,96,TextureFormat.RGBA32,false);
            for(int y=0;y<96;y++)for(int x=0;x<96;x++){float d=Vector2.Distance(new Vector2(x,y),new Vector2(47.5f,47.5f));ringTexture.SetPixel(x,y,new Color(1,1,1,d>39&&d<46?1:0));}
            ringTexture.Apply();ringSprite=Sprite.Create(ringTexture,new Rect(0,0,96,96),new Vector2(.5f,.5f));
            flashRing=flashTarget.gameObject.AddComponent<Image>();flashRing.sprite=ringSprite;flashRing.color=new Color(1,.7f,.1f,.9f);flashRing.raycastTarget=false;
            flashArrow=Text("FlashArrow",flashTarget,"▼",46,new Vector2(0,120),new Vector2(200,200),Color.white);flashArrow.alignment=TextAlignmentOptions.Center;
            flashFill=ContestBar(flashTarget,"FlashTime",new Vector2(0,0),new Vector2(200,12),new Color(1,.8f,.2f));
        }
        private Image ContestBar(Transform parent,string name,Vector2 low,Vector2 high,Color color)
        {
            var back=Element(name,parent,Vector2.zero,Vector2.zero,low,high);back.gameObject.AddComponent<Image>().color=new Color(.18f,.23f,.3f);
            var fill=Element("Fill",back,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject.AddComponent<Image>();fill.color=color;fill.raycastTarget=false;return fill;
        }
        private void RefreshEvent(double now)
        {
            var m=game.Model;bool match=game.IsPlaying&&m.MatchBusy;
            bool speed=game.IsPlaying&&game.FlashHole!=null;
            bool outcome=game.IsPlaying&&now<game.EventMessageUntil;
            bool resume=game.IsPlaying&&now<m.ResumeUntilMs;
            eventPanel.SetActive(match||speed||outcome||resume);eventShade.SetActive(match);
            contestRedBar.transform.parent.gameObject.SetActive(match&&m.Match==SensorArenaModel.MatchPhase.Circling);
            contestBlueBar.transform.parent.gameObject.SetActive(match&&m.Match==SensorArenaModel.MatchPhase.Circling);
            flashTarget.gameObject.SetActive(speed&&!match);
            var rect=(RectTransform)eventPanel.transform;
            rect.anchoredPosition=match?new Vector2(0,15):new Vector2(0,canvasRect.rect.height*.5f-225);
            rect.sizeDelta=match?new Vector2(1300,430):new Vector2(1300,170);
            eventTitle.rectTransform.anchoredPosition=match?new Vector2(650,380):new Vector2(650,130);
            eventValue.gameObject.SetActive(match);eventHelp.rectTransform.anchoredPosition=match?new Vector2(650,87.5f):new Vector2(650,48);
            eventHelp.rectTransform.sizeDelta=match?new Vector2(1240,135):new Vector2(1240,76);
            if(match)
            {
                eventTitle.text="5秒・ぐるぐる対決";
                switch(m.Match)
                {
                    case SensorArenaModel.MatchPhase.Briefing:
                        eventValue.text="まもなく対決！";eventHelp.text="穴を叩く手を休めて、チャージと同じ腕回し！\n回転ポイントで勝負・勝者はチャージ+25";break;
                    case SensorArenaModel.MatchPhase.Countdown:
                        eventValue.text=Math.Max(1,Math.Ceiling((m.MatchStartMs-now)/1000)).ToString("0");eventHelp.text="腕を回し始めて準備！\n合図から5秒間を測定します";break;
                    case SensorArenaModel.MatchPhase.Circling:
                        eventValue.text=$"あと {Math.Max(0,(m.MatchEndMs-now)/1000):0.0} 秒";eventHelp.text=$"RED  {m.Red.CirclePoints} pt     ／     BLUE  {m.Blue.CirclePoints} pt\nチャージと同じ腕回し！\n赤：{(m.Red.LastCircleSeconds>0?"回転中！":"腕を回そう")} ／ 青：{(m.Blue.LastCircleSeconds>0?"回転中！":"腕を回そう")}";
                        float max=Math.Max(100,Math.Max(m.Red.CirclePoints,m.Blue.CirclePoints));
                        contestRedBar.rectTransform.anchorMax=new Vector2(m.Red.CirclePoints/max,1);contestBlueBar.rectTransform.anchorMax=new Vector2(m.Blue.CirclePoints/max,1);break;
                    case SensorArenaModel.MatchPhase.Settling:eventValue.text="集計中…";eventHelp.text="動きを止めて、結果を待とう";break;
                    case SensorArenaModel.MatchPhase.Result:
                        var result=m.LastContest;eventValue.text=m.Result;
                        string reward=result==null||!result.Valid||result.Winner==null?"報酬なし":result.Gained==0?"チャージ満タンのため増加なし":$"{(result.Winner=="red"?"赤":"青")}：チャージ +{result.Gained:0}  {result.Before:0} → {result.After:0}"+(result.Gained<25?"（上限100）":"");
                        eventHelp.text=$"RED  {result?.RedPoints??0} pt     ／     BLUE  {result?.BluePoints??0} pt\n{reward}\nまもなくモグラたたきを再開";break;
                }
            }
            else if(speed)
            {
                bool warning=game.FlashWarning;
                eventTitle.text=warning?$"{(game.FlashColour==DwellerColor.Red?"赤":"青")}のスピードが来る！":"今！ 穴を隠して！";
                eventHelp.text=warning?$"矢印の穴に注目！ あと {game.FlashSeconds:0.0} 秒・出てから叩こう":"成功で +30点";
                var camera=Camera.main;
                if(camera!=null)
                {
                    var screen=CameraDisplaySettings.ToDisplayPoint(camera.WorldToScreenPoint(game.FlashHole.transform.position),camera);
                    Vector2 local;RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,screen,canvasComponent.renderMode==RenderMode.ScreenSpaceOverlay?null:canvasComponent.worldCamera,out local);
                    flashTarget.anchoredPosition=local+new Vector2(0,30);
                    flashTarget.gameObject.SetActive(screen.z>0);
                }
                flashArrow.color=game.FlashColour==DwellerColor.Red?new Color(1,.4f,.3f):new Color(.4f,.7f,1);
                flashFill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01((float)((warning?m.FlashStartMs-now:m.FlashEndMs-now)/(warning?m.WarningLeadMs:m.FlashDurationMs))),1);
            }
            else if(outcome){eventTitle.text=game.EventMessage;eventHelp.text="盤面を見て次のモグラを狙おう";}
            else if(resume){eventTitle.text="モグラたたき再開！";eventHelp.text="右手で穴を叩こう・腕回しでチャージ";}
        }
    }
}
