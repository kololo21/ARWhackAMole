using UnityEngine;
using TMPro;

// 画面(パネル)の切り替えと、HUD・結果画面の文字の更新を担当する。
// GameManager のイベントを購読して動くので、GameManager 側はこのクラスを知らなくてよい。
// Canvas に付けて使う。
public class UIController : MonoBehaviour
{
    [Header("パネル")]
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject resultPanel;

    [Header("文字")]
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text redScoreText;
    [SerializeField] private TMP_Text blueScoreText;
    [SerializeField] private TMP_Text resultText;

    // 結果画面に出すため、直近の得点を覚えておく
    private int lastRed;
    private int lastBlue;

    // 得点が増えた瞬間(Time.unscaledTime)。数字を一瞬ふくらませる演出に使う
    private float redBumpAt = -10;
    private float blueBumpAt = -10;

    private ResultScreenView resultView;

    private void Start()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("UIController: シーンに GameManager がありません");
            return;
        }

        StyleHud();
        TitleScreenView.Build(titlePanel);
        resultView = ResultScreenView.Build(resultPanel, resultText);

        // 購読(+=)。メソッド名の後ろに () は付けない
        gm.OnStateChanged += HandleState;
        gm.OnScoreChanged += HandleScore;
        gm.OnTimeChanged += HandleTime;

        // Start の呼ばれる順番は決まっていないので、
        // GameManager が先に Title を通知していても取りこぼさないよう、今の状態に合わせる
        HandleState(gm.State);
    }

    private void OnDestroy()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        // 解除(-=)。購読と必ず対にする
        gm.OnStateChanged -= HandleState;
        gm.OnScoreChanged -= HandleScore;
        gm.OnTimeChanged -= HandleTime;
    }

    // 状態に応じて、該当するパネルだけを表示する
    private void HandleState(GameState s)
    {
        titlePanel.SetActive(s == GameState.Title);
        hudPanel.SetActive(s == GameState.Playing);
        resultPanel.SetActive(s == GameState.Result);

        if (s == GameState.Result)
        {
            // <color=...> は TextMeshPro の書式タグ。HUD と同じ赤・青で表示する
            resultText.text =
                $"RESULT\n<color=#FF5A4E>RED {lastRed}</color>   <color=#5AA0FF>BLUE {lastBlue}</color>";
            // 見た目はスコアカードの演出(ResultScreenView)で出す。上の文字は非表示のまま残す
            if (resultView != null) resultView.Show(lastRed, lastBlue);
        }
    }

    // 得点が変わったとき (赤, 青)
    private void HandleScore(int red, int blue)
    {
        if (red > lastRed) redBumpAt = Time.unscaledTime;
        if (blue > lastBlue) blueBumpAt = Time.unscaledTime;
        lastRed = red;
        lastBlue = blue;
        redScoreText.text = $"<size=50%><color={UITheme.RedHex}>RED</color></size>  {red}";
        blueScoreText.text = $"{blue}  <size=50%><color={UITheme.BlueHex}>BLUE</color></size>";
    }

    // 残り時間が変わったとき。9.2 秒なら「10」と切り上げて表示する
    // 残り10秒からは黄色→赤で点滅させて、終わりが近いことを知らせる
    private void HandleTime(float t)
    {
        int seconds = Mathf.CeilToInt(t);
        timeText.text = $"<size=45%><color={UITheme.SubtleHex}>TIME </color></size>{seconds}";
        bool hurry = seconds <= 10 && seconds > 0;
        float pulse = hurry ? .5f + .5f * Mathf.Sin(Time.unscaledTime * 10) : 0;
        timeText.color = hurry ? Color.Lerp(UITheme.Accent, UITheme.Red, pulse) : UITheme.Text;
        timeText.transform.localScale = Vector3.one * (1 + .06f * pulse);
    }

    private void Update()
    {
        // 得点が増えたら 0.35 秒かけて元の大きさへ戻る
        redScoreText.transform.localScale = Vector3.one * Bump(redBumpAt);
        blueScoreText.transform.localScale = Vector3.one * Bump(blueBumpAt);
    }

    private static float Bump(float at)
    {
        float age = Time.unscaledTime - at;
        return age < .35f ? 1 + .25f * (1 - age / .35f) : 1;
    }

    // HUD の文字の後ろに、暗いグレーのプレート(チーム色の細い縁)を敷く。Scene は変更しない
    private void StyleHud()
    {
        Plate(redScoreText, UITheme.Red);
        Plate(blueScoreText, UITheme.Blue);
        Plate(timeText, new Color(1, 1, 1, .12f));
        foreach (TMP_Text text in new[] { redScoreText, blueScoreText, timeText })
        {
            text.color = UITheme.Text;
            text.fontStyle = FontStyles.Bold;
        }
        redScoreText.alignment = TextAlignmentOptions.Left;
        blueScoreText.alignment = TextAlignmentOptions.Right;
        timeText.alignment = TextAlignmentOptions.Center;
        HandleScore(0, 0);
    }

    private static void Plate(TMP_Text text, Color edge)
    {
        RectTransform source = text.rectTransform;
        var plate = new GameObject(text.name + "Plate", typeof(RectTransform));
        plate.transform.SetParent(source.parent, false);
        // 文字より奥に描くため、同じ親の中で文字の直前に入れる
        plate.transform.SetSiblingIndex(source.GetSiblingIndex());
        var rect = plate.GetComponent<RectTransform>();
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.anchoredPosition = source.anchoredPosition;
        rect.sizeDelta = source.sizeDelta + new Vector2(10, 0);
        var image = plate.AddComponent<UnityEngine.UI.Image>();
        UITheme.StylePanel(image, new Color(UITheme.Surface.r, UITheme.Surface.g, UITheme.Surface.b, .85f));
        image.GetComponent<UnityEngine.UI.Outline>().effectColor = edge;
        image.raycastTarget = false;
        // 文字の左右に余白を取る
        text.margin = new Vector4(28, 0, 28, 0);
    }

    // エディタでこのコンポーネントを付けた瞬間(または Inspector の右クリック → Reset)に
    // Unity が自動で呼ぶ。Canvas の子から名前で部品を探して欄を埋める。
    // 見つからなかった欄は None のまま残るので、手でドラッグする。
    private void Reset()
    {
        titlePanel = FindObj("TitlePanel");
        hudPanel = FindObj("HudPanel");
        resultPanel = FindObj("ResultPanel");
        timeText = FindText("HudPanel/TimeText");
        redScoreText = FindText("HudPanel/RedScoreText");
        blueScoreText = FindText("HudPanel/BlueScoreText");
        resultText = FindText("ResultPanel/ResultText");
    }

    private GameObject FindObj(string path)
    {
        Transform t = transform.Find(path);
        return t != null ? t.gameObject : null;
    }

    private TMP_Text FindText(string path)
    {
        Transform t = transform.Find(path);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }
}
