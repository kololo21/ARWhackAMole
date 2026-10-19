using UnityEngine;
using UnityEngine.UI;
using TMPro;

// タイトル画面のロゴ・副題・遊び方の一行と、START ボタンの見た目。
// UIController が起動時に TitlePanel の下へ作る。Scene のファイルは変えず、実行時に文字・色・配置を整える。
// 下半分はカメラ設定と準備欄(SensorGameHud)が使うので、ロゴ類は画面の上半分に収める。
public class TitleScreenView : MonoBehaviour
{
    // START はカメラ設定(上)と準備欄(下, 画面下端から 340)の間に収める
    private static readonly Vector2 StartPosition = new Vector2(0, -140);
    private static readonly Vector2 StartSize = new Vector2(360, 96);

    private RectTransform startButton;
    private Vector3 startScale;

    public static TitleScreenView Build(GameObject titlePanel)
    {
        var view = titlePanel.AddComponent<TitleScreenView>();
        view.Decorate();
        return view;
    }

    private void Decorate()
    {
        Transform panel = transform;

        // Scene のタイトルを、日本語対応フォントでゲーム名のロゴとして整える
        Transform scene = panel.Find("TitleText");
        if (scene != null && scene.TryGetComponent(out TMP_Text logo))
        {
            logo.font = UITheme.JapaneseFont;
            logo.text = "リアモグ";
            logo.fontSize = 124;
            logo.enableAutoSizing = false;
            logo.textWrappingMode = TextWrappingModes.NoWrap;
            logo.fontStyle = FontStyles.Bold;
            logo.characterSpacing = 6;
            logo.color = UITheme.Text;
            logo.alignment = TextAlignmentOptions.Center;
            logo.raycastTarget = false;
            logo.rectTransform.anchoredPosition = new Vector2(0, 352);
            logo.rectTransform.sizeDelta = new Vector2(1600, 160);
            UITheme.StyleText(logo, UITheme.Ink, .12f);
        }

        // 小さな英語表記をゲーム名の直下に置き、その下に短い黄色の線を引く
        var subtitle = UITheme.MakeLabel("TitleSubtitle", panel, new Vector2(0, 248), new Vector2(1200, 44),
            "REAL MOGURA", 30, UITheme.Subtle);
        subtitle.characterSpacing = 5;
        UITheme.MakeImage("TitleAccent", panel, new Vector2(0, 208), new Vector2(120, 6), UITheme.Accent).sprite = UITheme.RoundedSmall;

        // 遊び方は枠を付けず、一行の控えめな文字にする
        UITheme.MakeLabel("TitleHowTo", panel, new Vector2(0, 168), new Vector2(1500, 40),
            $"<color={UITheme.AccentHex}>1</color>  右手で穴をかくす　　<color={UITheme.AccentHex}>2</color>  腕を回してチャージ　　<color={UITheme.AccentHex}>3</color>  同じ色のモグラで得点",
            25, UITheme.Subtle);

        StyleStartButton(panel.Find("StartButton"));
    }

    private void StyleStartButton(Transform start)
    {
        if (start == null || !start.TryGetComponent(out Button button)) return;
        UITheme.StyleButton(button, UITheme.Text);
        startButton = (RectTransform)start;
        startButton.anchoredPosition = StartPosition;
        startButton.sizeDelta = StartSize;
        startScale = startButton.localScale;

        TMP_Text label = start.GetComponentInChildren<TMP_Text>();
        if (label == null) return;
        label.text = "START";
        label.fontSize = 50;
        label.fontStyle = FontStyles.Bold;
        label.characterSpacing = 8;
        label.color = UITheme.Ink;
    }

    private void Update()
    {
        // START だけをゆっくり脈打たせて、押す場所を目立たせる
        if (startButton != null) startButton.localScale = startScale * (1 + .02f * Mathf.Sin(Time.unscaledTime * 3));
    }
}
