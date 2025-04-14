using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

public partial class Main
{
    public IntPtr Start()
    {
        try
        {
            // get gameassembly byte array
            if (!CheckSetProcessAndValues()) return IntPtr.Zero;

            ProcessModule gameAssemblyModule = null;

            for (int i = 0; i < 50; i++)
            {
                Instance = Process.GetProcessesByName("Megabonk Demo")[0];
                try { gameAssemblyModule = UProcess.GetModule(Instance, "GameAssembly.dll"); } catch { }
                if (gameAssemblyModule != null) break;
                Thread.Sleep(100);
            }

            ulong gameAssemblyAddress = (ulong)gameAssemblyModule.BaseAddress;
            int gameAssemblySize = gameAssemblyModule.ModuleMemorySize;

            if (gameAssemblyAddress == 0 ||  gameAssemblySize == 0) return IntPtr.Zero;

            byte[] gameAssemblyBytes = UMemory.ReadMemoryBytes(Instance, gameAssemblyAddress, gameAssemblySize);
            if (gameAssemblyBytes.Length == 0) return IntPtr.Zero;

            ulong allocated = RefAllocateMemory(Instance, 0x1000);

            // 1
            {
                byte[] code = new byte[]
                {
                    0x48, 0x83, 0xEC, 0x28, // sub rsp,28
                    0x48, 0x8B, 0x49, 0x60, // mov rcx,[rcx+60]
                    0x48, 0x85, 0xC9, // test rcx,rcx
                    0x75, 0x0E, // jne address
                    0xFF, 0x25, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // jmp address
                    0x45, 0x31, 0xC0, // xor r8d,r8d
                    0xFF, 0x25, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // jmp address
                };

                ulong scan = UMemory.ScanSpecific(Instance,
                    "48 83 EC 28 48 8B 49 60 48 85 C9 74 0E 45 33 C0 B2 01", gameAssemblyAddress, gameAssemblySize);

                if (scan != 0)
                {
                    if (allocated == 0) return IntPtr.Zero;

                    UArray.Insert(code, BitConverter.GetBytes(scan + 0x1B), 19);
                    UArray.Insert(code, BitConverter.GetBytes(scan + 0x10), 36);

                    byte[] codePlus = UArray.Merge(USignature.GetBytes("48 83 05 F8 FE FF FF 01"), code);

                    RefWriteBytes(Instance, allocated + 0x100, codePlus);

                    byte[] jumpIn = UArray.Merge(USignature.GetBytes("FF 25 00 00 00 00"), BitConverter.GetBytes(allocated + 0x100));
                    RefWriteBytes(Instance, scan, jumpIn);
                }
                else
                {
                    scan = UMemory.ScanSpecific(Instance,
                        "FF 25 00 00 00 00 ?? ?? ?? ?? ?? ?? ?? ?? 33 C0 B2 01", gameAssemblyAddress, gameAssemblySize);

                    if (scan == 0) return IntPtr.Zero;

                    ulong extract = (ulong)BitConverter.ToInt64(UMemory.ReadMemoryBytes(Instance, scan + 0x6, 8), 0);
                    if (extract != 0) return (IntPtr)(extract - 0x100);
                }
            }

            // 2
            {
                byte[] code = new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x08, // mov [rsp+08],rbx
                    0x57, // push rdi
                    0x48, 0x83, 0xEC, 0x20, // sub rsp, 20
                    0x48, 0xBB, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // mov rbx value
                    0x80, 0x3B, 0x00, // cmp byte ptr [rbx], 0
                    0xFF, 0x25, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // jmp address
                };

                ulong scan = UMemory.ScanSpecific(Instance,
                    "48 89 5C 24 08 57 48 83 EC 20 80 3D ?? ?? ?? ?? 00 0F B6 FA 48 8B D9 75 1F", gameAssemblyAddress, gameAssemblySize);

                if (scan != 0)
                {
                    int relative = UMemory.ReadMemory<int>(Instance, scan + 0xC);
                    ulong cmp = scan + (ulong)relative + 0x11;

                    UArray.Insert(code, BitConverter.GetBytes(cmp), 12);
                    UArray.Insert(code, BitConverter.GetBytes(scan + 17), 29);

                    byte[] codePlus = UArray.Merge(USignature.GetBytes("48 83 05 00 FE FF FF 01"), code);

                    RefWriteBytes(Instance, allocated + 0x200, codePlus);

                    byte[] jumpIn = UArray.Merge(USignature.GetBytes("FF 25 00 00 00 00"), BitConverter.GetBytes(allocated + 0x200));
                    RefWriteBytes(Instance, scan, jumpIn);
                }
                else
                {
                    scan = UMemory.ScanSpecific(Instance,
                        "FF 25 00 00 00 00 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 00 0F B6 FA 48", gameAssemblyAddress, gameAssemblySize);

                    if (scan == 0) return IntPtr.Zero;

                    ulong extract = (ulong)BitConverter.ToInt64(UMemory.ReadMemoryBytes(Instance, scan + 0x6, 8), 0);
                    if (extract != 0) return (IntPtr)(extract - 0x200);
                }
            }

            return (IntPtr)allocated;
        }
        catch (Exception ex) { UProgram.Print(ex.Message); }
        return IntPtr.Zero;
    }

    public IntPtr CatchReg(IntPtr address, string register, int overwriteSize)
    {
        try
        {
            return CodeHK(address, overwriteSize, SaveRegBytes[register]);
        }
        catch { }
        return IntPtr.Zero;
    }

    public IntPtr CodeHK(IntPtr address, int overwriteSize, string customCode)
    {
        try
        {
            return CodeHK(address, overwriteSize, UMemory.GetByteArray(customCode));
        }
        catch { }
        return IntPtr.Zero;
    }

    public IntPtr CodeHK(IntPtr address, int overwriteSize, byte[] customCode)
    {
        try
        {
            byte[] jmpConfirmBytes = new byte[] { 0xFF, 0x25, 0x00, 0x00, 0x00, 0x00 };
            byte[] readConfirmBytes = UMemory.ReadMemoryBytes(Instance, address, 0x6);

            if (readConfirmBytes == null) return IntPtr.Zero;

            if (jmpConfirmBytes.SequenceEqual(readConfirmBytes))
            {
                ulong oldAllocated = UMemory.ReadMemory<ulong>(Instance, address + 0x6);

                if (oldAllocated == 0) return IntPtr.Zero;
                else return (IntPtr)(oldAllocated - 0x8);
            }
            else
            {
                ulong allocated = RefAllocateMemory(Instance, 0x100);
                if (allocated == 0) return IntPtr.Zero;

                byte[] stolen = UMemory.ReadMemoryBytes(Instance, address, overwriteSize);
                if (stolen == null) return IntPtr.Zero;

                byte[] e1 = customCode;
                byte[] e2 = stolen;
                byte[] e3 = { 0xFF, 0x25, 0x00, 0x00, 0x00, 0x00 };
                byte[] e4 = BitConverter.GetBytes((ulong)address + (ulong)overwriteSize);
                byte[] end = UArray.Merge(e1, e2, e3, e4);

                byte[] s1 = { 0xFF, 0x25, 0x00, 0x00, 0x00, 0x00 };
                byte[] s2 = BitConverter.GetBytes(allocated + 0x8);
                byte[] start = UArray.Merge(s1, s2);

                RefWriteBytes(Instance, allocated + 0x8, end);
                RefWriteBytes(Instance, (ulong)address, start);

                return (IntPtr)allocated;
            }
        }
        catch (Exception ex) { UProgram.Print(ex.Message); }
        return IntPtr.Zero;
    }

    public IntPtr ScanSpecific(string signature, IntPtr startAddress, int size)
    {
        try
        {
            if (CheckSetProcessAndValues())
                return (IntPtr)UMemory.ScanSpecific(Instance, signature, (ulong)startAddress, size);
        }
        catch { }
        return IntPtr.Zero;
    }

    public IntPtr ScanSingle(string signature)
    {
        try
        {
            if (CheckSetProcessAndValues())
                return (IntPtr)UMemory.ScanSingle(Instance, signature);
        }
        catch { }
        return IntPtr.Zero;
    }

    public IntPtr ScanRel(int offset, string signature)
    {
        try
        {
            if (CheckSetProcessAndValues())
                return (IntPtr)UMemory.ScanRel(Instance, offset, signature);
        }
        catch { }
        return IntPtr.Zero;
    }

    public string GetCategoryName()
    {
        try
        {
            string category = UReflection.GetValue(UReflection.GetValue(Application.OpenForms["TimerForm"],
            "<CurrentState>k__BackingField",
            "<Run>k__BackingField",
            "categoryName")).ToString();

            return category ?? "";
        }
        catch { }
        return "";
    }

    readonly Dictionary<string, byte[]> SaveRegBytes = new Dictionary<string, byte[]>
    {
        { "rax", new byte[] { 0x48, 0x89, 0x05, 0xF1, 0xFF, 0xFF, 0xFF } },
        { "rbx", new byte[] { 0x48, 0x89, 0x1D, 0xF1, 0xFF, 0xFF, 0xFF } },
        { "rcx", new byte[] { 0x48, 0x89, 0x15, 0xF1, 0xFF, 0xFF, 0xFF } },
        { "rdx", new byte[] { 0x48, 0x89, 0x15, 0xF1, 0xFF, 0xFF, 0xFF } },
        { "rbp", new byte[] { 0x48, 0x89, 0x2D, 0xF1, 0xFF, 0xFF, 0xFF } },
        { "rsp", new byte[] { 0x48, 0x89, 0x25, 0xF1, 0xFF, 0xFF, 0xFF } },
        { "rsi", new byte[] { 0x48, 0x89, 0x35, 0xF1, 0xFF, 0xFF, 0xFF } },
        { "rdi", new byte[] { 0x48, 0x89, 0x3D, 0xF1, 0xFF, 0xFF, 0xFF } },
        { "r8",  new byte[] { 0x4C, 0x89, 0x05, 0xF1, 0xFF, 0xFF, 0xFF } },
        { "r9",  new byte[] { 0x4C, 0x89, 0x0D, 0xF1, 0xFF, 0xFF, 0xFF } },
        { "r10", new byte[] { 0x4C, 0x89, 0x15, 0xF1, 0xFF, 0xFF, 0xFF } },
        { "r11", new byte[] { 0x4C, 0x89, 0x1D, 0xF1, 0xFF, 0xFF, 0xFF } },
        { "r12", new byte[] { 0x4C, 0x89, 0x25, 0xF1, 0xFF, 0xFF, 0xFF } },
        { "r13", new byte[] { 0x4C, 0x89, 0x2D, 0xF1, 0xFF, 0xFF, 0xFF } },
        { "r14", new byte[] { 0x4C, 0x89, 0x35, 0xF1, 0xFF, 0xFF, 0xFF } },
        { "r15", new byte[] { 0x4C, 0x89, 0x3D, 0xF1, 0xFF, 0xFF, 0xFF } },
    };
}
