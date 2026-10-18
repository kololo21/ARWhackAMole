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
        TMP_Text logo = MoleTheme.MakeLabel("TitleLogo", panel, new Vector2(0, 372), new Vector2(1000, 170), "モグラたたき", 132, MoleTheme.Cream);
        logo.fontStyle = FontStyles.Bold;
        logo.characterSpacing = 6;
        MoleTheme.StyleText(logo, MoleTheme.SoilDeep, .28f);

        leftMole = MoleTheme.MakeImage("TitleMoleLeft", panel, new Vector2(-610, 360), new Vector2(190, 190), Color.white).rectTransform;
        rightMole = MoleTheme.MakeImage("TitleMoleRight", panel, new Vector2(610, 360), new Vector2(190, 190), Color.white).rectTransform;
        leftMole.GetComponent<Image>().sprite = MoleTheme.Mole;
        rightMole.GetComponent<Image>().sprite = MoleTheme.Mole;
        rightMole.localScale = new Vector3(-1, 1, 1);

        // Scene にある英語のタイトルは、ロゴの下の小さな副題として使う
        Transform scene = panel.Find("TitleText");
        if (scene != null && scene.TryGetComponent(out TMP_Text subtitle))
        {
            subtitle.text = "AR WHACK-A-MOLE  ·  RED vs BLUE";
            subtitle.fontSize = 34;
            subtitle.characterSpacing = 12;
            subtitle.fontStyle = FontStyles.Bold;
            subtitle.color = MoleTheme.Gold;
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
            Image chip = MoleTheme.MakeImage("HowTo" + (i + 1), panel, new Vector2((i - 1) * 440, 172), new Vector2(410, 58), Color.white);
            MoleTheme.StylePanel(chip, new Color(MoleTheme.SoilDeep.r, MoleTheme.SoilDeep.g, MoleTheme.SoilDeep.b, .88f), true, true);
            string number = ((char)('①' + i)).ToString();
            MoleTheme.MakeLabel("Text", chip.transform, Vector2.zero, new Vector2(400, 58),
                $"<color={MoleTheme.GoldHex}>{number}</color> {steps[i]}", 26, MoleTheme.Cream);
        }

        StyleStartButton(panel.Find("StartButton"));
    }

    private void StyleStartButton(Transform start)
    {
        if (start == null || !start.TryGetComponent(out Button button)) return;
        MoleTheme.StyleButton(button, MoleTheme.Grass);
        if (button.targetGraphic.TryGetComponent(out Outline _) == false)
            button.targetGraphic.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
        button.targetGraphic.GetComponent<Outline>().effectColor = MoleTheme.GrassDark;

        TMP_Text label = start.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.font = MoleTheme.JapaneseFont;
            label.text = "▶ START";
            label.fontSize = 58;
            label.fontStyle = FontStyles.Bold;
            label.color = MoleTheme.Cream;
            MoleTheme.StyleText(label, MoleTheme.GrassDark, .22f, false);
        }
        startButton = (RectTransform)start;
        startScale = startButton.localScale;
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        // もぐらが交互にひょこひょこ顔を出す
        if (leftMole != null) leftMole.anchoredPosition = new Vector2(-610, 352 + 14 * Mathf.Max(0, Mathf.Sin(t * 3)));
        if (rightMole != null) rightMole.anchoredPosition = new Vector2(610, 352 + 14 * Mathf.Max(0, Mathf.Sin(t * 3 + Mathf.PI)));
        // START ボタンはゆっくり脈打たせて、押す場所を目立たせる
        if (startButton != null) startButton.localScale = startScale * (1 + .035f * Mathf.Sin(t * 4));
    }
}
