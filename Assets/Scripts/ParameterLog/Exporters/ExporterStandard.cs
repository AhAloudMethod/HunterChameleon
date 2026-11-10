#if !UNITY_WEBGL || UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 標準的なプラットフォーム（PC/Mobile/Console）用のエクスポート実装。
/// Task.Run を使用して別スレッドで処理を行い、メインスレッドのフリーズを回避する。
/// </summary>
public static class ExporterStandard
{
    // Complete the save before retrying can change the cached rows.
    public static void AutoSave(
        string runId,
        LogItemCache streamCache, LogItemCache snapshotCache,
        IReadOnlyList<(int s0, int s1, int p0, int p1)> trials)
    {
        string baseDirName = Path.Combine("PlayLogs", "run_" + runId);
        try
        {
            string folder = Path.Combine(Application.persistentDataPath, baseDirName);
            Directory.CreateDirectory(folder);
            string basePrefix = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", System.Globalization.CultureInfo.InvariantCulture);
            string filePrefix = basePrefix;
            int suffix = 1;
            while (Directory.GetFiles(folder, filePrefix + "*").Length > 0)
            {
                filePrefix = basePrefix + "_" + suffix++;
            }
            ExportLogic(streamCache, snapshotCache, trials, baseDirName, Application.persistentDataPath, filePrefix);
        }
        catch (Exception exception)
        {
            Debug.LogError("[ExporterStandard] Automatic log save failed: " + exception);
        }
    }

    public static void Export(
        LogItemCache streamCache, LogItemCache snapshotCache,
        IReadOnlyList<(int s0, int s1, int p0, int p1)> trials,
        string baseDirName, Action onComplete)
    {
        // 保存先パスの取得はメインスレッドで行う必要がある
        string savePathBase = Application.persistentDataPath;

        // 重い処理を別スレッドで実行
        Task.Run(() =>
        {
            try
            {
                ExportLogic(streamCache, snapshotCache, trials, baseDirName, savePathBase);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ExporterStandard] Error: {ex}");
            }
        }).ContinueWith(t =>
        {
            // 処理完了後、メインスレッドでコールバックを実行
            onComplete?.Invoke();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private static void ExportLogic(
        LogItemCache streamCache, LogItemCache snapshotCache,
        IReadOnlyList<(int s0, int s1, int p0, int p1)> trials,
        string baseDirName, string savePathBase, string filePrefix = null)
    {
        var files = new List<LogExporter.FileInMemory>();
        var allSnapshotRows = new List<object[]>();

        // Snapshotヘッダ準備
        var rawSnapshotHeader = new List<string> { "trial_index" };
        if (snapshotCache.Header != null) { rawSnapshotHeader.AddRange(snapshotCache.Header); }

        // 試行ごとのループ
        for (int i = 0; i < trials.Count; i++)
        {
            var (s0, s1, p0, p1) = trials[i];
            int trialId = i + 1;

            // Streamデータ処理
            if (s1 > s0)
            {
                var (header, rows) = LogCsvGenerator.SliceCache(streamCache, s0, s1);
                if (rows.Count > 0)
                {
                    byte[] content = LogCsvGenerator.CreateCSVContent(header, rows);
                    string path = filePrefix == null
                        ? $"{baseDirName}/stream_trial{trialId}.csv"
                        : $"{baseDirName}/{filePrefix}_player_progress{(trials.Count > 1 ? "_trial" + trialId : "")}.csv";
                    files.Add(new LogExporter.FileInMemory { Path = path, Content = content });
                }
            }

            // Snapshotデータ蓄積
            if (p1 > p0)
            {
                LogCsvGenerator.AccumulateSnapshotRows(snapshotCache, p0, p1, trialId, allSnapshotRows);
            }
        }

        // 蓄積したSnapshotを一括出力
        if (allSnapshotRows.Count > 0)
        {
            var (finalHeader, finalRows) = LogCsvGenerator.FilterActiveColumns(rawSnapshotHeader, allSnapshotRows);
            byte[] content = LogCsvGenerator.CreateCSVContent(finalHeader, finalRows);
            string path = filePrefix == null
                ? $"{baseDirName}/snapshot.csv"
                : $"{baseDirName}/{filePrefix}.csv";
            files.Add(new LogExporter.FileInMemory { Path = path, Content = content });
        }

        // ファイル書き出し（IO処理）
        foreach (var file in files)
        {
            string fullPath = Path.Combine(savePathBase, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, file.Content);
        }

        Debug.Log($"[ExporterStandard] Export complete: {Path.Combine(savePathBase, baseDirName)}");
    }
}
#endif