using SuperIronRuby.Runtime;

namespace SuperIronRuby.Builtins;

/// <summary>Minimal File/Dir/ENV support (task B11 core).</summary>
[RubyClass("File")]
public static class FileOps
{
    private static string P(object? o) => o is MutableString m ? m.Value : o?.ToString() ?? "";

    [RubyMethod("read", Kind = RubyMethodKind.Static)]
    public static object? Read(RubyContext c, object? self, object?[] a, RubyProc? b)
    {
        try { return new MutableString(File.ReadAllText(P(a[0]))); }
        catch (FileNotFoundException) { throw Enoent(c, P(a[0])); }
        catch (DirectoryNotFoundException) { throw Enoent(c, P(a[0])); }
    }

    [RubyMethod("write", Kind = RubyMethodKind.Static)]
    public static object? Write(RubyContext c, object? self, object?[] a, RubyProc? b)
    {
        var text = P(a[1]);
        File.WriteAllText(P(a[0]), text);
        return (long)System.Text.Encoding.UTF8.GetByteCount(text);
    }

    [RubyMethod("readlines", Kind = RubyMethodKind.Static)]
    public static object? ReadLines(RubyContext c, object? self, object?[] a, RubyProc? b)
    {
        var arr = new RubyArray();
        foreach (var line in File.ReadAllLines(P(a[0]))) arr.Add(new MutableString(line + "\n"));
        return arr;
    }

    [RubyMethod("exist?", Kind = RubyMethodKind.Static)]
    [RubyMethod("exists?", Kind = RubyMethodKind.Static)]
    public static object? Exist(RubyContext c, object? self, object?[] a, RubyProc? b)
        => File.Exists(P(a[0])) || Directory.Exists(P(a[0]));

    [RubyMethod("file?", Kind = RubyMethodKind.Static)]
    public static object? IsFile(RubyContext c, object? self, object?[] a, RubyProc? b) => File.Exists(P(a[0]));

    [RubyMethod("directory?", Kind = RubyMethodKind.Static)]
    public static object? IsDir(RubyContext c, object? self, object?[] a, RubyProc? b) => Directory.Exists(P(a[0]));

    [RubyMethod("basename", Kind = RubyMethodKind.Static)]
    public static object? Basename(RubyContext c, object? self, object?[] a, RubyProc? b)
    {
        var name = Path.GetFileName(P(a[0]));
        if (a.Length > 1) { var ext = P(a[1]); if (ext == ".*") name = Path.GetFileNameWithoutExtension(name); else if (name.EndsWith(ext)) name = name[..^ext.Length]; }
        return new MutableString(name);
    }

    [RubyMethod("dirname", Kind = RubyMethodKind.Static)]
    public static object? Dirname(RubyContext c, object? self, object?[] a, RubyProc? b)
    {
        var d = Path.GetDirectoryName(P(a[0]));
        return new MutableString(string.IsNullOrEmpty(d) ? "." : d);
    }

    [RubyMethod("extname", Kind = RubyMethodKind.Static)]
    public static object? Extname(RubyContext c, object? self, object?[] a, RubyProc? b)
        => new MutableString(Path.GetExtension(P(a[0])));

    [RubyMethod("join", Kind = RubyMethodKind.Static)]
    public static object? Join(RubyContext c, object? self, object?[] a, RubyProc? b)
        => new MutableString(string.Join("/", a.Select(P)));

    [RubyMethod("expand_path", Kind = RubyMethodKind.Static)]
    public static object? ExpandPath(RubyContext c, object? self, object?[] a, RubyProc? b)
    {
        var path = P(a[0]);
        if (a.Length > 1) path = Path.Combine(P(a[1]), path);
        return new MutableString(Path.GetFullPath(path));
    }

    [RubyMethod("delete", Kind = RubyMethodKind.Static)]
    [RubyMethod("unlink", Kind = RubyMethodKind.Static)]
    public static object? Delete(RubyContext c, object? self, object?[] a, RubyProc? b)
    {
        foreach (var x in a) File.Delete(P(x));
        return (long)a.Length;
    }

    private static Exception Enoent(RubyContext c, string path)
    {
        c.ObjectClass.TryGetConstant("RuntimeError", out var _);
        return c.RaiseError(c.IOErrorClass, $"No such file or directory @ rb_sysopen - {path}");
    }
}

[RubyClass("Dir")]
public static class DirOps
{
    private static string P(object? o) => o is MutableString m ? m.Value : o?.ToString() ?? "";

    [RubyMethod("pwd", Kind = RubyMethodKind.Static)]
    [RubyMethod("getwd", Kind = RubyMethodKind.Static)]
    public static object? Pwd(RubyContext c, object? self, object?[] a, RubyProc? b)
        => new MutableString(Directory.GetCurrentDirectory());

    [RubyMethod("exist?", Kind = RubyMethodKind.Static)]
    public static object? Exist(RubyContext c, object? self, object?[] a, RubyProc? b) => Directory.Exists(P(a[0]));

    [RubyMethod("mkdir", Kind = RubyMethodKind.Static)]
    public static object? Mkdir(RubyContext c, object? self, object?[] a, RubyProc? b)
    {
        Directory.CreateDirectory(P(a[0]));
        return 0L;
    }

    [RubyMethod("entries", Kind = RubyMethodKind.Static)]
    public static object? Entries(RubyContext c, object? self, object?[] a, RubyProc? b)
    {
        var arr = new RubyArray { new MutableString("."), new MutableString("..") };
        foreach (var e in Directory.GetFileSystemEntries(P(a[0]))) arr.Add(new MutableString(Path.GetFileName(e)));
        return arr;
    }

    [RubyMethod("home", Kind = RubyMethodKind.Static)]
    public static object? Home(RubyContext c, object? self, object?[] a, RubyProc? b)
        => new MutableString(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
}
