using System.Security.Cryptography;
using System.Text;

namespace NipahFirebase.FirebaseCore;

public struct DBPath
{
    const string DeepNotation = "Deep/";
    public static string FastGetDeepPath(string from)
    {
        var path = new DBPath(from);

        bool hasDeep = path.GetFirstPathName().Contains("Deep");

        if (hasDeep is false)
        {
            Span<byte> pathB = stackalloc byte[Encoding.UTF8.GetByteCount(from)];
            Encoding.UTF8.GetBytes(from, pathB);
            Span<byte> hash = stackalloc byte[16];
            MD5.HashData(pathB, hash);
            path.Insert(0, Convert.ToHexString(hash[..(16 / 4)]) + '-');

            path.Insert(0, DeepNotation);
        }

        return path.ToString();
    }
    public static string FastGetDeepPath<T>(string from)
    {
        var path = new DBPath(from);

        bool hasDeep = path.GetFirstPathName().Contains("Deep");

        if (hasDeep is false)
        {
            string fdeep = $"{DeepNotation}{GetRealTypeName(typeof(T))}/";

            /*Span<byte> pathB = stackalloc byte[Encoding.UTF8.GetByteCount(from)];
            Encoding.UTF8.GetBytes(from, pathB);
            Span<byte> hash = stackalloc byte[16];
            MD5.HashData(pathB, hash);
            path.Insert(0, Convert.ToHexString(hash[..(16 / 4)]) + '-');*/

            path.Insert(0, fdeep);
        }

        return path.ToString();
    }
    static Dictionary<Type, string> cachedNames = new Dictionary<Type, string>(32);
    static string GetRealTypeName(Type t)
    {
        if (!t.IsGenericType)
            return t.Name;

        if (cachedNames.TryGetValue(t, out string? name))
            return name;

        StringBuilder sb = new StringBuilder();
        sb.Append(t.Name.AsSpan(0, t.Name.IndexOf('`')));
        sb.Append('<');
        bool appendComma = false;
        foreach (Type arg in t.GetGenericArguments())
        {
            if (appendComma) sb.Append(',');
            sb.Append(GetRealTypeName(arg));
            appendComma = true;
        }
        sb.Append('>');

        name = sb.ToString();
        cachedNames[t] = name;
        return name;
    }

    public readonly StringBuilder Path;

    public DBPath New => new DBPath("");

    /// <summary>
    /// The last part of the path (like: for A/B/.../C is C, and so on...)
    /// </summary>
    /// <returns></returns>
    public string GetCurrentPathName()
    {
        string cur = "";
        int offset = 1;
    begin:
        if (Path[^offset] != '/')
        {
            cur += Path[^offset];
            offset++;
            goto begin;
        }
        return cur;
    }
    public string GetFirstPathName()
    {
        int length = Path.Length;
        string cur = "";
        int offset = 0;
    begin:
        if (offset < length && Path[offset] is char c and not '/')
        {
            cur += c;
            offset++;
            goto begin;
        }
        return cur;
    }

    public DBPath MoveUp()
    {
    begin:
        if (Path[^1] != '/')
        {
            Path.Remove(Path.Length - 1, 1);
            goto begin;
        }
        return this;
    }

    public DBPath MoveDown(string to)
    {
        if (to.Contains('.')) throw new Exception("Cannot put any '.' in path");

        Path.Append('/');
        Path.Append(to);
        return this;
    }

    public DBPath Insert(int index, string content)
    {
        if (content.Contains('.')) throw new Exception("Cannot put any '.' in path");
        Path.Insert(index, content);
        return this;
    }

    public override string ToString()
    {
        return Path.ToString();
    }

    public DBPath(string from) => Path = new StringBuilder(from);
}
public readonly struct ImmutableDBPath
{
    public readonly string Path;

    public static implicit operator string (ImmutableDBPath path) => path.Path;

    public override string ToString() => Path;

    public string GetCurrentPathName()
    {
        string cur = "";
        int offset = 1;
    begin:
        if (Path[^offset] != '/')
        {
            cur += Path[^offset];
            offset++;
            goto begin;
        }
        return cur;
    }
    public string GetFirstPathName()
    {
        int length = Path.Length;
        string cur = "";
        int offset = 0;
    begin:
        if (offset < length && Path[offset] is char c and not '/')
        {
            cur += c;
            offset++;
            goto begin;
        }
        return cur;
    }

    public ImmutableDBPath MoveUp()
    {
        string fpath = Path;
    begin:
        if (fpath[^1] != '/')
        {
            fpath = fpath.Remove(fpath.Length - 1, 1);
            goto begin;
        }
        return new ImmutableDBPath(fpath);
    }

    public ImmutableDBPath MoveDown(string to)
    {
        if (to.Contains('.')) throw new Exception("Cannot put any '.' in path");

        return new ImmutableDBPath(Path + '/' + to);
    }

    public ImmutableDBPath Insert(int index, string content)
    {
        if (content.Contains('.')) throw new Exception("Cannot put any '.' in path");
        return new ImmutableDBPath(Path.Insert(index, content));
    }

    public ImmutableDBPath(string path) => Path = path;
}