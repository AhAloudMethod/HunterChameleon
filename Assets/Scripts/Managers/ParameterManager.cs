using System.IO;
using UnityEngine;

public class ParameterManager : MonoBehaviour
{
    public static ParameterManager Instance;

    public GameParameter parameter;

    [SerializeField, Range(30, 60)]
    private int frameRate = 60;

    private string defaultPath;
    private string writePath;

    private void Awake()
    {
        if (Instance == null)
        {
            defaultPath = Path.Combine(Application.streamingAssetsPath, "GameParameter.json");
            writePath = Path.Combine(Application.persistentDataPath, "GameParameter.json");

            LoadParameter();
            SaveParameter();

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        // フレームレートを設定
        Application.targetFrameRate = frameRate;
    }

    private void LoadParameter()
    {
        // 起動時は常に初期値(StreamingAssets)から読み込む
        // マウス感度など前回プレイ時の変更値は、ゲーム再起動で初期値に戻す
        string json = File.ReadAllText(defaultPath);
        if (parameter == null)
            parameter = JsonUtility.FromJson<GameParameter>(json);
        else
            JsonUtility.FromJsonOverwrite(json, parameter);
    }

    public void ReloadParameter()
    {
        // マウス感度などプレイヤーがSettingsで変更した入力設定は、再読み込みで初期値に戻さない
        InputParameter input = parameter.input;
        float sensitivity = input.sensitivity;

        string json = File.ReadAllText(defaultPath);
        JsonUtility.FromJsonOverwrite(json, parameter);

        // 入力スクリプトがキャッシュしている参照を維持しつつ、ユーザ設定値を復元する
        parameter.input = input;
        parameter.input.sensitivity = sensitivity;
        SaveParameter();
    }

    public void SaveParameter()
    {
        string json = JsonUtility.ToJson(parameter, true);
        File.WriteAllText(writePath, json);
    }
}

