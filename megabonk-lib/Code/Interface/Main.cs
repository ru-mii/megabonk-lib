using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

public partial class Main : UShared
{
    public Main()
    {
        try
        {
            CheckSetProcessAndValues();
            Vars.Megabonk = this;

            Assembly liveSplitAssembly = Assembly.Load("LiveSplit.Core");
            Type extensionMethodsType = liveSplitAssembly.GetType("LiveSplit.ComponentUtil.ExtensionMethods");

            _RefAllocateMemory = extensionMethodsType.GetMethod("AllocateMemory", new Type[] { typeof(Process), typeof(int) });
            _RefWriteBytes = extensionMethodsType.GetMethod("WriteBytes", new Type[] { typeof(Process), typeof(IntPtr), typeof(byte[]) });
            _RefCreateThread = extensionMethodsType.GetMethod("CreateThread", new Type[] { typeof(Process), typeof(IntPtr) });
        }
        catch { }
    }
}