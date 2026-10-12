using UnityEngine;
using System;

public enum GameState { Title, Playing, Result };
public enum DwellerColor { Red, Blue };

public class GameManager : MonoBehaviour
{

    public static GameManager Instance;
    public float gameDuration = 180f; // ゲームの制限時間（秒）
    public int redPoint = 10; // 赤のポイント
    public int bluePoint = 10; // 青のポイント
    public GameState State { get; private set; }
    public bool SensorsPaused { get; set; }
    public float RemainingTime => remainingTime;
    public int RedScore => redScore;
    public int BlueScore => blueScore;
    [SerializeField] private float remainingTime; // 残り時間
    [SerializeField] private int redScore; // 赤のスコア
    [SerializeField] private int blueScore; // 青のスコア

    // イベントの定義
    public event Action<GameState> OnStateChanged;
    public event Action<int, int> OnScoreChanged;   // (赤, 青)
    public event Action<float> OnTimeChanged;


    private void SetState(GameState newState)
    {
        State = newState;
        OnStateChanged?.Invoke(newState);
    }


    public void StartGame()
    {
        redScore = 0;
        blueScore = 0;
        remainingTime = gameDuration;
        SensorsPaused = false;
        SetState(GameState.Playing);
        OnScoreChanged?.Invoke(redScore, blueScore);
        OnTimeChanged?.Invoke(remainingTime);
    }

    void Update()
    {
        if(State != GameState.Playing || SensorsPaused)
        {
            return;
        }
        remainingTime -= Time.deltaTime;
        if(remainingTime <= 0)
        {
            remainingTime = 0;
            OnTimeChanged?.Invoke(remainingTime);
            SetState(GameState.Result);
            return;
        }
        else
        {
            OnTimeChanged?.Invoke(remainingTime);

        }
    }


    public void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // GameManagerをシーン間で保持する場合
        }
        else
        {
            Destroy(gameObject); // 既に存在する場合は破棄
        }
    }


        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
    {
        SetState(GameState.Title);
    }

 

    public void EndGame()
    {
        if (State != GameState.Playing) return;
        remainingTime = 0; SensorsPaused = false;
        OnTimeChanged?.Invoke(remainingTime);
        SetState(GameState.Result);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void AddScore(DwellerColor colors)
    {
        if(State != GameState.Playing || SensorsPaused)
        {
            return; // ゲーム中でない場合はスコアを加算しない
        }
        switch (colors)
        {
            case DwellerColor.Red:
                redScore += redPoint;
                break;
            case DwellerColor.Blue:
                blueScore += bluePoint;
                break;
        }
        OnScoreChanged?.Invoke(redScore, blueScore);
    }

    }

    // Update is called once per frame




