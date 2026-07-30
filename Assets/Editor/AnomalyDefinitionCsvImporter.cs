using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 기획 CSV의 기본 이상현상 정보만 AnomalyDefinition SO로 단방향 반영합니다.
/// Actions 배열과 그 안의 오브젝트/스프라이트/좌표 설정은 절대 변경하지 않습니다.
/// </summary>
public static class AnomalyDefinitionCsvImporter
{
    private const string DefaultOutputFolder = "Assets/Datas/AnomalyDefinitions";

    [MenuItem("Tools/Unrecorded Area/Anomaly/Import Definitions From CSV")]
    public static void ImportFromCsv()
    {
        string csvPath = EditorUtility.OpenFilePanel("Anomaly Definition CSV 선택", Application.dataPath, "csv");
        if (string.IsNullOrEmpty(csvPath))
            return;

        string initialOutputFolder = Directory.Exists(DefaultOutputFolder)
            ? Path.GetFullPath(DefaultOutputFolder)
            : Application.dataPath;
        string outputFolder = EditorUtility.OpenFolderPanel("AnomalyDefinition 상위 폴더 선택 (D1/D2/D3 자동 분류)", initialOutputFolder, string.Empty);
        if (string.IsNullOrEmpty(outputFolder))
            return;

        outputFolder = outputFolder.Replace('\\', '/');
        string projectRoot = Path.GetFullPath(".").Replace('\\', '/');
        if (!outputFolder.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("가져오기 실패", "저장 폴더는 Unity 프로젝트 내부여야 합니다.", "확인");
            return;
        }

        string assetFolder = outputFolder.Substring(projectRoot.Length + 1);
        if (!assetFolder.StartsWith("Assets/", StringComparison.Ordinal) && assetFolder != "Assets")
        {
            EditorUtility.DisplayDialog("가져오기 실패", "저장 폴더는 Assets 내부여야 합니다.", "확인");
            return;
        }

        Import(csvPath, assetFolder);
    }

    private static void Import(string csvPath, string outputFolder)
    {
        List<List<string>> rows;
        try
        {
            rows = ReadCsv(csvPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("가져오기 실패", "CSV를 읽을 수 없습니다. Console을 확인하세요.", "확인");
            return;
        }

        if (rows.Count < 2)
        {
            EditorUtility.DisplayDialog("가져오기 실패", "헤더와 최소 한 개의 데이터 행이 필요합니다.", "확인");
            return;
        }

        Dictionary<string, int> headers = BuildHeaderMap(rows[0]);
        if (!headers.ContainsKey("anomaly_id") || !headers.ContainsKey("day"))
        {
            EditorUtility.DisplayDialog("가져오기 실패", "필수 헤더 'anomaly_id' 또는 'day'가 없습니다.", "확인");
            return;
        }

        EnsureAssetFolder(outputFolder);
        EnsureAssetFolder($"{outputFolder}/D1");
        EnsureAssetFolder($"{outputFolder}/D2");
        EnsureAssetFolder($"{outputFolder}/D3");
        int created = 0;
        int updated = 0;
        int skipped = 0;
        var errors = new List<string>();

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                List<string> row = rows[rowIndex];
                string anomalyId = GetValue(row, headers, "anomaly_id");
                if (string.IsNullOrWhiteSpace(anomalyId))
                    continue;

                anomalyId = anomalyId.Trim();
                if (!TryReadDefinition(row, headers, out DefinitionData data, out string error))
                {
                    skipped++;
                    errors.Add($"행 {rowIndex + 1} ({anomalyId}): {error}");
                    continue;
                }

                string dayFolder = $"{outputFolder}/D{data.Day}";
                string assetPath = $"{dayFolder}/{SanitizeFileName(anomalyId)}.asset";
                AnomalyDefinition definition = AssetDatabase.LoadAssetAtPath<AnomalyDefinition>(assetPath);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<AnomalyDefinition>();
                    AssetDatabase.CreateAsset(definition, assetPath);
                    created++;
                }
                else
                {
                    updated++;
                }

                ApplyBasicDefinitionFields(definition, data);
                EditorUtility.SetDirty(definition);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary = $"생성 {created}개, 갱신 {updated}개, 건너뜀 {skipped}개\n\n" +
                         "CSV의 day 열에 따라 D1/D2/D3 하위 폴더로 자동 분류했습니다.\n" +
                         "Actions 배열과 nextStage 참조는 변경하지 않았습니다.";
        if (errors.Count > 0)
            Debug.LogWarning("[AnomalyDefinitionCsvImporter]\n" + string.Join("\n", errors));

        Debug.Log($"[AnomalyDefinitionCsvImporter] {summary}");
        EditorUtility.DisplayDialog("Anomaly Definition CSV 가져오기 완료", summary, "확인");
    }

    private static bool TryReadDefinition(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> headers,
        out DefinitionData data,
        out string error)
    {
        data = new DefinitionData
        {
            AnomalyId = GetValue(row, headers, "anomaly_id").Trim(),
            ActiveDurationSec = 20f,
        };
        error = string.Empty;

        if (!int.TryParse(GetValue(row, headers, "day"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int day) || day < 1 || day > 3)
        {
            error = "day must be 1, 2, or 3.";
            return false;
        }

        if (!TryParseEnum(GetValue(row, headers, "area_id"), out AreaId areaId))
        {
            error = "area_id가 올바른 AreaId 숫자/이름이 아닙니다.";
            return false;
        }

        if (!TryParseEnum(GetValue(row, headers, "report_target_id"), out ReportTargetId targetId))
        {
            error = "report_target_id가 올바른 ReportTargetId 숫자/이름이 아닙니다.";
            return false;
        }

        string reportTypeValue = GetValue(row, headers, "report_type_id", "report_type");
        if (!TryParseEnum(reportTypeValue, out AnomalyReportType reportType))
        {
            error = "report_type_id가 올바른 AnomalyReportType 숫자/이름이 아닙니다.";
            return false;
        }

        if (!TryParseFloat(GetValue(row, headers, "warning_sec", "warning_duration_sec"), 0f, out float warningDuration) ||
            !TryParseFloat(GetValue(row, headers, "active_sec", "active_duration_sec"), 20f, out float activeDuration))
        {
            error = "warning_sec 또는 active_sec가 숫자가 아닙니다.";
            return false;
        }

        if (!TryParseBool(GetValue(row, headers, "can_escalate"), false, out bool canEscalate) ||
            !TryParseBool(GetValue(row, headers, "is_emergency"), false, out bool isEmergency))
        {
            error = "can_escalate 또는 is_emergency 값은 true/false, 예/아니오, 1/0 중 하나여야 합니다.";
            return false;
        }

        data.Day = day;
        data.AreaId = areaId;
        data.TargetId = targetId;
        data.ReportType = reportType;
        data.WarningDurationSec = Mathf.Max(0f, warningDuration);
        data.ActiveDurationSec = Mathf.Max(0.01f, activeDuration);
        data.CanEscalate = canEscalate;
        data.IsEmergency = isEmergency;
        return true;
    }

    private static void ApplyBasicDefinitionFields(AnomalyDefinition definition, DefinitionData data)
    {
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("anomalyId").stringValue = data.AnomalyId;
        serializedDefinition.FindProperty("areaId").intValue = (int)data.AreaId;
        serializedDefinition.FindProperty("reportTargetId").intValue = (int)data.TargetId;
        serializedDefinition.FindProperty("reportType").intValue = (int)data.ReportType;
        serializedDefinition.FindProperty("warningDurationSec").floatValue = data.WarningDurationSec;
        serializedDefinition.FindProperty("activeDurationSec").floatValue = data.ActiveDurationSec;
        serializedDefinition.FindProperty("canEscalate").boolValue = data.CanEscalate;
        serializedDefinition.FindProperty("isEmergency").boolValue = data.IsEmergency;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Dictionary<string, int> BuildHeaderMap(IReadOnlyList<string> headers)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            string header = headers[i]?.Trim().TrimStart('\uFEFF');
            if (!string.IsNullOrEmpty(header) && !result.ContainsKey(header))
                result.Add(header, i);
        }

        return result;
    }

    private static string GetValue(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> headers, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (headers.TryGetValue(key, out int index) && index >= 0 && index < row.Count)
                return row[index]?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }

    private static bool TryParseEnum<T>(string value, out T result) where T : struct, Enum
    {
        value = value?.Trim();
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numericValue) &&
            Enum.IsDefined(typeof(T), numericValue))
        {
            result = (T)Enum.ToObject(typeof(T), numericValue);
            return true;
        }

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float decimalValue) &&
            Mathf.Approximately(decimalValue, Mathf.Round(decimalValue)) &&
            Enum.IsDefined(typeof(T), (int)decimalValue))
        {
            result = (T)Enum.ToObject(typeof(T), (int)decimalValue);
            return true;
        }

        return Enum.TryParse(value, true, out result) && Enum.IsDefined(typeof(T), result);
    }

    private static bool TryParseFloat(string value, float fallback, out float result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = fallback;
            return true;
        }

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ||
               float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result);
    }

    private static bool TryParseBool(string value, bool fallback, out bool result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = fallback;
            return true;
        }

        value = value.Trim();
        if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "예")
        {
            result = true;
            return true;
        }

        if (value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "아니오")
        {
            result = false;
            return true;
        }

        result = fallback;
        return false;
    }

    private static void EnsureAssetFolder(string assetFolder)
    {
        string[] segments = assetFolder.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = $"{current}/{segments[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidCharacter, '_');
        return value;
    }

    private static List<List<string>> ReadCsv(string path)
    {
        string text = File.ReadAllText(path, Encoding.UTF8);
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];
            if (character == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if ((character == '\n' || character == '\r') && !inQuotes)
            {
                if (character == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;

                row.Add(cell.ToString());
                cell.Clear();
                if (row.Count > 1 || !string.IsNullOrWhiteSpace(row[0]))
                    rows.Add(row);
                row = new List<string>();
            }
            else
            {
                cell.Append(character);
            }
        }

        row.Add(cell.ToString());
        if (row.Count > 1 || !string.IsNullOrWhiteSpace(row[0]))
            rows.Add(row);

        return rows;
    }

    private struct DefinitionData
    {
        public string AnomalyId;
        public int Day;
        public AreaId AreaId;
        public ReportTargetId TargetId;
        public AnomalyReportType ReportType;
        public float WarningDurationSec;
        public float ActiveDurationSec;
        public bool CanEscalate;
        public bool IsEmergency;
    }
}
