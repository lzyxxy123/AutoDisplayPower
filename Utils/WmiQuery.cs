using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AutoDisplayPower.Utils;

/// <summary>WMI 查询结果。</summary>
public sealed class WmiQueryResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string Method { get; set; } = "";
    public List<string> Values { get; } = new();
}

/// <summary>
/// 通过 Windows 自带的 WMI 脚本 COM（WbemScripting.SWbemLocator，无需第三方包）查询 WMI。
/// 说明：本项目构建环境无法访问 nuget.org，无法引用 System.Management，故改用晚绑定 COM。
/// 取条目优先用 _NewEnum 顺序枚举（ExecQuery 返回的 SWbemObjectSet 不支持 Item(index) 随机访问）。
/// </summary>
public static class WmiQuery
{
    private const BindingFlags InvokeMethod = BindingFlags.InvokeMethod;
    private const BindingFlags GetProperty = BindingFlags.GetProperty;

    /// <summary>在指定 WMI 命名空间执行 WQL，收集某个属性的字符串值。</summary>
    public static WmiQueryResult QueryProperty(string wmiNamespace, string wql, string propertyName)
    {
        var result = new WmiQueryResult();
        object? locator = null, services = null, set = null;
        try
        {
            Type? locatorType = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
            if (locatorType is null)
            {
                result.Error = "WMI COM (WbemScripting.SWbemLocator) 不可用";
                return result;
            }

            locator = Activator.CreateInstance(locatorType);
            if (locator is null)
            {
                result.Error = "无法创建 WMI Locator";
                return result;
            }

            services = locatorType.InvokeMember("ConnectServer", InvokeMethod, null, locator,
                new object?[] { ".", wmiNamespace, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing });
            if (services is null)
            {
                result.Error = $"连接 WMI 命名空间 {wmiNamespace} 失败";
                return result;
            }

            set = services.GetType().InvokeMember("ExecQuery", InvokeMethod, null, services, new object?[] { wql });
            if (set is null)
            {
                result.Error = "ExecQuery 返回空";
                return result;
            }

            // 方式一：_NewEnum 顺序枚举（推荐）
            if (TryEnumerateViaNewEnum(set, propertyName, result.Values, out string? enumError))
            {
                result.Ok = true;
                result.Method = "_NewEnum";
                return result;
            }

            // 方式二：Count + Item(index) 回退
            result.Values.Clear();
            if (TryEnumerateViaItem(set, propertyName, result.Values, out string? itemError))
            {
                result.Ok = true;
                result.Method = "Item";
                return result;
            }

            result.Error = $"_NewEnum 失败({enumError})；Item 失败({itemError})";
            return result;
        }
        catch (Exception ex)
        {
            result.Error = InnerMessage(ex);
            return result;
        }
        finally
        {
            Release(set);
            Release(services);
            Release(locator);
        }
    }

    private static bool TryEnumerateViaNewEnum(object set, string propertyName, List<string> values, out string? error)
    {
        error = null;
        try
        {
            object? enumVar = set.GetType().InvokeMember("_NewEnum", GetProperty, null, set, null);
            if (enumVar is not IEnumerator enumerator)
            {
                error = "未取得 IEnumerator";
                return false;
            }

            while (enumerator.MoveNext())
            {
                object? item = enumerator.Current;
                if (item is null) continue;
                object? value = item.GetType().InvokeMember(propertyName, GetProperty, null, item, null);
                if (value is not null) values.Add(value.ToString() ?? string.Empty);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = InnerMessage(ex);
            return false;
        }
    }

    private static bool TryEnumerateViaItem(object set, string propertyName, List<string> values, out string? error)
    {
        error = null;
        try
        {
            int count = Convert.ToInt32(set.GetType().InvokeMember("Count", GetProperty, null, set, null));
            for (int i = 1; i <= count; i++)
            {
                object? item = set.GetType().InvokeMember("Item", InvokeMethod, null, set, new object?[] { i });
                if (item is null) continue;
                object? value = item.GetType().InvokeMember(propertyName, GetProperty, null, item, null);
                if (value is not null) values.Add(value.ToString() ?? string.Empty);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = InnerMessage(ex);
            return false;
        }
    }

    private static string InnerMessage(Exception ex)
    {
        Exception inner = ex;
        while (inner.InnerException is not null) inner = inner.InnerException;
        return inner.Message.Trim();
    }

    private static void Release(object? comObject)
    {
        try
        {
            if (comObject is not null && Marshal.IsComObject(comObject)) Marshal.ReleaseComObject(comObject);
        }
        catch
        {
            // 忽略释放失败
        }
    }
}
