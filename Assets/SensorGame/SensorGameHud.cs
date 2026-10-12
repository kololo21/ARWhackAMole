using System.Linq;
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
        private Button contest,flash,charge;
        private TMP_FontAsset font;
        public TMP_FontAsset JapaneseFont=>font;
        public static SensorGameHud Create(WhackSensorGame game,GameObject canvas)
        {
            if(!canvas){canvas=new GameObject("Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;}
            var root=new GameObject("SensorGameHUD",typeof(RectTransform));root.transform.SetParent(canvas.transform,false);
            var rect=root.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var hud=root.AddComponent<SensorGameHud>();hud.game=game;hud.Build();return hud;
        }
        private void Build()
        {
            var source=Resources.Load<Font>("WhackSensorJP");
            if(source){font=TMP_FontAsset.CreateFontAsset(source);font.isMultiAtlasTexturesEnabled=true;}
            else font=TMP_Settings.defaultFontAsset;
            var panel=Element("SensorPanel",transform,new Vector2(0,0),new Vector2(1,0),new Vector2(22,20),new Vector2(-22,235));
            var background=panel.gameObject.AddComponent<Image>();background.color=new Color(.035f,.055f,.075f,.92f);background.raycastTarget=false;
            red=Text("RedCharge",panel,"RED",24,new Vector2(20,157),new Vector2(500,190),new Color(1,.4f,.4f));
            blue=Text("BlueCharge",panel,"BLUE",24,new Vector2(570,157),new Vector2(1050,190),new Color(.4f,.65f,1));
            redFill=Bar(panel,"RedChargeBar",20,132,new Color(.95f,.25f,.3f));blueFill=Bar(panel,"BlueChargeBar",570,132,new Color(.2f,.5f,1));
            status=Text("SensorStatus",panel,"",20,new Vector2(20,66),new Vector2(1100,121),Color.white);
            legend=Text("MoleLegend",panel,"通常：消費なし ／ ぜんまい：チャージ25消費 ／ 得点はもぐらの色へ",18,new Vector2(20,33),new Vector2(1120,64),new Color(.78f,.85f,.9f));
            contest=Button("CountContest",panel,"5秒・往復回数勝負",new Vector2(1160,145),new Vector2(1450,190),game.BeginContest);
            charge=Button("ChargeMole",panel,"チャージもぐらを出す",new Vector2(1160,87),new Vector2(1450,132),game.ShowChargeMole);
            flash=Button("FlashMole",panel,"合図 → 瞬間もぐら",new Vector2(1160,29),new Vector2(1450,74),game.BeginFlash);
        }
        private static RectTransform Element(string name,Transform parent,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
        {
            var o=new GameObject(name,typeof(RectTransform));o.transform.SetParent(parent,false);var r=o.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=low;r.offsetMax=high;return r;
        }
        private TMP_Text Text(string name,Transform parent,string value,int size,Vector2 low,Vector2 high,Color colour)
        {
            var r=Element(name,parent,Vector2.zero,Vector2.zero,low,high);var text=r.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;text.color=colour;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;return text;
        }
        private Image Bar(Transform parent,string name,float x,float y,Color colour)
        {
            var r=Element(name,parent,Vector2.zero,Vector2.zero,new Vector2(x,y),new Vector2(x+480,y+16));var bg=r.gameObject.AddComponent<Image>();bg.color=new Color(.16f,.2f,.25f);bg.raycastTarget=false;
            var f=Element("Fill",r,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);var fill=f.gameObject.AddComponent<Image>();fill.color=colour;fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.fillAmount=0;fill.raycastTarget=false;return fill;
        }
        private Button Button(string name,Transform parent,string label,Vector2 low,Vector2 high,UnityEngine.Events.UnityAction action)
        {
            var r=Element(name,parent,Vector2.zero,Vector2.zero,low,high);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.17f,.26f,.31f);var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(action);
            var text=Text("Label",r,label,20,new Vector2(10,0),new Vector2(high.x-low.x-10,high.y-low.y),Color.white);text.alignment=TextAlignmentOptions.Center;return button;
        }
        public void Refresh(string message)
        {
            var m=game.Model;var now=WhackSensorGame.Now;
            red.text="RED  チャージ "+m.Red.Charge.ToString("0")+" / 100    往復 "+m.Red.ShakeCount+"回";
            blue.text="BLUE  チャージ "+m.Blue.Charge.ToString("0")+" / 100    往復 "+m.Blue.ShakeCount+"回";
            redFill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01((float)(m.Red.Charge/100)),1);blueFill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01((float)(m.Blue.Charge/100)),1);
            string PlayerStatus(SensorBridge.SensorArenaModel.Player p) => m.Ready(p,now)?(p.Simulated?"合成データ":"受信中"):(p.Connected?"接続済・センサー待ち":"未接続");
            var connection="赤 "+PlayerStatus(m.Red)+" ／ 青 "+PlayerStatus(m.Blue);
            var relay=game.Receiver.Status=="Connected"?"接続済":game.Receiver.Status=="Connecting..."?"接続準備中":game.Receiver.Status.StartsWith("Reconnecting")?"再接続中":game.Receiver.Status;
            status.text="中継 "+relay+" ／ Room "+game.roomKey+" ／ "+connection+"\n"+(m.MatchBusy?m.Result:"腕を回すとチャージ。"+message);
            bool ready=game.IsPlaying&&!m.MatchBusy;
            contest.interactable=ready&&m.Ready(m.Red,now)&&m.Ready(m.Blue,now)&&m.Flash==SensorBridge.SensorArenaModel.FlashPhase.Idle;
            charge.interactable=ready&&game.Holes.Any(h=>h.IsTracked);
            flash.interactable=contest.interactable&&game.Holes.Any(h=>h.IsTracked);
        }
    }
}
