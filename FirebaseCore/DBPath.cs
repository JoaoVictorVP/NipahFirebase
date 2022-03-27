using System.Security.Cryptography;
using System.Text;

namespace NipahFirebase.FirebaseCore;

public struct DBPath
{
    const string DeepNotation = "Deep/";
    public static string FastGetDeepPath(string from)
    {
        var path = new DBPath(from);

        Span<byte> pathB = stackalloc byte[Encoding.UTF8.GetByteCount(from)];
        Encoding.UTF8.GetBytes(from, pathB);
        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(pathB, hash);
        path.Insert(0, Convert.ToHexString(hash[..(16/4)]) + '-');

        if(path.GetFirstPathName().Contains("Deep") is false)
            path.Insert(0, DeepNotation);

        return path.ToString();
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
        string cur = "";
        int offset = 0;
    begin:
        if (Path[offset] is char c and not '/')
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