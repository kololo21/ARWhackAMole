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
    private RectTransform mole;
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
        if (TryGetComponent(out Image shade)) shade.color = new Color(MoleTheme.SoilDeep.r, MoleTheme.SoilDeep.g, MoleTheme.SoilDeep.b, .78f);
        // 文字だけの結果表示は、このビューに置き換える
        if (sceneText != null) sceneText.gameObject.SetActive(false);

        TMP_Text heading = MoleTheme.MakeLabel("ResultHeading", panel, new Vector2(0, 505), new Vector2(600, 50), "RESULT", 32, MoleTheme.Gold);
        heading.characterSpacing = 30;
        heading.fontStyle = FontStyles.Bold;

        banner = MoleTheme.MakeLabel("ResultBanner", panel, new Vector2(0, 428), new Vector2(1200, 130), "", 112, MoleTheme.Cream);
        banner.fontStyle = FontStyles.Bold;
        MoleTheme.StyleText(banner, MoleTheme.SoilDeep, .3f);
        verdict = MoleTheme.MakeLabel("ResultVerdict", panel, new Vector2(0, 340), new Vector2(1200, 50), "", 38, MoleTheme.Cream);

        red = MakeCard("Red", new Vector2(-275, 172), "RED", MoleTheme.Red);
        blue = MakeCard("Blue", new Vector2(275, 172), "BLUE", MoleTheme.Blue);

        // カードの間で、穴から顔を出すもぐらが結果を見守る
        Image icon = MoleTheme.MakeImage("ResultMole", panel, new Vector2(0, 150), new Vector2(140, 140), Color.white);
        icon.sprite = MoleTheme.Mole;
        mole = icon.rectTransform;

        StyleRetryButton(panel.Find("RetryButton"));
    }

    private Card MakeCard(string name, Vector2 center, string team, Color colour)
    {
        var card = new Card { Team = colour };
        Image body = MoleTheme.MakeImage(name + "ScoreCard", transform, center, new Vector2(380, 200), Color.white);
        MoleTheme.StylePanel(body, new Color(MoleTheme.Soil.r, MoleTheme.Soil.g, MoleTheme.Soil.b, .95f));
        card.Root = body.rectTransform;
        card.Edge = body.GetComponent<Outline>();
        // 上端のチーム色の帯
        Image strip = MoleTheme.MakeImage("TeamStrip", body.transform, new Vector2(0, 84), new Vector2(340, 10), colour);
        strip.sprite = MoleTheme.RoundedSmall;
        strip.type = Image.Type.Sliced;
        MoleTheme.MakeLabel("Team", body.transform, new Vector2(0, 52), new Vector2(360, 44), team, 34, colour).fontStyle = FontStyles.Bold;
        card.Score = MoleTheme.MakeLabel("Score", body.transform, new Vector2(0, -26), new Vector2(360, 110), "0", 96, MoleTheme.Cream);
        card.Score.fontStyle = FontStyles.Bold;

        Image ribbon = MoleTheme.MakeImage("WinnerRibbon", body.transform, new Vector2(0, 122), new Vector2(220, 46), MoleTheme.Gold);
        MoleTheme.StylePanel(ribbon, MoleTheme.Gold, false, true);
        MoleTheme.MakeLabel("Text", ribbon.transform, Vector2.zero, new Vector2(220, 46), "★ WINNER", 28, MoleTheme.SoilDeep).fontStyle = FontStyles.Bold;
        card.Ribbon = ribbon.gameObject;
        return card;
    }

    private void StyleRetryButton(Transform retry)
    {
        if (retry == null || !retry.TryGetComponent(out Button button)) return;
        MoleTheme.StyleButton(button, MoleTheme.Gold);
        TMP_Text label = retry.GetComponentInChildren<TMP_Text>();
        if (label == null) return;
        label.font = MoleTheme.JapaneseFont;
        label.text = "▶ RETRY";
        label.fontSize = 58;
        label.fontStyle = FontStyles.Bold;
        label.color = MoleTheme.SoilDeep;
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
        banner.color = draw ? MoleTheme.Gold : redWins ? MoleTheme.Red : MoleTheme.Blue;
        verdict.text = draw ? $"引き分け！ どちらも {redScore} 点"
            : $"{(redWins ? "赤" : "青")}チームの勝ち！ <color={MoleTheme.MutedHex}>（{Mathf.Abs(redScore - blueScore)}点差）</color>";

        Highlight(red, !draw && redWins);
        Highlight(blue, !draw && !redWins);
        Update();
    }

    private static void Highlight(Card card, bool winner)
    {
        card.Ribbon.SetActive(winner);
        card.Edge.effectColor = winner ? MoleTheme.Gold : card.Team;
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

        if (mole != null) mole.anchoredPosition = new Vector2(0, 142 + 12 * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.5f)));
    }
}
