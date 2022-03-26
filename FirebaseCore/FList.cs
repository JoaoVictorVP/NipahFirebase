using NipahFirebase.FirebaseCore.Attributes;
using System.Collections.Generic;

namespace NipahFirebase.FirebaseCore;

/// <summary>
/// Specify a shallow-oriented list for Firebase, this makes the owner object to need not to load all the internal items in order to load itself, and them makes possible to lazily load all the data when needed
/// </summary>
/// <typeparam name="T"></typeparam>
[Firebase]
public class FList<T> : ICustomInstanceFirebaseObject, IPublicFirebaseObject
{
    int count;
    List<int> removed_indexes;
    string dbPath;
    public bool IsLoaded { get; }

    LoadType _loadType;
    LoadType loadType
    {
        get
        {
            if(_loadType is LoadType.ToVerify)
            {
                if (typeof(T).GetInterface("ICustomFirebaseObject") is not null)
                    _loadType = LoadType.StaticLoad;
                else if (typeof(T).GetInterface("ICustomInstanceFirebaseObject") is not null)
                    _loadType = LoadType.Load;
                else
                    _loadType = LoadType.Direct;
            }
            return _loadType;
        }
    }
    enum LoadType : byte
    {
        ToVerify,

        Direct,
        Load,
        StaticLoad
    }

    /*public static FList<T> New(string path) => new FList<T>
    {
        dbPath = path,
        list = new Dictionary<int, T>(32),
        removed_indexes = new List<int>(32)
    };*/
    public FList(string path)
    {
        dbPath = path;
        list = new Dictionary<int, T>(32);
        removed_indexes = new List<int>(32);
    }

    public async Task Load(string path)
    {
        // dbPath = path;
        dbPath = await Database.Get<string>(path + "/Path");

        list = new (32);
        //removed_indexes = new List<int>(32);
        count = await Database.Get<int>(path + "/Count");
        removed_indexes = await Database.Get<List<int>>(path + "/RemovedIndexes");
    }
    public async Task Save(string path)
    {
        await Database.Set(dbPath, path + "/Path");
        await Database.Set(count, path + "/Count");
        await Database.Set(removed_indexes, path + "/RemovedIndexes");
    }

    public async Task Delete(string path)
    {
        await Database.Delete(dbPath);
        await Database.Delete(path);
    }

    string GenPath(int index) => $"{dbPath}/{index}";

    #region Implementation

    Dictionary<int, T> list;
    public async ValueTask<T> Get(int index)
    {
        if (list.TryGetValue(index, out T value))
            return value;
        string path = GenPath(index);
        //value = await Database.Get<T>(GenPath(index));
        value = loadType switch
        {
            LoadType.Direct => await Database.Get<T>(path),
            LoadType.Load => await instance_load(path),
            LoadType.StaticLoad => await DatabaseHelper<T>.GetStaticLoad()(path),

            _ => default!
        };
        
        list[index] = value;
        return value;
    }
    async ValueTask<T> instance_load(string path)
    {
        var t_ins = Activator.CreateInstance<T>();
        await ((ICustomInstanceFirebaseObject)t_ins!).Load(path);
        return t_ins;
    }

    public async Task<int> Add(T value)
    {
        int index = calc_index();
        await Set(index, value);
        return index;
    }
    int calc_index()
    {
        int nindex = count;
        bool assume_index = false;
        foreach (var rindex in removed_indexes)
        {
            if (rindex < count)
            {
                nindex = rindex;
                removed_indexes.Remove(rindex);
                assume_index = true;
            }
        }
        if (assume_index is false)
            count++;
        return nindex;
    }

    public async Task Set(int index, T value)
    {
        await Database.Set(value, GenPath(index));
        list[index] = value;
    }
    public async Task Remove(int index)
    {
        await Database.Delete(GenPath(index));
        list.Remove(index);
        count--;

        removed_indexes.Add(index);
    }

    public Enumerator GetAsyncEnumerator() => new Enumerator(this);

    #endregion

    public struct Enumerator : IAsyncEnumerator<T>
    {
        public readonly FList<T> list;
        int index;
        T current;
        public T Current => current;

        public ValueTask DisposeAsync() => default;

        public async ValueTask<bool> MoveNextAsync()
        {
            current = await list.Get(index);
            index++;
            if (Equals(current, default(T)) is false)
                return false;
            return true;
        }

        public Enumerator(FList<T> list) { this.list = list; index = 0; current = default!; }
    }
}
