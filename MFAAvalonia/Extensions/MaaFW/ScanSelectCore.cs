using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MFAAvalonia.Extensions.MaaFW;

/// <summary>
/// scan_select 核心逻辑：处理 pipeline_override 占位符替换
/// </summary>
public static class ScanSelectCore
{
    /// <summary>
    /// 为 scan_select 类型生成处理后的 pipeline override
    /// 递归遍历 pipeline_override，对所有 attach.option_name 键赋值选中值
    /// </summary>
    public static string GeneratePipeline(MaaInterface.MaaInterfaceOption option, string selectedValue)
    {
        if (option.PipelineOverride == null)
            return "{}";

        var cloned = JsonConvert.DeserializeObject<JObject>(JsonConvert.SerializeObject(option.PipelineOverride));
        if (cloned == null)
            return "{}";

        ReplacePlaceholders(cloned, option.Name ?? "", selectedValue);
        return JsonConvert.SerializeObject(cloned, Formatting.Indented);
    }

    private static void ReplacePlaceholders(JToken token, string optionKey, string selectedValue)
    {
        if (token is JObject obj)
        {
            foreach (var prop in obj.Properties().ToArray())
            {
                if (prop.Value is JObject || prop.Value is JArray)
                {
                    ReplacePlaceholders(prop.Value, optionKey, selectedValue);
                }
                else if (prop.Value.Type == JTokenType.String)
                {
                    var value = prop.Value.ToString();
                    if (value.Contains($"{{{optionKey}}}"))
                    {
                        prop.Value = value.Replace($"{{{optionKey}}}", selectedValue);
                    }
                }
            }
        }
        else if (token is JArray arr)
        {
            foreach (var item in arr)
            {
                ReplacePlaceholders(item, optionKey, selectedValue);
            }
        }
    }
}
