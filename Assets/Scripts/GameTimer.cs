using UnityEngine;

public enum TimerMode { CountUp, CountDown }

public class GameTimer : MonoBehaviour
{
    public static GameTimer i;

    public TimerMode mode = TimerMode.CountUp;
    [Tooltip("カウントダウン時の上限秒数")]
    public float timeLimit = 60f;
    [Tooltip("CountUp/CountDown をランタイム切り替えするキー")]
    public KeyCode toggleKey = KeyCode.T;
    [Tooltip("オフにするとHUDを非表示")]
    public bool showHUD = true;

    float elapsed;
    bool running;

    public float DisplayTime => mode == TimerMode.CountUp
        ? elapsed
        : Mathf.Max(0f, timeLimit - elapsed);

    public TimerMode CurrentMode => mode;
    public bool IsRunning => running;
    public float Elapsed => elapsed;

    void Awake()
    {
        i = this;

        if (showHUD && FindObjectOfType<TimerHUD>() == null)
        {
            var hudHost = new GameObject("TimerHUD_Host");
            DontDestroyOnLoad(hudHost);
            hudHost.AddComponent<TimerHUD>();
        }
    }

    public void ToggleMode() => mode = mode == TimerMode.CountUp ? TimerMode.CountDown : TimerMode.CountUp;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) ToggleMode();

        if (running)
            elapsed += Time.deltaTime;
    }

    public void StartTimer()
    {
        elapsed = 0f;
        running = true;
    }

    public void StopTimer() => running = false;

    public void ResetTimer()
    {
        elapsed = 0f;
        running = false;
    }
}
