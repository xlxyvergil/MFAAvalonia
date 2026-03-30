using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace MFAAvalonia.Extensions.MaaFW;

/// <summary>
/// scan_select 选项类型的共享工具类
/// 提供目录扫描、glob 模式匹配和 pipeline_override 处理功能
/// </summary>
public static class ScanSelectUtils
{
    /// <summary>
    /// 扫描目录并根据 filter 更新 interfaceOption.Cases
    /// </summary>
    public static void ScanDirectory(
        MaaInterface.MaaInterfaceOption interfaceOption,
        string resourceBase)
    {
        if (string.IsNullOrEmpty(interfaceOption.ScanDir) || string.IsNullOrEmpty(interfaceOption.ScanFilter))
            return;

        try
        {
            var scanPath = interfaceOption.ScanDir.Replace("{PROJECT_DIR}", resourceBase);
            scanPath = Path.GetFullPath(scanPath);

            if (!Directory.Exists(scanPath))
            {
                LoggerHelper.Warning($"Scan directory does not exist: {scanPath}");
                interfaceOption.Cases ??= new List<MaaInterface.MaaInterfaceOptionCase>();
                return;
            }

            var files = EnumerateFilesWithGlob(scanPath, interfaceOption.ScanFilter);
            var newCases = new List<MaaInterface.MaaInterfaceOptionCase>();

            foreach (var file in files.OrderBy(f => f))
            {
                var fileName = Path.GetFileName(file);
                var caseItem = new MaaInterface.MaaInterfaceOptionCase
                {
                    Name = fileName,
                    Label = fileName
                };
                newCases.Add(caseItem);
            }

            interfaceOption.Cases = newCases;

            // 初始化所有 case 的显示名称
            foreach (var caseItem in interfaceOption.Cases)
            {
                caseItem.InitializeDisplayName();
            }
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"Failed to scan directory: {ex.Message}");
            interfaceOption.Cases ??= new List<MaaInterface.MaaInterfaceOptionCase>();
        }
    }

    /// <summary>
    /// 使用 glob 模式枚举文件
    /// </summary>
    public static List<string> EnumerateFilesWithGlob(string basePath, string pattern)
    {
        var results = new List<string>();

        // 处理 ** 递归模式
        if (pattern.Contains("**/"))
        {
            var parts = pattern.Split(new[] { "**/" }, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                var baseDir = parts[0];
                var filePattern = parts[1];

                var searchPath = string.IsNullOrEmpty(baseDir)
                    ? basePath
                    : Path.Combine(basePath, baseDir);

                if (Directory.Exists(searchPath))
                {
                    // 递归搜索所有子目录
                    var allFiles = Directory.EnumerateFiles(searchPath, "*", SearchOption.AllDirectories);
                    foreach (var file in allFiles)
                    {
                        var fileName = Path.GetFileName(file);
                        if (MatchesGlobPattern(fileName, filePattern))
                        {
                            results.Add(file);
                        }
                    }
                }
            }
        }
        else if (pattern.Contains("*"))
        {
            // 处理包含通配符的目录路径，如 "settings/*.json"
            var dirPart = Path.GetDirectoryName(pattern) ?? "";
            var filePart = Path.GetFileName(pattern);

            if (string.IsNullOrEmpty(dirPart))
            {
                // 当前目录
                if (Directory.Exists(basePath))
                {
                    results.AddRange(Directory.EnumerateFiles(basePath, filePart));
                }
            }
            else
            {
                // 子目录
                var searchPath = Path.Combine(basePath, dirPart);
                if (Directory.Exists(searchPath))
                {
                    results.AddRange(Directory.EnumerateFiles(searchPath, filePart));
                }
            }
        }
        else
        {
            // 简单模式，直接搜索
            if (Directory.Exists(basePath))
            {
                results.AddRange(Directory.EnumerateFiles(basePath, pattern));
            }
        }

        return results;
    }

    /// <summary>
    /// 检查文件名是否匹配 glob 模式
    /// </summary>
    public static bool MatchesGlobPattern(string fileName, string pattern)
    {
        // 将 glob 模式转换为正则表达式
        var regexPattern = pattern
            .Replace(".", "\\.")
            .Replace("*", ".*")
            .Replace("?", ".");

        regexPattern = "^" + regexPattern + "$";

        try
        {
            return Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase);
        }
        catch
        {
            // 如果正则表达式无效，使用简单的字符串比较
            return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 处理 scan_select 的 pipeline_override，递归查找并更新 attach.option_name
    /// 支持递归处理对象和数组
    /// </summary>
    public static void ProcessScanSelectPipeline(
        Dictionary<string, Dictionary<string, JToken>> pipelineOverride,
        string optionName,
        string selectedValue)
    {
        foreach (var preset in pipelineOverride.Values)
        {
            ProcessScanSelectPreset(preset, optionName, selectedValue);
        }
    }

    /// <summary>
    /// 处理单个 preset 对象，递归查找并更新 attach.option_name
    /// </summary>
    private static void ProcessScanSelectPreset(
        Dictionary<string, JToken> preset,
        string optionName,
        string selectedValue)
    {
        foreach (var key in preset.Keys.ToList())
        {
            var jToken = preset[key];
            
            if (key == "attach" && jToken.Type == JTokenType.Object)
            {
                var attachObj = (JObject)jToken;
                if (attachObj.ContainsKey(optionName))
                {
                    attachObj[optionName] = JToken.FromObject(selectedValue);
                }
            }
            else if (jToken.Type == JTokenType.Object)
            {
                var nestedObj = (JObject)jToken;
                var nestedDict = nestedObj.ToObject<Dictionary<string, JToken>>();
                if (nestedDict != null)
                {
                    ProcessScanSelectPreset(nestedDict, optionName, selectedValue);
                    // 更新原对象
                    foreach (var nestedKey in nestedDict.Keys.ToList())
                    {
                        nestedObj[nestedKey] = nestedDict[nestedKey];
                    }
                }
            }
            else if (jToken.Type == JTokenType.Array)
            {
                // 处理数组中的对象
                var array = (JArray)jToken;
                for (int i = 0; i < array.Count; i++)
                {
                    var item = array[i];
                    if (item.Type == JTokenType.Object)
                    {
                        var itemObj = (JObject)item;
                        var itemDict = itemObj.ToObject<Dictionary<string, JToken>>();
                        if (itemDict != null)
                        {
                            ProcessScanSelectPreset(itemDict, optionName, selectedValue);
                            // 更新原对象
                            foreach (var itemKey in itemDict.Keys.ToList())
                            {
                                itemObj[itemKey] = itemDict[itemKey];
                            }
                        }
                    }
                }
            }
        }
    }
}
