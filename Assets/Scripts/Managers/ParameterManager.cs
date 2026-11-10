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
        // 既存ファイルがあればwritePathを使い、なければdefaultPathを使う
        string path = File.Exists(writePath) ? writePath : defaultPath;
        // JSONを読み込み、デシリアライズする
        string json = File.ReadAllText(path);
        if (parameter == null)
            parameter = JsonUtility.FromJson<GameParameter>(json);
        else
            JsonUtility.FromJsonOverwrite(json, parameter);
    }

    public void ReloadParameter()
    {
        string json = File.ReadAllText(defaultPath);
        JsonUtility.FromJsonOverwrite(json, parameter);
        SaveParameter();
    }

    public void SaveParameter()
    {
        string json = JsonUtility.ToJson(parameter, true);
        File.WriteAllText(writePath, json);
    }
}

