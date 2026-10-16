using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WhackAMole.Sensors;

// ゲーム中だけ、一定間隔でキャラを出す係。
// マーカーが見えていて空いている穴から 1 つ選び、色・種類・寿命をランダムに決めて HoleSlot.Spawn を呼ぶ。
// 得点・撃破音・消滅音は HoleSlot 側が担当するので、ここでは出すことだけを考える。
public class DwellerSpawner : MonoBehaviour
{
    [Header("出す場所(各 ImageTarget の HoleSlot)")]
    [SerializeField] private HoleSlot[] slots;

    [Header("出すキャラ(色ごと)")]
    [SerializeField] private GameObject[] redPrefabs;
    [SerializeField] private GameObject[] bluePrefabs;

    [Header("出し方(秒)")]
    [SerializeField] private float firstDelay = 1.0f;       // START から最初の 1 体までの待ち時間
    [SerializeField] private float intervalMin = 1.0f;      // 次を出すまでの間隔(最小)
    [SerializeField] private float intervalMax = 2.0f;      // 次を出すまでの間隔(最大)
    [SerializeField] private float lifetimeMin = 2.0f;      // 出てから引っ込むまで(最小)
    [SerializeField] private float lifetimeMax = 4.0f;      // 出てから引っ込むまで(最大)

    [Header("同時に出ている数の上限")]
    [SerializeField] private int maxAlive = 2;

    [Header("赤が選ばれる確率 (0〜1)")]
    [Range(0f, 1f)]
    [SerializeField] private float redChance = 0.5f;

    private Coroutine loop;
    private int taughtChargeMask;
    public bool IsAtCapacity
    {
        get
        {
            int occupied=0;foreach(var slot in slots)if(slot!=null&&(!slot.IsEmpty||WhackSensorGame.Instance!=null&&WhackSensorGame.Instance.IsReserved(slot)))occupied++;
            return occupied>=maxAlive;
        }
    }

    private void Start()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("DwellerSpawner: シーンに GameManager がありません");
            return;
        }
        gm.OnStateChanged += HandleState;
        HandleState(gm.State);
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null) HandleState(GameManager.Instance.State);
    }

    private void OnDisable()
    {
        if (loop != null) StopCoroutine(loop);
        loop = null;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleState;
        }
    }

    // ゲーム中になったら出し始め、それ以外になったら止める
    private void HandleState(GameState s)
    {
        if (loop != null)
        {
            StopCoroutine(loop);
            loop = null;
        }
        if (s == GameState.Playing && isActiveAndEnabled)
        {
            taughtChargeMask=0;
            loop = StartCoroutine(SpawnLoop());
        }
    }

    private IEnumerator SpawnLoop()
    {
        yield return WaitPlayingSeconds(firstDelay);
        while (true)
        {
            SpawnOne();
            yield return WaitPlayingSeconds(Random.Range(intervalMin, intervalMax));
        }
    }

    private IEnumerator WaitPlayingSeconds(float seconds)
    {
        float elapsed = 0;
        while (elapsed < seconds)
        {
            if (GameManager.Instance != null && !GameManager.Instance.SensorsPaused) elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void SpawnOne()
    {
        if (GameManager.Instance == null || GameManager.Instance.SensorsPaused) return;
        // 今出ている数を数え、空いていてマーカーが見えている穴を集める
        int alive = 0;
        List<HoleSlot> candidates = new List<HoleSlot>();
        foreach (HoleSlot slot in slots)
        {
            if (slot == null) continue;
            if (!slot.IsEmpty||WhackSensorGame.Instance!=null&&WhackSensorGame.Instance.IsReserved(slot)) alive++;
            else if (slot.IsTracked && (WhackSensorGame.Instance == null || !WhackSensorGame.Instance.IsReserved(slot))) candidates.Add(slot);
        }
        if (alive >= maxAlive || candidates.Count == 0) return;

        // 穴・色・種類・寿命をランダムに決める。int の Random.Range は上限を含まない
        HoleSlot target = candidates[Random.Range(0, candidates.Count)];
        DwellerColor color = Random.value < redChance ? DwellerColor.Red : DwellerColor.Blue;
        var game=WhackSensorGame.Instance;
        float elapsed=GameManager.Instance.gameDuration-GameManager.Instance.RemainingTime;
        bool teach=false;
        if(game!=null&&elapsed>=8)
        {
            if((taughtChargeMask&1)==0&&game.CanSpawn(MoleKind.Charge,DwellerColor.Red)){color=DwellerColor.Red;teach=true;}
            else if((taughtChargeMask&2)==0&&game.CanSpawn(MoleKind.Charge,DwellerColor.Blue)){color=DwellerColor.Blue;teach=true;}
        }
        GameObject[] pool = color == DwellerColor.Red ? redPrefabs : bluePrefabs;
        if (pool == null || pool.Length == 0) return;
        var weighted=new List<GameObject>();
        foreach(var item in pool)
        {
            if(item==null)continue;
            var kind=game!=null?game.Catalog.KindOf(item):MoleKind.Normal;
            if(kind==MoleKind.Flash||elapsed<8&&kind!=MoleKind.Normal||(kind==MoleKind.Balance||kind==MoleKind.Sleepy)&&elapsed<20||kind==MoleKind.King&&elapsed<35||game!=null&&!game.CanSpawn(kind,color))continue;
            int weight=kind==MoleKind.Normal?4:kind==MoleKind.Charge?3:kind==MoleKind.King?1:2;
            for(int i=0;i<weight;i++)weighted.Add(item);
        }
        if(weighted.Count==0)return;
        GameObject prefab=teach?game.Catalog.Prefab(MoleKind.Charge,WhackSensorGame.Team(color)):weighted[Random.Range(0,weighted.Count)];
        float life = Random.Range(lifetimeMin, lifetimeMax);

        if (target.Spawn(prefab, color, teach?8:life))
        {
            if(teach)taughtChargeMask|=color==DwellerColor.Red?1:2;
            Debug.Log($"DwellerSpawner: {target.name} に {prefab.name} ({life:F1} 秒)");
        }
    }

    // 付けた瞬間にエディタが呼ぶ。シーンの HoleSlot と、Prefabs フォルダの赤・青のキャラを集めて欄を埋める
    private void Reset()
    {
        HoleSlot[] found = FindObjectsByType<HoleSlot>(FindObjectsSortMode.None);
        System.Array.Sort(found, (a, b) => string.CompareOrdinal(a.name, b.name));
        slots = found;
#if UNITY_EDITOR
        redPrefabs = LoadPrefabs("_Red");
        bluePrefabs = LoadPrefabs("_Blue");
#endif
    }

#if UNITY_EDITOR
    private static GameObject[] LoadPrefabs(string suffix)
    {
        List<GameObject> list = new List<GameObject>();
        string[] guids = UnityEditor.AssetDatabase.FindAssets("Dweller_ t:Prefab", new[] { "Assets/Characters/Prefabs" });
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(suffix + ".prefab"))
            {
                list.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
        }
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list.ToArray();
    }
#endif
}
