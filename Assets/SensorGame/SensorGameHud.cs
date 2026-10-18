using System.Linq;
using SensorBridge;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WhackAMole.Sensors
{
    public sealed partial class SensorGameHud : MonoBehaviour
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
                var box=Element("Instruction_"+hole.name,transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-190,0),new Vector2(190,106));
                box.pivot=new Vector2(.5f,0);
                var bg=box.gameObject.AddComponent<Image>();UITheme.StylePanel(bg,UITheme.Tint(new Color(.025f,.045f,.065f,.9f)),true,true);bg.raycastTarget=false;
                var text=Text("InstructionText",box,"",23,new Vector2(8,3),new Vector2(372,103),Color.white);text.alignment=TextAlignmentOptions.Center;
                holeLabels.Add(hole,text);box.gameObject.SetActive(false);
                var voiceBar=Element("VoiceProgress",box,new Vector2(0,0),new Vector2(1,0),new Vector2(8,2),new Vector2(-8,7));
                var voiceBack=voiceBar.gameObject.AddComponent<Image>();voiceBack.color=UITheme.Tint(new Color(.18f,.23f,.3f));voiceBack.raycastTarget=false;
                var voiceFill=Element("Fill",voiceBar,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject.AddComponent<Image>();voiceFill.color=new Color(.55f,1,.7f);voiceFill.raycastTarget=false;voiceBars[hole]=voiceFill;
            }
            BuildPlayPanel();
            BuildPreparation();
            BuildEventPanel();
        }

        private static RectTransform Element(string name,Transform parent,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
        {
            var o=new GameObject(name,typeof(RectTransform));o.transform.SetParent(parent,false);var r=o.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=low;r.offsetMax=high;return r;
        }
        private void BuildCameraSettings()
        {
            var panel=Element("CameraSettingsPanel",transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-430,-45),new Vector2(430,85));
            cameraSettingsPanel=panel.gameObject;
            var background=panel.gameObject.AddComponent<Image>();UITheme.StylePanel(background,UITheme.Tint(new Color(.035f,.055f,.075f,.96f)));
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
            var r=Element(name,parent,Vector2.zero,Vector2.zero,low,high);var text=r.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;text.color=UITheme.Tint(colour);text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;return text;
        }
        private Image Bar(Transform parent,string name,float x,float y,Color colour,ChargeDisplay display)
        {
            var r=Element(name,parent,Vector2.zero,Vector2.zero,new Vector2(x,y),new Vector2(x+480,y+16));var bg=r.gameObject.AddComponent<Image>();bg.color=UITheme.Tint(new Color(.16f,.2f,.25f));bg.raycastTarget=false;
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
        private void OnDestroy(){if(game!=null)game.Model.ChargeSpent-=ShowChargeSpent;if(ringSprite!=null)Destroy(ringSprite);if(ringTexture!=null)Destroy(ringTexture);}
        private Button Button(string name,Transform parent,string label,Vector2 low,Vector2 high,UnityEngine.Events.UnityAction action)
        {
            var r=Element(name,parent,Vector2.zero,Vector2.zero,low,high);var image=r.gameObject.AddComponent<Image>();var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;UITheme.StyleButton(button,UITheme.SurfaceLight);button.onClick.AddListener(action);
            var text=Text("Label",r,label,20,new Vector2(10,0),new Vector2(high.x-low.x-10,high.y-low.y),Color.white);text.alignment=TextAlignmentOptions.Center;return button;
        }
        public void Refresh(string message)
        {
            cameraSettingsPanel.SetActive(!game.IsPlaying);
            bool result=game.Manager!=null&&game.Manager.State==GameState.Result;
            ((RectTransform)cameraSettingsPanel.transform).anchoredPosition=new Vector2(0,result?-25:20);
            cameraSettingsPanel.transform.localScale=Vector3.one*(result ? .9f : 1);
            RefreshCameraSettings();
            var m=game.Model;var now=WhackSensorGame.Now;
            red.text=$"RED  チャージ {m.Red.Charge:0} / {m.ChargeLimit:0}";
            blue.text=$"BLUE  チャージ {m.Blue.Charge:0} / {m.ChargeLimit:0}";
            RefreshCharge(redDisplay,redFill,m.Red,now);RefreshCharge(blueDisplay,blueFill,m.Blue,now);
            status.text=$"赤：{ConnectionStatus(m.Red,now)}{(m.Red.Voice.Verified?"":"・声未確認")} ／ 青：{ConnectionStatus(m.Blue,now)}{(m.Blue.Voice.Verified?"":"・声未確認")}";
            legend.text=m.Red.Voice.Verified&&m.Blue.Voice.Verified?"腕を回してチャージ ／ ぜんまい50・王様75消費":"おねむ準備：マイク許可 → 一度静かに → 普通の声";
            bool ready=game.IsPlaying&&!m.MatchBusy;
            contest.interactable=ready&&m.CircleReady(m.Red,now)&&m.CircleReady(m.Blue,now)&&m.Flash==SensorArenaModel.FlashPhase.Idle;
            charge.interactable=ready&&game.Holes.Any(h=>h.IsTracked);
            flash.interactable=ready&&m.Ready(m.Red,now)&&m.Ready(m.Blue,now)&&m.Flash==SensorArenaModel.FlashPhase.Idle&&game.Holes.Any(h=>h.IsTracked);
            playPanel.SetActive(game.IsPlaying);
            preparationPanel.SetActive(!game.IsPlaying);
            diagnosticsPanel.SetActive(diagnosticsVisible&&game.IsPlaying);
            RefreshPreparation(now);
            RefreshEvent(now);
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
                bool visible=game.IsPlaying&&!game.Model.MatchBusy&&camera!=null&&hole.HasDisplayPose&&game.FlashHole!=hole&&(hole.IsAlive||warning);
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
                float maxY=canvasRect.rect.yMax-(game.FlashHole!=null?440:300);
                float baseY=Mathf.Clamp(local.y+24,canvasRect.rect.yMin+185,maxY),y=baseY;
                for(int attempt=0;attempt<5;attempt++)
                {
                    var rect=new Rect(x-195,y,390,112);if(!occupied.Any(other=>other.Overlaps(rect)))break;
                    float offset=(attempt/2+1)*116*(attempt%2==0?1:-1);
                    y=Mathf.Clamp(baseY+offset,canvasRect.rect.yMin+185,maxY);
                }
                occupied.Add(new Rect(x-195,y,390,112));box.anchoredPosition=new Vector2(x,y);
                box.gameObject.SetActive(true);
                var voice=voiceBars[hole];voice.transform.parent.gameObject.SetActive(hole.CurrentKind==MoleKind.Sleepy&&hole.NeedsSensor);
                var player=game.Model.Get(WhackSensorGame.Team(hole.CurrentColor));
                voice.rectTransform.anchorMax=new Vector2(Mathf.Clamp01((float)(player.MicrophoneRms/player.Voice.Threshold)),1);
                text.text=warning?$"スピード予告 {(game.FlashColour==DwellerColor.Red?"R":"B")}\nあと{game.FlashSeconds:0.0}秒・出てから叩こう":hole.Instruction;
                text.color=warning||hole.HasInteractionFeedback?new Color(1,.85f,.3f):hole.CurrentColor==DwellerColor.Red?new Color(1,.55f,.5f):new Color(.5f,.75f,1);
                box.localScale=Vector3.one*(warning?1+.025f*Mathf.Sin(Time.unscaledTime*10):1);
            }
        }
    }
}
