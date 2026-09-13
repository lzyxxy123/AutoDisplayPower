using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AutoDisplayPower.Utils;

/// <summary>WMI 查询结果。</summary>
public sealed class WmiQueryResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public List<string> Values { get; } = new();
}

/// <summary>
/// 通过 Windows 自带的 WMI 脚本 COM（WbemScripting.SWbemLocator，无需第三方包）查询 WMI。
/// 说明：本项目构建环境无法访问 nuget.org，无法引用 System.Management，故改用晚绑定 COM。
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

            int count = Convert.ToInt32(set.GetType().InvokeMember("Count", GetProperty, null, set, null));
            for (int i = 1; i <= count; i++)
            {
                object? item = set.GetType().InvokeMember("Item", InvokeMethod, null, set, new object?[] { i });
                if (item is null) continue;
                object? value = item.GetType().InvokeMember(propertyName, GetProperty, null, item, null);
                if (value is not null) result.Values.Add(value.ToString() ?? string.Empty);
            }

            result.Ok = true;
            return result;
        }
        catch (Exception ex)
        {
            // 反射调用会包一层 TargetInvocationException，取最内层原因（如“拒绝访问”）
            Exception inner = ex;
            while (inner.InnerException is not null) inner = inner.InnerException;
            result.Error = inner.Message;
            return result;
        }
        finally
        {
            Release(set);
            Release(services);
            Release(locator);
        }
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
