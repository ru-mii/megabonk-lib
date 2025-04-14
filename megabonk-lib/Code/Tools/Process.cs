using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal class UProcess
{
    public static ulong GetModBase(Process process, string name = null)
    {
        if (name == null) return (ulong)process.MainModule.BaseAddress;
        else if (!name.EndsWith(".dll")) name += ".dll";

        return (ulong)GetModule(process, name).BaseAddress;
    }

    public static ProcessModule GetModule(Process process, string name = null)
    {
        if (name == null)
        {
            return process.MainModule;
        }
        else
        {
            foreach (ProcessModule module in process.Modules)
            {
                if (module.ModuleName.ToLower() == name.ToLower())
                {
                    return module;
                }
            }
        }
        return null;
    }

    internal static ulong GetTime(Process process)
    {
        ulong startTime = (ulong)((DateTimeOffset)process.StartTime).ToUnixTimeMilliseconds();
        ulong currentTime = (ulong)((DateTimeOffset)DateTime.Now).ToUnixTimeMilliseconds();
        return currentTime - startTime;
    }

    internal static bool IsAlive(Process process)
    {
        try
        {
            return
            process != null &&
            !process.HasExited &&
            process.MainModule != null;
            //Process.GetProcessById(process.Id) != null;
        }
        catch { }
        return false;
    }
}