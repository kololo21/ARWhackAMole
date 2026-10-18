using System;
using System.Collections;
using UnityEngine;
using Vuforia;
using WhackAMole.Sensors;

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
    private float activeAge;
    public GameObject Current => current;
    public DwellerColor CurrentColor => currentColor;
    public MoleKind CurrentKind { get; private set; }
    public bool IsAlive => state == SlotState.Alive;
    private float lifetime;
    private bool helmetBroken,weakened,wasSensorTarget;
    private float tiltProgress,trackedAge;
    private bool hasDisplayPose;
    private Vector3 lastTrackedPosition;
    private Quaternion lastTrackedRotation;
    private string interactionFeedback;
    private float feedbackUntil;
    private bool KeepDisplay => hasDisplayPose && GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
    public bool HasDisplayPose => IsTracked || KeepDisplay;
    public bool HasInteractionFeedback => !string.IsNullOrEmpty(interactionFeedback) && Time.unscaledTime < feedbackUntil;
    private Transform prop;
    private Mesh removedPropMesh;
    // 回転させる前のキャラの顔の向き(穴のローカル座標、水平)。測れなかったら zero
    private Vector3 faceLocal;
    private float faceYaw;
    private static long nextSpawnOrder;
    public long SpawnOrder {get;private set;}
    public long SpawnSensorSeq {get;private set;}
    public double SpawnTimeMs {get;private set;}
    public bool IsWeakened=>weakened;
    public bool NeedsSensor=>IsAlive&&!weakened&&(CurrentKind==MoleKind.Balance||CurrentKind==MoleKind.Sleepy);
    public float TiltProgress=>tiltProgress;
    public string Instruction
    {
        get
        {
            var game=WhackSensorGame.Instance;string action="手で穴を隠す";
            var player=game?.Model.Get(WhackSensorGame.Team(currentColor));
            if(CurrentKind==MoleKind.Helmet)action=helmetBroken?"装甲破壊！ 手を戻してもう1回":"手を戻しながら2回叩く";
            else if(CurrentKind==MoleKind.Balance)action=weakened?"ボールが落ちた！ 叩こう":$"{game.balanceTiltDegrees:0}°傾けてキープ {tiltProgress/game.balanceHoldSeconds:P0}";
            else if(CurrentKind==MoleKind.Sleepy)action=weakened?"起きた！ 叩こう":player==null||!game.Model.MicrophoneReady(player,WhackSensorGame.Now)?"スマホのマイクを許可":!player.Voice.Calibrated?"周囲音を測定中…":$"{(player.Voice.Armed?"声で起こそう":"一度静かにしてから声を出す")}\n声 {player.MicrophoneRms:0.000} / 目安 {player.Voice.Threshold:0.000}";
            else if(CurrentKind==MoleKind.Charge||CurrentKind==MoleKind.King)
            {
                double cost=game==null?(CurrentKind==MoleKind.King?75:50):SensorGamePolicy.Cost(game.Model,CurrentKind);
                action=player!=null&&player.Charge>=cost?$"叩こう！ チャージ{cost:0}消費":$"腕回しで準備：あと{Math.Ceiling(cost-(player?.Charge??0)):0}";
            }
            if(NeedsSensor&&game!=null&&game.SensorTarget(currentColor)!=this)action="同じ色の先の敵を解除しよう";
            if(HasInteractionFeedback)action=interactionFeedback;
            int points=(GameManager.Instance==null?10:currentColor==DwellerColor.Red?GameManager.Instance.redPoint:GameManager.Instance.bluePoint)*SensorGamePolicy.ScoreMultiplier(CurrentKind);
            return $"{SensorGamePolicy.Name(CurrentKind)} {(currentColor==DwellerColor.Red?"R":"B")} +{points}\n{action}";
        }
    }

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
        if(removedPropMesh!=null)Destroy(removedPropMesh);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleGameState;
        }
    }

    private void Update()
    {
        // 寿命が来たら引っ込む
        if (GameManager.Instance == null || GameManager.Instance.SensorsPaused) return;
        trackedAge=IsTracked?trackedAge+Time.deltaTime:0;
        if(NeedsSensor&&WhackSensorGame.Instance!=null)
        {
            var game=WhackSensorGame.Instance;var player=game.Model.Get(WhackSensorGame.Team(currentColor));
            bool target=game.SensorTarget(currentColor)==this;
            if(!target||!IsTracked||!game.Model.TiltReady(player,WhackSensorGame.Now))tiltProgress=0;
            if(target&&!wasSensorTarget){SpawnSensorSeq=player.LastSeq;tiltProgress=0;if(CurrentKind==MoleKind.Sleepy){player.Voice.ResetDetection();player.LastQuietSeq=-1;}}
            if(player.LastSeq<SpawnSensorSeq)SpawnSensorSeq=player.LastSeq;
            wasSensorTarget=target;
            if(target&&IsTracked&&CurrentKind==MoleKind.Balance&&player.LastSeq>SpawnSensorSeq&&game.Model.TiltReady(player,WhackSensorGame.Now))
            {
                tiltProgress=player.TiltDegrees>=game.balanceTiltDegrees?tiltProgress+Time.deltaTime:0;
                if(tiltProgress>=game.balanceHoldSeconds)Weaken();
            }
        }
        if (state == SlotState.Alive) activeAge += Time.deltaTime;
        if (state == SlotState.Alive && activeAge >= lifetime)
        {
            Expire();
        }
    }

    // キャラを出す。出せたら true
    public bool Spawn(GameObject prefab, DwellerColor color, float life, bool flash = false)
    {
        if (!IsTracked || state != SlotState.Empty || prefab == null || GameManager.Instance == null || GameManager.Instance.State != GameState.Playing || GameManager.Instance.SensorsPaused) return false;

        current = Instantiate(prefab, transform);   // ImageTarget の子として作る
        current.transform.localPosition = Vector3.zero;
        current.transform.localRotation = Quaternion.identity;
        current.transform.localScale = Vector3.one * dwellerScale;
        currentAnimator = current.GetComponentInChildren<Animator>();

        currentColor = color;
        activeAge = 0;
        CurrentKind = flash ? MoleKind.Flash : WhackSensorGame.Instance!=null&&WhackSensorGame.Instance.Catalog!=null?WhackSensorGame.Instance.Catalog.KindOf(prefab):MoleKind.Normal;
        lifetime = flash?life:Mathf.Max(life,SensorGamePolicy.MinimumLifetime(CurrentKind));
        helmetBroken=weakened=wasSensorTarget=false;tiltProgress=0;SpawnOrder=++nextSpawnOrder;SpawnTimeMs=WhackSensorGame.Now;
        interactionFeedback=null;
        SpawnSensorSeq=WhackSensorGame.Instance?.Model.Get(WhackSensorGame.Team(color)).LastSeq??-1;
        prop=null;foreach(var t in current.GetComponentsInChildren<Transform>())if(t.name=="prop"){prop=t;break;}
        MeasureFace();
        FaceCamera(true);
        if(CurrentKind==MoleKind.Helmet&&prop!=null)current.AddComponent<HelmetBreakEffect>().Prepare(prop);
        state = SlotState.Alive;
        if (flash && currentAnimator != null) { currentAnimator.Play("Idle", 0, 0); currentAnimator.Update(0); }
        return true;
    }

    protected override void OnTrackingFound()
    {
        base.OnTrackingFound();   // 隠れていた穴やキャラを表示する
        IsTracked = true;
        trackedAge=0;
        RememberDisplayPose();
    }

    protected override void OnTrackingLost()
    {
        bool wasTracked = IsTracked;
        IsTracked = false;
        if(wasTracked)WhackSensorGame.Instance?.NotifyTrackingLost(this);

        if (wasTracked && state == SlotState.Alive)
        {
            // キャラがいる間に覆われた = 倒した。
            // base を呼ぶと表示が消えて Defeat が見えないので、ここでは呼ばない
            var sensors = WhackSensorGame.Instance;
            if(GameManager.Instance!=null&&GameManager.Instance.State==GameState.Playing&&!GameManager.Instance.SensorsPaused)
            {
                if(CurrentKind==MoleKind.Helmet&&(!helmetBroken||trackedAge<.12f))
                {
                    if(!helmetBroken&&prop!=null)current.GetComponent<HelmetBreakEffect>().Play(prop);
                    helmetBroken=true;ShowInteractionFeedback("装甲破壊！ 手を戻してもう1回");
                    UpdateLostVisibility();return;
                }
                if(NeedsSensor){ShowInteractionFeedback(Instruction.Split('\n')[1]);UpdateLostVisibility();return;}
            }
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing || GameManager.Instance.SensorsPaused || (sensors != null ? !sensors.TryAuthorizeDefeat(currentColor,CurrentKind) : CurrentKind == MoleKind.Charge||CurrentKind==MoleKind.King))
            {
                if(GameManager.Instance!=null&&GameManager.Instance.State==GameState.Playing&&!GameManager.Instance.SensorsPaused)
                {
                    var player=sensors?.Model.Get(WhackSensorGame.Team(currentColor));
                    ShowInteractionFeedback(player==null||!sensors.Model.Ready(player,WhackSensorGame.Now)?"未撃破：スマホの送信を確認":"チャージ不足！ 腕回しで準備");
                }
                UpdateLostVisibility();
                return;
            }
            Defeat();
            return;
        }
        UpdateLostVisibility();
    }

    private void ShowInteractionFeedback(string message)
    {
        interactionFeedback=message;feedbackUntil=Time.unscaledTime+2;
        WhackSensorGame.Instance?.SetNotice(message);
    }

    private void RememberDisplayPose()
    {
        hasDisplayPose=true;lastTrackedPosition=transform.position;lastTrackedRotation=transform.rotation;
    }

    private void UpdateLostVisibility()
    {
        // Display is independent of tracking: keeping the hole visible must not
        // count as reacquisition or allow another hit / spawn under the hand.
        if(KeepDisplay)OnTargetLost?.Invoke();
        else base.OnTrackingLost();
    }

    private void Defeat()
    {
        state = SlotState.Leaving;
        interactionFeedback=null;
        if (currentAnimator != null) currentAnimator.SetTrigger("Defeat");
        Debug.Log($"{name}: Defeat ({currentColor})");

        if (GameManager.Instance != null) GameManager.Instance.AddScore(currentColor,SensorGamePolicy.ScoreMultiplier(CurrentKind));
        if (AudioManager.Instance != null) AudioManager.Instance.PlayDefeatSe();
        OnDefeated?.Invoke(currentColor);
        if (CurrentKind != MoleKind.Charge&&CurrentKind!=MoleKind.King) WhackSensorGame.Instance?.SendHit(currentColor,CurrentKind);

        StartCoroutine(RemoveAfter(defeatRemoveDelay));
    }
    public void Weaken()
    {
        if(!NeedsSensor)return;
        weakened=true;interactionFeedback=null;
        if(CurrentKind==MoleKind.Balance)
        {
            // The ball and its outline share a skinned mesh with the body.
            // Remove ball triangles from a private copy; keep the source intact.
            var renderer=current.GetComponentInChildren<SkinnedMeshRenderer>();
            if(renderer!=null&&renderer.sharedMesh.isReadable)
            {
                var source=renderer.sharedMesh;var vertices=source.vertices;var materials=renderer.sharedMaterials;
                var bounds=new Bounds();bool found=false;
                for(int i=0;i<materials.Length;i++)if(materials[i]!=null&&(materials[i].name.StartsWith("BallA")||materials[i].name.StartsWith("BallB")))
                    foreach(int index in source.GetTriangles(i)){if(!found){bounds=new Bounds(vertices[index],Vector3.zero);found=true;}else bounds.Encapsulate(vertices[index]);}
                if(found)
                {
                    bounds.Expand(bounds.size.magnitude*.12f);removedPropMesh=Instantiate(source);
                    for(int i=0;i<materials.Length;i++)
                    {
                        if(materials[i]==null)continue;
                        if(materials[i].name.StartsWith("BallA")||materials[i].name.StartsWith("BallB"))removedPropMesh.SetTriangles(new int[0],i);
                        else if(materials[i].name.StartsWith("Outline"))
                        {
                            var triangles=source.GetTriangles(i);var kept=new System.Collections.Generic.List<int>();
                            for(int t=0;t<triangles.Length;t+=3)if(!bounds.Contains((vertices[triangles[t]]+vertices[triangles[t+1]]+vertices[triangles[t+2]])/3f)){kept.Add(triangles[t]);kept.Add(triangles[t+1]);kept.Add(triangles[t+2]);}
                            removedPropMesh.SetTriangles(kept,i);
                        }
                    }
                    renderer.sharedMesh=removedPropMesh;
                }
            }
        }
        WhackSensorGame.Instance?.SetNotice(CurrentKind==MoleKind.Balance?"ボールが落ちた！ 手で叩こう":"目が覚めた！ 手で叩こう");
    }
    private void LateUpdate()
    {
        if(IsTracked)RememberDisplayPose();
        else if(KeepDisplay)transform.SetPositionAndRotation(lastTrackedPosition,lastTrackedRotation);
        if(prop!=null&&CurrentKind==MoleKind.Balance&&weakened)prop.localScale=Vector3.zero;
        FaceCamera(false);
    }

    // 頭と両目のボーンから、キャラの顔が向いている水平方向を求める。
    // 両目を結ぶ線に垂直な向きを基本にし、頭→目の向きで前後を決める
    private void MeasureFace()
    {
        faceLocal=Vector3.zero;
        Transform head=null,eyeL=null,eyeR=null;
        foreach(var t in current.GetComponentsInChildren<Transform>())
        {
            if(t.name=="head")head=t;else if(t.name=="eye_L")eyeL=t;else if(t.name=="eye_R")eyeR=t;
        }
        if(eyeL==null||eyeR==null)return;
        Vector3 side=Vector3.ProjectOnPlane(transform.InverseTransformDirection(eyeR.position-eyeL.position),Vector3.up);
        Vector3 forward=Vector3.Cross(side,Vector3.up);
        if(head!=null)
        {
            Vector3 toEyes=Vector3.ProjectOnPlane(transform.InverseTransformDirection((eyeL.position+eyeR.position)*.5f-head.position),Vector3.up);
            if(Vector3.Dot(toEyes,forward)<0)forward=-forward;
        }
        if(forward.sqrMagnitude>1e-10f)faceLocal=forward.normalized;
    }

    // カメラの映像で顔が見えるよう、キャラを穴の上下軸まわりに回してカメラの方へ向ける。
    // 映像の左右・上下反転は描画後に行うので、どの反転設定でも顔が映る
    private void FaceCamera(bool snap)
    {
        var camera=Camera.main;
        if(current==null||camera==null||faceLocal==Vector3.zero)return;
        Vector3 toCamera=Vector3.ProjectOnPlane(transform.InverseTransformPoint(camera.transform.position)-current.transform.localPosition,Vector3.up);
        if(toCamera.sqrMagnitude<1e-8f)return;
        float target=Vector3.SignedAngle(faceLocal,toCamera,Vector3.up);
        faceYaw=snap?target:Mathf.LerpAngle(faceYaw,target,1-Mathf.Exp(-10*Time.deltaTime));
        current.transform.localRotation=Quaternion.AngleAxis(faceYaw,Vector3.up);
    }
    public void ResetSensorProgress(){tiltProgress=0;wasSensorTarget=false;}

    private void Expire()
    {
        state = SlotState.Leaving;
        if(CurrentKind==MoleKind.Flash)WhackSensorGame.Instance?.NotifyFlashExpired(this);
        if (currentAnimator != null) currentAnimator.SetTrigger("Hide");
        Debug.Log($"{name}: Hide (時間切れ)");

        if (AudioManager.Instance != null) AudioManager.Instance.PlayExpireSe();
        OnExpired?.Invoke();

        StartCoroutine(RemoveAfter(hideRemoveDelay));
    }
    public void ExpireNow(){if(IsAlive)Expire();}

    // アニメーションが終わるのを待ってから消し、空きに戻す
    private IEnumerator RemoveAfter(float seconds)
    {
        GameObject target = current;
        yield return new WaitForSeconds(seconds);
        if (target != null) Destroy(target);
        if (current == target)
        {
            if(removedPropMesh!=null){Destroy(removedPropMesh);removedPropMesh=null;}
            current = null;
            currentAnimator = null;
            state = SlotState.Empty;
        }
    }

    // ゲーム中以外になったら、出ているキャラをすぐ片付ける
    private void HandleGameState(GameState s)
    {
        if (s != GameState.Playing) Clear();
        hasDisplayPose=IsTracked;
        if(IsTracked)RememberDisplayPose();
        else base.OnTrackingLost();
    }

    public void Clear()
    {
        StopAllCoroutines();
        if (current != null) Destroy(current);
        if(removedPropMesh!=null){Destroy(removedPropMesh);removedPropMesh=null;}
        current = null;
        currentAnimator = null;
        state = SlotState.Empty;
        interactionFeedback=null;
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

#if UNITY_EDITOR
    public void SetTrackingForVerification(bool found) { if (found) OnTrackingFound(); else OnTrackingLost(); }
#endif

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
