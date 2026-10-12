using System;
using System.Collections;
using UnityEngine;
using Vuforia;

// 穴(マーカー)1つ分を担当する。各 ImageTarget に付けて使う。
// ・Spawn でキャラを出す
// ・キャラがいる間にマーカーを覆われる(Tracked でなくなる)と Defeat → 撃破音・得点
// ・寿命が来たら Hide → 消滅音
// DefaultObserverEventHandler を継承しているので、マーカーが見つかった/見失った通知を受け取れる。
public class HoleSlot : DefaultObserverEventHandler
{
    private enum SlotState { Empty, Alive, Leaving }   // 空き / 出ている / 退場アニメ中

    [Header("出したキャラの大きさ(穴の Scale と合わせる)")]
    [SerializeField] private float dwellerScale = 60f;

    [Header("アニメーション開始から消すまでの秒数")]
    [SerializeField] private float defeatRemoveDelay = 1.4f;  // Defeat は 32 フレーム(約1.07秒)+遷移
    [SerializeField] private float hideRemoveDelay = 0.7f;    // Hide は 10 フレーム(約0.33秒)+遷移

    [Header("テスト用:Play 中に Inspector の右クリック → Debug Spawn")]
    [SerializeField] private GameObject debugPrefab;
    [SerializeField] private DwellerColor debugColor = DwellerColor.Red;
    [SerializeField] private float debugLifetime = 5f;

    public event Action<DwellerColor> OnDefeated;   // 倒された(色つき)
    public event Action OnExpired;                  // 時間切れで引っ込んだ

    public bool IsTracked { get; private set; }               // マーカーが今見えているか
    public bool IsEmpty { get { return state == SlotState.Empty; } }

    private SlotState state = SlotState.Empty;
    private GameObject current;
    private Animator currentAnimator;
    private DwellerColor currentColor;
    private float spawnTime;
    private float lifetime;

    protected override void Start()
    {
        base.Start();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += HandleGameState;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleGameState;
        }
    }

    private void Update()
    {
        // 寿命が来たら引っ込む
        if (state == SlotState.Alive && Time.time - spawnTime >= lifetime)
        {
            Expire();
        }
    }

    // キャラを出す。出せたら true
    public bool Spawn(GameObject prefab, DwellerColor color, float life)
    {
        if (!IsTracked || state != SlotState.Empty || prefab == null) return false;

        current = Instantiate(prefab, transform);   // ImageTarget の子として作る
        current.transform.localPosition = Vector3.zero;
        current.transform.localRotation = Quaternion.identity;
        current.transform.localScale = Vector3.one * dwellerScale;
        currentAnimator = current.GetComponentInChildren<Animator>();

        currentColor = color;
        spawnTime = Time.time;
        lifetime = life;
        state = SlotState.Alive;
        return true;
    }

    protected override void OnTrackingFound()
    {
        base.OnTrackingFound();   // 隠れていた穴やキャラを表示する
        IsTracked = true;
    }

    protected override void OnTrackingLost()
    {
        IsTracked = false;

        if (state == SlotState.Alive)
        {
            // キャラがいる間に覆われた = 倒した。
            // base を呼ぶと表示が消えて Defeat が見えないので、ここでは呼ばない
            Defeat();
            return;
        }
        base.OnTrackingLost();   // それ以外は普通に隠す
    }

    private void Defeat()
    {
        state = SlotState.Leaving;
        if (currentAnimator != null) currentAnimator.SetTrigger("Defeat");
        Debug.Log($"{name}: Defeat ({currentColor})");

        if (GameManager.Instance != null) GameManager.Instance.AddScore(currentColor);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayDefeatSe();
        OnDefeated?.Invoke(currentColor);

        StartCoroutine(RemoveAfter(defeatRemoveDelay));
    }

    private void Expire()
    {
        state = SlotState.Leaving;
        if (currentAnimator != null) currentAnimator.SetTrigger("Hide");
        Debug.Log($"{name}: Hide (時間切れ)");

        if (AudioManager.Instance != null) AudioManager.Instance.PlayExpireSe();
        OnExpired?.Invoke();

        StartCoroutine(RemoveAfter(hideRemoveDelay));
    }

    // アニメーションが終わるのを待ってから消し、空きに戻す
    private IEnumerator RemoveAfter(float seconds)
    {
        GameObject target = current;
        yield return new WaitForSeconds(seconds);
        if (target != null) Destroy(target);
        if (current == target)
        {
            current = null;
            currentAnimator = null;
            state = SlotState.Empty;
        }
    }

    // ゲーム中以外になったら、出ているキャラをすぐ片付ける
    private void HandleGameState(GameState s)
    {
        if (s != GameState.Playing) Clear();
    }

    private void Clear()
    {
        StopAllCoroutines();
        if (current != null) Destroy(current);
        current = null;
        currentAnimator = null;
        state = SlotState.Empty;
    }

    [ContextMenu("Debug Spawn")]
    private void DebugSpawn()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Debug Spawn は Play 中に使ってください");
            return;
        }
        bool ok = Spawn(debugPrefab, debugColor, debugLifetime);
        Debug.Log($"{name}: Debug Spawn {(ok ? "成功" : "失敗(マーカーが見えていないか、すでに出ている)")}");
    }

    // 付けた瞬間にエディタが呼ぶ。覆ったら Lost になるよう「Tracked のみ」にする
    private void Reset()
    {
        StatusFilter = TrackingStatusFilter.Tracked;
#if UNITY_EDITOR
        debugPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Characters/Prefabs/Dweller_Normal_Red.prefab");
#endif
    }
}
