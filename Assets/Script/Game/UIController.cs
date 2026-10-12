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

    private void Start()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("UIController: シーンに GameManager がありません");
            return;
        }

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
        }
    }

    // 得点が変わったとき (赤, 青)
    private void HandleScore(int red, int blue)
    {
        lastRed = red;
        lastBlue = blue;
        redScoreText.text = $"RED {red}";
        blueScoreText.text = $"BLUE {blue}";
    }

    // 残り時間が変わったとき。9.2 秒なら「10」と切り上げて表示する
    private void HandleTime(float t)
    {
        timeText.text = Mathf.CeilToInt(t).ToString();
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
