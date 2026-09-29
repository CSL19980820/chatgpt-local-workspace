using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

static class WorkspaceCredentials
{
    const string Target = "LocalWorkspacePlugin/tunnel-api-key";
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct Credential {
        public uint Flags, Type;
        public string TargetName, Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint Size;
        public IntPtr Blob;
        public uint Persist, AttributeCount;
        public IntPtr Attributes;
        public string Alias, UserName;
    }
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool CredWriteW(ref Credential value, uint flags);
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool CredReadW(string target, uint type, uint flags, out IntPtr result);
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool CredDeleteW(string target, uint type, uint flags);
    [DllImport("advapi32.dll")] static extern void CredFree(IntPtr value);
    public static string Read() { return ReadTarget(Target); }
    internal static string ReadTarget(string target)
    {
        IntPtr pointer;
        if (!CredReadW(target, 1, 0, out pointer)) { int error=Marshal.GetLastWin32Error(); if(error==1168)return ""; throw new Win32Exception(error,"无法读取 Windows 凭据管理器。"); }
        try { var value=(Credential)Marshal.PtrToStructure(pointer,typeof(Credential)); return value.Size==0?"":Marshal.PtrToStringUni(value.Blob,(int)value.Size/2); }
        finally { CredFree(pointer); }
    }
    public static void Save(string value) { SaveTarget(Target,value); }
    internal static void SaveTarget(string target,string value)
    {
        if(string.IsNullOrEmpty(value)){if(!CredDeleteW(target,1,0)&&Marshal.GetLastWin32Error()!=1168)throw new Win32Exception(Marshal.GetLastWin32Error());return;}
        byte[] bytes=Encoding.Unicode.GetBytes(value);IntPtr blob=Marshal.AllocHGlobal(bytes.Length);
        try { Marshal.Copy(bytes,0,blob,bytes.Length);var credential=new Credential{Type=1,TargetName=target,Size=(uint)bytes.Length,Blob=blob,Persist=2,UserName=Environment.UserName};if(!CredWriteW(ref credential,0))throw new Win32Exception(Marshal.GetLastWin32Error(),"无法保存 Windows 凭据。"); }
        finally { Array.Clear(bytes,0,bytes.Length);Marshal.Copy(bytes,0,blob,bytes.Length);Marshal.FreeHGlobal(blob); }
    }
}
