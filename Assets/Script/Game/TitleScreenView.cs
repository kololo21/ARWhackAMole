using UnityEngine;
using UnityEngine.UI;
using TMPro;

// タイトル画面の飾り(ロゴ・顔を出すもぐら・遊び方の3ステップ)と START ボタンの見た目。
// UIController が起動時に TitlePanel の下へ作る。Scene の配置は変えず、文字と色だけを整える。
// 下半分はカメラ設定と準備欄(SensorGameHud)が使うので、ロゴ類は画面の上半分に収める。
public class TitleScreenView : MonoBehaviour
{
    private RectTransform leftMole;
    private RectTransform rightMole;
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

        // 日本語のロゴ。縁取りと影で、カメラ映像の上でも読めるようにする
        TMP_Text logo = UITheme.MakeLabel("TitleLogo", panel, new Vector2(0, 372), new Vector2(1000, 170), "モグラたたき", 132, UITheme.Text);
        logo.fontStyle = FontStyles.Bold;
        logo.characterSpacing = 6;
        UITheme.StyleText(logo, UITheme.Ink, .28f);

        // Scene にある英語のタイトルは、ロゴの下の小さな副題として使う
        Transform scene = panel.Find("TitleText");
        if (scene != null && scene.TryGetComponent(out TMP_Text subtitle))
        {
            subtitle.text = "AR WHACK-A-MOLE  ·  RED vs BLUE";
            subtitle.fontSize = 34;
            subtitle.characterSpacing = 12;
            subtitle.fontStyle = FontStyles.Bold;
            subtitle.color = UITheme.Accent;
            subtitle.alignment = TextAlignmentOptions.Center;
            subtitle.raycastTarget = false;
            var rect = subtitle.rectTransform;
            rect.anchoredPosition = new Vector2(0, 262);
            rect.sizeDelta = new Vector2(1400, 50);
        }

        // 遊び方を3つの札で示す
        string[] steps = { "右手で穴をかくして撃破", "腕を回してチャージ", "同じ色のモグラで得点" };
        for (int i = 0; i < steps.Length; i++)
        {
            Image chip = UITheme.MakeImage("HowTo" + (i + 1), panel, new Vector2((i - 1) * 440, 172), new Vector2(410, 58), Color.white);
            UITheme.StylePanel(chip, new Color(UITheme.Ink.r, UITheme.Ink.g, UITheme.Ink.b, .88f), true, true);
            string number = ((char)('①' + i)).ToString();
            UITheme.MakeLabel("Text", chip.transform, Vector2.zero, new Vector2(400, 58),
                $"<color={UITheme.AccentHex}>{number}</color> {steps[i]}", 26, UITheme.Text);
        }

        StyleStartButton(panel.Find("StartButton"));
    }

    private void StyleStartButton(Transform start)
    {
        if (start == null || !start.TryGetComponent(out Button button)) return;
        UITheme.StyleButton(button, UITheme.Text);
        if (button.targetGraphic.TryGetComponent(out Outline _) == false)
            button.targetGraphic.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
        button.targetGraphic.GetComponent<Outline>().effectColor = UITheme.Ink;

        TMP_Text label = start.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.font = UITheme.JapaneseFont;
            label.text = "▶ START";
            label.fontSize = 58;
            label.fontStyle = FontStyles.Bold;
            label.color = UITheme.Text;
            UITheme.StyleText(label, UITheme.Ink, .22f, false);
        }
        startButton = (RectTransform)start;
        startScale = startButton.localScale;
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        // もぐらが交互にひょこひょこ顔を出す
        // START ボタンはゆっくり脈打たせて、押す場所を目立たせる
        if (startButton != null) startButton.localScale = startScale * (1 + .035f * Mathf.Sin(t * 4));
    }
}
