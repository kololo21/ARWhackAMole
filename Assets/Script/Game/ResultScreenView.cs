using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 結果画面。赤・青のスコアカードで得点を数え上げ、そのあと勝敗のバナーを弾ませて出す。
// UIController が起動時に ResultPanel の下へ作り、結果に入るたびに Show を呼ぶ。
// 画面の下半分はカメラ設定・RETRY・準備欄(SensorGameHud)が使うので、上半分に収める。
public class ResultScreenView : MonoBehaviour
{
    private const float CountSeconds = 1.0f; // 得点を数え上げる時間
    private const float PopSeconds = .3f;    // バナーが弾む時間

    private sealed class Card
    {
        public RectTransform Root;
        public Outline Edge;
        public TMP_Text Score;
        public GameObject Ribbon;
        public Color Team;
        public int Target;
    }

    private Card red, blue;
    private TMP_Text banner, verdict;
    private float shownAt = -100;

    public static ResultScreenView Build(GameObject resultPanel, TMP_Text sceneText)
    {
        var view = resultPanel.AddComponent<ResultScreenView>();
        view.Decorate(sceneText);
        return view;
    }

    private void Decorate(TMP_Text sceneText)
    {
        Transform panel = transform;
        // 従来の青い半透明の幕を、土の色の暗幕に変える
        if (TryGetComponent(out Image shade)) shade.color = new Color(UITheme.Ink.r, UITheme.Ink.g, UITheme.Ink.b, .78f);
        // 文字だけの結果表示は、このビューに置き換える
        if (sceneText != null) sceneText.gameObject.SetActive(false);

        TMP_Text heading = UITheme.MakeLabel("ResultHeading", panel, new Vector2(0, 505), new Vector2(600, 50), "RESULT", 32, UITheme.Accent);
        heading.characterSpacing = 30;
        heading.fontStyle = FontStyles.Bold;

        banner = UITheme.MakeLabel("ResultBanner", panel, new Vector2(0, 428), new Vector2(1200, 130), "", 112, UITheme.Text);
        banner.fontStyle = FontStyles.Bold;
        UITheme.StyleText(banner, UITheme.Ink, .3f);
        verdict = UITheme.MakeLabel("ResultVerdict", panel, new Vector2(0, 340), new Vector2(1200, 50), "", 38, UITheme.Text);

        red = MakeCard("Red", new Vector2(-275, 172), "RED", UITheme.Red);
        blue = MakeCard("Blue", new Vector2(275, 172), "BLUE", UITheme.Blue);

        StyleRetryButton(panel.Find("RetryButton"));
    }

    private Card MakeCard(string name, Vector2 center, string team, Color colour)
    {
        var card = new Card { Team = colour };
        Image body = UITheme.MakeImage(name + "ScoreCard", transform, center, new Vector2(380, 200), Color.white);
        UITheme.StylePanel(body, new Color(UITheme.Surface.r, UITheme.Surface.g, UITheme.Surface.b, .95f));
        card.Root = body.rectTransform;
        card.Edge = body.GetComponent<Outline>();
        // 上端のチーム色の帯
        Image strip = UITheme.MakeImage("TeamStrip", body.transform, new Vector2(0, 84), new Vector2(340, 10), colour);
        strip.sprite = UITheme.RoundedSmall;
        strip.type = Image.Type.Sliced;
        UITheme.MakeLabel("Team", body.transform, new Vector2(0, 52), new Vector2(360, 44), team, 34, colour).fontStyle = FontStyles.Bold;
        card.Score = UITheme.MakeLabel("Score", body.transform, new Vector2(0, -26), new Vector2(360, 110), "0", 96, UITheme.Text);
        card.Score.fontStyle = FontStyles.Bold;

        Image ribbon = UITheme.MakeImage("WinnerRibbon", body.transform, new Vector2(0, 122), new Vector2(220, 46), UITheme.Accent);
        UITheme.StylePanel(ribbon, UITheme.Accent, false, true);
        UITheme.MakeLabel("Text", ribbon.transform, Vector2.zero, new Vector2(220, 46), "★ WINNER", 28, UITheme.Ink).fontStyle = FontStyles.Bold;
        card.Ribbon = ribbon.gameObject;
        return card;
    }

    private void StyleRetryButton(Transform retry)
    {
        if (retry == null || !retry.TryGetComponent(out Button button)) return;
        UITheme.StyleButton(button, UITheme.Accent);
        TMP_Text label = retry.GetComponentInChildren<TMP_Text>();
        if (label == null) return;
        label.font = UITheme.JapaneseFont;
        label.text = "▶ RETRY";
        label.fontSize = 58;
        label.fontStyle = FontStyles.Bold;
        label.color = UITheme.Ink;
    }

    // 結果画面に入ったときに呼ぶ。演出を最初から再生する
    public void Show(int redScore, int blueScore)
    {
        red.Target = redScore;
        blue.Target = blueScore;
        shownAt = Time.unscaledTime;

        bool draw = redScore == blueScore;
        bool redWins = redScore > blueScore;
        banner.text = draw ? "DRAW" : redWins ? "RED WIN!" : "BLUE WIN!";
        banner.color = draw ? UITheme.Accent : redWins ? UITheme.Red : UITheme.Blue;
        verdict.text = draw ? $"引き分け！ どちらも {redScore} 点"
            : $"{(redWins ? "赤" : "青")}チームの勝ち！ <color={UITheme.SubtleHex}>（{Mathf.Abs(redScore - blueScore)}点差）</color>";

        Highlight(red, !draw && redWins);
        Highlight(blue, !draw && !redWins);
        Update();
    }

    private static void Highlight(Card card, bool winner)
    {
        card.Ribbon.SetActive(winner);
        card.Edge.effectColor = winner ? UITheme.Accent : card.Team;
        card.Edge.effectDistance = winner ? new Vector2(5, -5) : new Vector2(3, -3);
        card.Root.localScale = Vector3.one * (winner ? 1.06f : 1);
    }

    private void Update()
    {
        float age = Time.unscaledTime - shownAt;
        // 数え上げは最初速く、最後ゆっくり(ease-out)
        float count = 1 - Mathf.Pow(1 - Mathf.Clamp01(age / CountSeconds), 3);
        red.Score.text = Mathf.RoundToInt(red.Target * count).ToString();
        blue.Score.text = Mathf.RoundToInt(blue.Target * count).ToString();

        // 数え終わったらバナーと勝敗の文を弾ませて出す
        float pop = Mathf.Clamp01((age - CountSeconds) / PopSeconds);
        float overshoot = pop < 1 ? Mathf.Sin(pop * Mathf.PI * .5f) * (1 + .25f * Mathf.Sin(pop * Mathf.PI)) : 1;
        banner.transform.localScale = Vector3.one * overshoot;
        verdict.alpha = pop;
        red.Ribbon.transform.localScale = blue.Ribbon.transform.localScale = Vector3.one * overshoot;
    }
}
