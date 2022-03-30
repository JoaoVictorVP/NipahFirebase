using Firebase.Database;
using Firebase.Database.Query;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Security.Cryptography;
using static NipahFirebase.FirebaseCore.Database;

namespace NipahFirebase.FirebaseCore;

public static class Database
{
    public static FirebaseClient Client;

    public static async Task Patch(string json, string path)
    {
        await Client.Child(path).PatchAsync(json);
    }
    public static async Task<string> Merge(string json, string path)
    {
        var child = Client.Child(path);
        return await child.BuildUrlAsync();
    }

    public static async Task<T> Get<T>(string path)
    {
        return await Client.Child(path).OnceSingleAsync<T>();
    }
    public static async Task<T> GetAndOrderByChild<T>(string path, string child)
    {
        return await Client.Child(path).OrderBy(child).OnceSingleAsync<T>();
    }

    public static async Task<DatabaseEnumerable<T>> GetAll<T>(string path)
    {
        var results = await Client.Child(path).OnceAsync<T>();

        return new DatabaseEnumerable<T>(new 
            (results));
    }

    public static async Task Post<T>(T value, string path)
    {
        await Client.Child(path).PostAsync(value);
    }

    public static async Task Set<T>(T value, string path)
    {
        await Client.Child(path).PutAsync(value);
    }

    public static async Task Delete(string path)
    {
        await Client.Child(path).DeleteAsync();
    }

    public static void Initialize()
{
        if (Client is not null)
            throw new Exception("Cannot initialize Database more than once");

        Client = new FirebaseClient(Shared.DatabaseUrl, new FirebaseOptions
        {
            AuthTokenAsyncFactory = () => Task.FromResult(Auth.FirebaseToken)
        });
    }

    public struct DatabaseEnumerable<T>
    {
        DatabaseEnumerator<T> enumerator;
        public DatabaseEnumerator<T> GetEnumerator() => enumerator;

        public DatabaseEnumerable(DatabaseEnumerator<T> enumerator) => this.enumerator = enumerator;
    }
    public struct DatabaseEnumerator<T> : IEnumerator<(string key, T value)>
    {
        readonly IEnumerator<FirebaseObject<T>>? enumerator;
        //IReadOnlyCollection<FirebaseObject<T>> results;
        //int index;
        //T? current;
        public (string key, T value) Current => enumerator is null ? default! : (enumerator.Current.Key, enumerator.Current.Object);

        object? IEnumerator.Current => enumerator?.Current;

        public void Dispose() => enumerator?.Dispose();

        public bool MoveNext() => enumerator?.MoveNext() ?? false;

        public void Reset() => enumerator?.Reset();
        
        public DatabaseEnumerator(IReadOnlyCollection<FirebaseObject<T>> results)
        {
            if (results is null or { Count: 0 })
                enumerator = null;
            else
                enumerator = results.GetEnumerator();
        }
    }
}
public struct Patcher
{
    public static Patcher operator ^ (Patcher from, Patcher to)
    {
        return from.values is not null ? from : to;
    } 

    Dictionary<string, object> values;
    List<Patch> patches;

    public Dictionary<string, object> AsDict() => values;

    static string randKey()
    {
        Span<byte> rand = stackalloc byte[16];
        RandomNumberGenerator.Fill(rand);
        return Convert.ToHexString(rand);
    }

    public void Set<T>(T value, string path)
    {
        var (patcher, dir) = putIn(path);
        patcher.values[dir] = value;
    }
    public void Post<T>(T value, string path)
    {
        var (patcher, dir) = putIn(path);
        string key = randKey();

        Patcher keyStore;
        if (patcher.values.TryGetValue(dir, out var pos) && pos is Dictionary<string, object> dvar and not null)
            keyStore = new Patcher(dvar, patches);
        else
            keyStore = patcher.Next(dir);

        keyStore.values[key] = value;
    }
    (Patcher last, string lastSegment) putIn(string path)
    {
        var segments = path.Split('/');
        var last = this;
        int size = segments.Length;
        int index = 0;
        string segment = segments[index];
        loop:
        if(index < size && (segment = segments[index]) is not null or "")
        {
            if (index == size - 1)
                goto end;
            else
            {
                if(last.values.TryGetValue(segment, out var posDict) && posDict is Dictionary<string, object> dict and not null)
                    last = new Patcher(dict, patches);
                else
                    last = last.Next(segment);
                index++;
                goto loop;
            }
        }

    end:
        return (last, segment ?? "DBSample");
    }

    public Patcher Next(string name)
    {
        var dict = new Dictionary<string, object>(32);
        values[name] = dict;
        return new Patcher(dict, patches);
    }

    public void Update<T>(T value, string path) => patches.Add(new Patch(path, PatchType.Update, JsonConvert.SerializeObject(value)));
    public void Delete(string path) => patches.Add(new Patch(path, PatchType.Delete));
    public void WaitForPatching(Func<Task> onPatch) => patches.Add(new Patch(null, PatchType.Wait, null, onPatch));

    public async Task DoPatch(bool doPatch = true, string useCloudFunction = null)
    {
        foreach(var patch in patches)
        {
            switch(patch.Type)
            {
                case PatchType.Update or PatchType.Add:
                    await Database.Patch(patch.Json!, patch.Path);
                    break;
                case PatchType.Delete:
                    await Database.Delete(patch.Path);
                    break;
                case PatchType.Wait:
                    await patch.OnPatch();
                    break;
            }
        }

        if (doPatch || useCloudFunction is not null)
        {
            string json = JsonConvert.SerializeObject(values);
            if (doPatch)
                await Database.Patch(json, "");
            else if (useCloudFunction is not null)
            { }
        }
        else
        {
            var path = new ImmutableDBPath("");
            static async ValueTask putValues(Dictionary<string, object> values, ImmutableDBPath path)
            {
                /*foreach (var value in values)
                {
                    if (value.Value is Dictionary<string, object> tree)
                        await putValues(tree, path.MoveDown(value.Key));
                    else
                        await Database.Set(value.Value, path.MoveDown(value.Key).Path);
                }*/
                bool anyDeep = false;
                foreach (var value in values)
                {
                    if (value.Value is Dictionary<string, object> tree)
                    {
                        await putValues(tree, path.MoveDown(value.Key));
                        anyDeep = true;
                    }
                }
                if (anyDeep)
                {
                    values.AsParallel().ForAll(async value =>
                    {
                        if (value.Value is not Dictionary<string, object>)
                            await Database.Set(value.Value, path.MoveDown(value.Key).Path);
                    });
                    /*foreach (var value in values)
                    {
                        if (value.Value is not Dictionary<string, object>)
                            await Database.Set(value.Value, path.MoveDown(value.Key).Path);
                    }*/
                }
                else
                {
                    string json = JsonConvert.SerializeObject(values);
                    await Database.Patch(json, path.Path);
                }
            }
            await putValues(values, path);
        }
    }
    
    public Patcher(Dictionary<string, object> values)
    {
        this.values = values;
        patches = new List<Patch>();
    }
    Patcher(Dictionary<string, object> values, List<Patch> patches)
    {
        this.values = values;
        this.patches = patches;
    }
    public static Patcher New() => new Patcher(new Dictionary<string, object>());

    public readonly record struct Patch(string Path, PatchType Type, string? Json = null, Func<Task> OnPatch = null);
}
public enum PatchType
{
    Add,
    Update,
    Delete,

    Wait
}