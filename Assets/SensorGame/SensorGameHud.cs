using System.Linq;
using SensorBridge;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WhackAMole.Sensors
{
    public sealed class SensorGameHud : MonoBehaviour
    {
        private WhackSensorGame game;
        private TMP_Text red,blue,status,legend;
        private Image redFill,blueFill;
        private sealed class ChargeDisplay
        {
            public TMP_Text Action;
            public Image Trail,Marker;
            public Color Colour;
            public double Before,After;
            public float SpentAt=-100;
        }
        private readonly ChargeDisplay redDisplay=new ChargeDisplay(),blueDisplay=new ChargeDisplay();
        private Button contest,flash,charge;
        private TMP_FontAsset font;
        private readonly System.Collections.Generic.Dictionary<HoleSlot,TMP_Text> holeLabels=new System.Collections.Generic.Dictionary<HoleSlot,TMP_Text>();
        private RectTransform canvasRect;
        private Canvas canvasComponent;
        private GameObject cameraSettingsPanel;
        private TMP_Text horizontalSetting,verticalSetting;
        public TMP_FontAsset JapaneseFont=>font;
        public static SensorGameHud Create(WhackSensorGame game,GameObject canvas)
        {
            if(!canvas){canvas=new GameObject("Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;}
            var root=new GameObject("SensorGameHUD",typeof(RectTransform));root.transform.SetParent(canvas.transform,false);
            var rect=root.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var hud=root.AddComponent<SensorGameHud>();hud.game=game;hud.Build();game.Model.ChargeSpent+=hud.ShowChargeSpent;return hud;
        }
        private void Build()
        {
            var source=Resources.Load<Font>("WhackSensorJP");
            if(source){font=TMP_FontAsset.CreateFontAsset(source);font.isMultiAtlasTexturesEnabled=true;}
            else font=TMP_Settings.defaultFontAsset;
            canvasComponent=GetComponentInParent<Canvas>();canvasRect=canvasComponent.GetComponent<RectTransform>();
            BuildCameraSettings();
            foreach(var hole in game.Holes)
            {
                var box=Element("Instruction_"+hole.name,transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-190,0),new Vector2(190,78));
                box.pivot=new Vector2(.5f,0);
                var bg=box.gameObject.AddComponent<Image>();bg.color=new Color(.025f,.045f,.065f,.9f);bg.raycastTarget=false;
                var text=Text("InstructionText",box,"",23,new Vector2(8,3),new Vector2(372,75),Color.white);text.alignment=TextAlignmentOptions.Center;
                holeLabels.Add(hole,text);box.gameObject.SetActive(false);
            }
            var panel=Element("SensorPanel",transform,new Vector2(0,0),new Vector2(1,0),new Vector2(22,20),new Vector2(-22,275));
            var background=panel.gameObject.AddComponent<Image>();background.color=new Color(.035f,.055f,.075f,.92f);background.raycastTarget=false;
            red=Text("RedCharge",panel,"RED",24,new Vector2(20,198),new Vector2(550,234),new Color(1,.4f,.4f));
            blue=Text("BlueCharge",panel,"BLUE",24,new Vector2(570,198),new Vector2(1100,234),new Color(.4f,.65f,1));
            redFill=Bar(panel,"RedChargeBar",20,178,new Color(.95f,.25f,.3f),redDisplay);blueFill=Bar(panel,"BlueChargeBar",570,178,new Color(.2f,.5f,1),blueDisplay);
            redDisplay.Action=Text("RedChargeAction",panel,"",25,new Vector2(20,132),new Vector2(550,173),Color.white);
            blueDisplay.Action=Text("BlueChargeAction",panel,"",25,new Vector2(570,132),new Vector2(1100,173),Color.white);
            status=Text("SensorStatus",panel,"",18,new Vector2(20,66),new Vector2(1100,121),Color.white);
            legend=Text("MoleLegend",panel,"",18,new Vector2(20,27),new Vector2(1120,59),new Color(.78f,.85f,.9f));
            contest=Button("CountContest",panel,"5秒・往復回数勝負",new Vector2(1160,185),new Vector2(1450,230),game.BeginContest);
            charge=Button("ChargeMole",panel,"チャージもぐらを出す",new Vector2(1160,127),new Vector2(1450,172),game.ShowChargeMole);
            flash=Button("FlashMole",panel,"合図 → 瞬間もぐら",new Vector2(1160,69),new Vector2(1450,114),game.BeginFlash);
        }
        private static RectTransform Element(string name,Transform parent,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
        {
            var o=new GameObject(name,typeof(RectTransform));o.transform.SetParent(parent,false);var r=o.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=low;r.offsetMax=high;return r;
        }
        private void BuildCameraSettings()
        {
            var panel=Element("CameraSettingsPanel",transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-430,-45),new Vector2(430,85));
            cameraSettingsPanel=panel.gameObject;
            var background=panel.gameObject.AddComponent<Image>();background.color=new Color(.035f,.055f,.075f,.96f);
            Text("CameraSettingsTitle",panel,"カメラの表示方向",22,new Vector2(18,99),new Vector2(840,129),Color.white);
            var horizontal=Button("FlipHorizontal",panel,"",new Vector2(18,48),new Vector2(278,94),()=>
            {
                var settings=CameraDisplaySettings.Instance;settings.Configure(!settings.FlipHorizontal,settings.FlipVertical);RefreshCameraSettings();
            });
            var vertical=Button("FlipVertical",panel,"",new Vector2(296,48),new Vector2(556,94),()=>
            {
                var settings=CameraDisplaySettings.Instance;settings.Configure(settings.FlipHorizontal,!settings.FlipVertical);RefreshCameraSettings();
            });
            Button("ResetCameraFlip",panel,"反転なしに戻す",new Vector2(574,48),new Vector2(842,94),()=>
            {CameraDisplaySettings.Instance.Configure(false,false);RefreshCameraSettings();});
            horizontalSetting=horizontal.GetComponentInChildren<TMP_Text>();verticalSetting=vertical.GetComponentInChildren<TMP_Text>();
            Text("CameraSettingsHelp",panel,"映像とモグラを一緒に反転します。両方ONで180度回転。",18,new Vector2(18,8),new Vector2(842,40),new Color(.78f,.85f,.9f));
            RefreshCameraSettings();
        }
        private void RefreshCameraSettings()
        {
            var settings=CameraDisplaySettings.Instance;if(settings==null)return;
            horizontalSetting.text="左右反転："+(settings.FlipHorizontal?"ON":"OFF");
            verticalSetting.text="上下反転："+(settings.FlipVertical?"ON":"OFF");
            horizontalSetting.color=settings.FlipHorizontal?new Color(.5f,1,.7f):Color.white;
            verticalSetting.color=settings.FlipVertical?new Color(.5f,1,.7f):Color.white;
        }
        private TMP_Text Text(string name,Transform parent,string value,int size,Vector2 low,Vector2 high,Color colour)
        {
            var r=Element(name,parent,Vector2.zero,Vector2.zero,low,high);var text=r.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;text.color=colour;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;return text;
        }
        private Image Bar(Transform parent,string name,float x,float y,Color colour,ChargeDisplay display)
        {
            var r=Element(name,parent,Vector2.zero,Vector2.zero,new Vector2(x,y),new Vector2(x+480,y+16));var bg=r.gameObject.AddComponent<Image>();bg.color=new Color(.16f,.2f,.25f);bg.raycastTarget=false;
            display.Colour=colour;
            var trail=Element("SpentTrail",r,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);display.Trail=trail.gameObject.AddComponent<Image>();display.Trail.color=new Color(1,.8f,.3f);display.Trail.raycastTarget=false;display.Trail.enabled=false;
            var f=Element("Fill",r,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);var fill=f.gameObject.AddComponent<Image>();fill.color=colour;fill.raycastTarget=false;
            var marker=Element("CostMarker",r,new Vector2(.5f,0),new Vector2(.5f,1),new Vector2(-1,-3),new Vector2(1,3));display.Marker=marker.gameObject.AddComponent<Image>();display.Marker.color=Color.white;display.Marker.raycastTarget=false;
            return fill;
        }
        private void ShowChargeSpent(SensorArenaModel.Player player,double amount,double before)
        {
            var display=player==game.Model.Red?redDisplay:blueDisplay;
            display.Before=before;display.After=player.Charge;display.SpentAt=Time.unscaledTime;
            display.Action.text=$"-{amount:0} 消費！  {before:0} → {player.Charge:0}";
            display.Action.color=new Color(1,.85f,.3f);
        }
        public void ResetChargeFeedback()
        {
            foreach(var display in new[]{redDisplay,blueDisplay}){display.SpentAt=-100;display.Action.text="";display.Trail.enabled=false;}
        }
        private void RefreshCharge(ChargeDisplay display,Image fill,SensorArenaModel.Player player,double now)
        {
            var model=game.Model;float limit=(float)model.ChargeLimit;
            fill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01((float)player.Charge/limit),1);
            float threshold=Mathf.Clamp01((float)model.ChargeCost/limit);
            display.Marker.rectTransform.anchorMin=new Vector2(threshold,0);display.Marker.rectTransform.anchorMax=new Vector2(threshold,1);
            float age=Time.unscaledTime-display.SpentAt;
            display.Trail.enabled=age<1.1f;
            if(display.Trail.enabled)
            {
                float remaining=Mathf.Lerp((float)display.Before,(float)display.After,Mathf.Clamp01((age-.25f)/.85f));
                display.Trail.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(remaining/limit),1);
            }
            fill.color=Color.Lerp(display.Colour,Color.white,Mathf.Clamp01(1-age/.5f)*.5f);
            if(age<2f)return;
            display.Action.color=new Color(.78f,.85f,.9f);
            display.Action.text=!game.IsPlaying?$"ぜんまい：{model.ChargeCost:0}必要":!model.Ready(player,now)?"ぜんまい：スマホの送信を確認":player.Charge>=model.ChargeCost?$"ぜんまい撃破OK！ {model.ChargeCost:0}消費":$"ぜんまい：あと{System.Math.Ceiling(model.ChargeCost-player.Charge):0}チャージ";
        }
        private void OnDestroy(){if(game!=null)game.Model.ChargeSpent-=ShowChargeSpent;}
        private Button Button(string name,Transform parent,string label,Vector2 low,Vector2 high,UnityEngine.Events.UnityAction action)
        {
            var r=Element(name,parent,Vector2.zero,Vector2.zero,low,high);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.17f,.26f,.31f);var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(action);
            var text=Text("Label",r,label,20,new Vector2(10,0),new Vector2(high.x-low.x-10,high.y-low.y),Color.white);text.alignment=TextAlignmentOptions.Center;return button;
        }
        public void Refresh(string message)
        {
            cameraSettingsPanel.SetActive(!game.IsPlaying);
            ((RectTransform)cameraSettingsPanel.transform).anchoredPosition=new Vector2(0,game.Manager!=null&&game.Manager.State==GameState.Result?-70:20);
            RefreshCameraSettings();
            var m=game.Model;var now=WhackSensorGame.Now;
            red.text=$"RED  チャージ {m.Red.Charge:0} / {m.ChargeLimit:0}    往復 {m.Red.ShakeCount}回";
            blue.text=$"BLUE  チャージ {m.Blue.Charge:0} / {m.ChargeLimit:0}    往復 {m.Blue.ShakeCount}回";
            RefreshCharge(redDisplay,redFill,m.Red,now);RefreshCharge(blueDisplay,blueFill,m.Blue,now);
            legend.text=$"ぜんまい{m.ChargeCost:0}／王様{m.KingChargeCost:0}消費。傾きは基準姿勢を設定、声はスマホのマイクを許可";
            string PlayerStatus(SensorBridge.SensorArenaModel.Player p) => m.Ready(p,now)?(p.Simulated?"合成データ":"受信中"):(p.Connected?"接続済・センサー待ち":"未接続");
            var connection="赤 "+PlayerStatus(m.Red)+" ／ 青 "+PlayerStatus(m.Blue);
            var relay=game.Receiver.Status=="Connected"?"接続済":game.Receiver.Status=="Connecting..."?"接続準備中":game.Receiver.Status.StartsWith("Reconnecting")?"再接続中":game.Receiver.Status;
            status.text="中継 "+relay+" ／ Room "+game.roomKey+" ／ "+connection+"\n"+(m.MatchBusy?m.Result:"腕を回すとチャージ。"+message);
            bool ready=game.IsPlaying&&!m.MatchBusy;
            contest.interactable=ready&&m.Ready(m.Red,now)&&m.Ready(m.Blue,now)&&m.Flash==SensorBridge.SensorArenaModel.FlashPhase.Idle;
            charge.interactable=ready&&game.Holes.Any(h=>h.IsTracked);
            flash.interactable=contest.interactable&&game.Holes.Any(h=>h.IsTracked);
            RefreshHoleLabels();
        }
        private void RefreshHoleLabels()
        {
            var camera=Camera.main;
            var occupied=new System.Collections.Generic.List<Rect>();
            foreach(var entry in holeLabels)
            {
                var hole=entry.Key;var text=entry.Value;var box=(RectTransform)text.transform.parent;
                bool warning=game.FlashWarning&&game.FlashHole==hole;
                bool visible=game.IsPlaying&&camera!=null&&hole.HasDisplayPose&&(hole.IsAlive||warning);
                if(!visible){box.gameObject.SetActive(false);continue;}
                var position=hole.transform.position;
                if(!warning&&hole.Current!=null)
                {
                    var renderer=hole.Current.GetComponentInChildren<Renderer>();
                    if(renderer!=null)position=renderer.bounds.center+Vector3.up*renderer.bounds.extents.y;
                }
                var screen=CameraDisplaySettings.ToDisplayPoint(camera.WorldToScreenPoint(position),camera);
                if(screen.z<=0){box.gameObject.SetActive(false);continue;}
                Vector2 local;RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,screen,canvasComponent.renderMode==RenderMode.ScreenSpaceOverlay?null:canvasComponent.worldCamera,out local);
                float x=Mathf.Clamp(local.x,canvasRect.rect.xMin+195,canvasRect.rect.xMax-195);
                float baseY=Mathf.Clamp(local.y+24,canvasRect.rect.yMin+290,canvasRect.rect.yMax-160),y=baseY;
                for(int attempt=0;attempt<5;attempt++)
                {
                    var rect=new Rect(x-195,y,390,84);if(!occupied.Any(other=>other.Overlaps(rect)))break;
                    float offset=(attempt/2+1)*88*(attempt%2==0?1:-1);
                    y=Mathf.Clamp(baseY+offset,canvasRect.rect.yMin+290,canvasRect.rect.yMax-160);
                }
                occupied.Add(new Rect(x-195,y,390,84));box.anchoredPosition=new Vector2(x,y);
                box.gameObject.SetActive(true);
                text.text=warning?$"スピード予告 {(game.FlashColour==DwellerColor.Red?"R":"B")}\nあと{game.FlashSeconds:0.0}秒・出てから叩こう":hole.Instruction;
                text.color=warning||hole.HasInteractionFeedback?new Color(1,.85f,.3f):hole.CurrentColor==DwellerColor.Red?new Color(1,.55f,.5f):new Color(.5f,.75f,1);
                box.localScale=Vector3.one*(warning?1+.025f*Mathf.Sin(Time.unscaledTime*10):1);
            }
        }
    }
}
