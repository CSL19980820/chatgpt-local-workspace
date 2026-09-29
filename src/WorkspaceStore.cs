using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

// One writer per local store; state is encrypted for the current Windows user.
static class WorkspaceStore
{
    static readonly object Gate = new object();
    static FileStream lease;
    static string root;
    static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 128 * 1024 * 1024, RecursionLimit = 100 }; }
    public static string Root { get { Ensure(); return root; } }
    static void Ensure()
    {
        lock (Gate) {
            if (lease != null) return;
            string configured = Environment.GetEnvironmentVariable("WORKSPACE_STATE_DIR");
            root = Path.GetFullPath(string.IsNullOrEmpty(configured) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalWorkspacePlugin", "state-v1") : configured);
            Directory.CreateDirectory(root);
            try { lease = new FileStream(Path.Combine(root, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { throw new IOException("Workspace state is already in use. Close the other MCP runtime or use a separate WORKSPACE_STATE_DIR."); }
        }
    }
    public static T Load<T>(string name, Func<T> empty)
    {
        lock (Gate) {
            Ensure(); string path = Path.Combine(root, name + ".bin");
            if (!File.Exists(path)) return empty();
            try {
                byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Encoding.UTF8.GetBytes("LocalWorkspace/state-v1/" + name), DataProtectionScope.CurrentUser);
                try { return Serializer().Deserialize<T>(Encoding.UTF8.GetString(plain)); }
                finally { Array.Clear(plain, 0, plain.Length); }
            } catch (Exception ex) { throw new IOException("Cannot read saved workspace " + name + "; original state was preserved. " + ex.GetType().Name); }
        }
    }
    public static void Save(string name, object state)
    {
        lock (Gate) {
            Ensure(); byte[] plain = Encoding.UTF8.GetBytes(Serializer().Serialize(state));
            if (plain.Length > 96 * 1024 * 1024) throw new IOException("Saved workspace data exceeds 96 MiB; reduce retained history.");
            byte[] encrypted;
            try { encrypted = ProtectedData.Protect(plain, Encoding.UTF8.GetBytes("LocalWorkspace/state-v1/" + name), DataProtectionScope.CurrentUser); }
            finally { Array.Clear(plain, 0, plain.Length); }
            AtomicWrite(Path.Combine(root, name + ".bin"), encrypted);
        }
    }
    public static void AtomicWrite(string path, byte[] data)
    {
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(data, 0, data.Length); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
