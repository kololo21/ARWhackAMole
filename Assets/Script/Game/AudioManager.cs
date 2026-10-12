using UnityEngine;

// BGM と効果音を担当する。
// GameManager の状態の通知を受けて、タイトル曲 / ゲーム曲 / 結果音 を切り替える。
// AudioManager オブジェクト(子に BGM と SE を持つ)に付けて使う。
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("鳴らす場所 (AudioSource)")]
    [SerializeField] private AudioSource bgmSource;   // ループする曲用
    [SerializeField] private AudioSource seSource;    // 重ねて鳴らす効果音用

    [Header("BGM")]
    [SerializeField] private AudioClip bgmTitle;
    [SerializeField] private AudioClip bgmGame;

    [Header("効果音")]
    [SerializeField] private AudioClip seStart;
    [SerializeField] private AudioClip seDefeat;
    [SerializeField] private AudioClip seExpire;
    [SerializeField] private AudioClip seResult;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("AudioManager: シーンに GameManager がありません");
            return;
        }
        gm.OnStateChanged += HandleState;
        HandleState(gm.State);   // 最初の通知を取りこぼさないよう、今の状態に合わせる
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleState;
        }
    }

    private void HandleState(GameState s)
    {
        switch (s)
        {
            case GameState.Title:
                PlayBgm(bgmTitle);
                break;
            case GameState.Playing:
                PlaySe(seStart);
                PlayBgm(bgmGame);
                break;
            case GameState.Result:
                bgmSource.Stop();
                PlaySe(seResult);
                break;
        }
    }

    // 曲を切り替える。同じ曲がすでに鳴っていれば最初からやり直さない
    public void PlayBgm(AudioClip clip)
    {
        if (clip == null) return;
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
        Debug.Log($"AudioManager: BGM {clip.name} (再生中={bgmSource.isPlaying})");
    }

    // 効果音。PlayOneShot は前の音を止めずに重ねて鳴らす
    public void PlaySe(AudioClip clip)
    {
        if (clip == null) return;
        seSource.PlayOneShot(clip);
    }

    // 段階3(キャラの撃破・時間切れ)から呼ぶ用
    public void PlayDefeatSe() { PlaySe(seDefeat); }
    public void PlayExpireSe() { PlaySe(seExpire); }

    // コンポーネントを付けた瞬間(または右クリック → Reset)にエディタが呼ぶ。
    // 子の BGM / SE から AudioSource を、Assets/Audio から曲を探して欄を埋める。
    private void Reset()
    {
        Transform bgm = transform.Find("BGM");
        Transform se = transform.Find("SE");
        if (bgm != null) bgmSource = bgm.GetComponent<AudioSource>();
        if (se != null) seSource = se.GetComponent<AudioSource>();

#if UNITY_EDITOR
        // AssetDatabase はエディタ専用なので、ビルドには含めない
        bgmTitle = LoadClip("bgm_title");
        bgmGame = LoadClip("bgm_game");
        seStart = LoadClip("se_start");
        seDefeat = LoadClip("se_defeat");
        seExpire = LoadClip("se_expire");
        seResult = LoadClip("se_result");
#endif
    }

#if UNITY_EDITOR
    private static AudioClip LoadClip(string fileName)
    {
        return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/{fileName}.ogg");
    }
#endif
}
