using Newtonsoft.Json;
using NipahFirebase.FirebaseCore;

// using Patcher = System.Collections.Generic.Dictionary<string, object>;
using Dict = System.Collections.Generic.Dictionary<string, object>;
using TimeStamp = System.Collections.Generic.Dictionary<string, long>;

namespace NipahFirebase.Indexing;

public readonly struct Indexer
{
    public string Path { get; init; }
    public int PathIndex => Path.GetDeterministicHashCode();
    /// <summary>
    /// Final path on indexes
    /// </summary>
    public string IndexPath => $"{IndexingUtils.Indexes}/Final/{PathIndex}";
    public int ChunkSize { get; init; }
    Patcher patcher { get; init; }

    public Patcher AsPatcher() => patcher;

    public Indexer For(string path)
    {
        var self = this with { Path = path, patcher = patcher ^ Patcher.New() };
        patcher.WaitForPatching(async () => await Database.Set(self.PathIndex, self.IndexPath));
        return self;
    }
    public Indexer WithChunkSize(int chunkSize) => this with { ChunkSize = chunkSize, patcher = patcher ^ Patcher.New() };
    
    public Indexer String(string key, string value)
    {
        // clearPrevious(key);

        var self = this;
        patcher.WaitForPatching(async () =>
        {
            List<string> perms = new List<string>(32);
            IndexingUtils.SmartPermuteString(value, perms);
            int index = self.PathIndex;
            //int keyIndex = (self.PathIndex + key).GetHashCode();
            //int chunk = Math.Abs(keyIndex % self.ChunkSize);

            var path = new ImmutableDBPath($"{IndexingUtils.Indexes}/{key}");

            var master = int.Parse(await Database.Get<string>(path.MoveDown("Master")) ?? "-1");
            bool firstMaster = false;
            if (master < 0) { firstMaster = true; master = 0; }

            Dict chunk;
            if (firstMaster)
                chunk = new Dict(32);
            else
                chunk = await Database.Get<Dict>(path.MoveDown(master.ToString()));
            TimeStamp history;
            bool modHistory = false;
            if (firstMaster)
            { history = new TimeStamp(32); modHistory = true; }
            else
                history = await Database.Get<TimeStamp>(path.MoveDown("History"));

            if (firstMaster is false)
            {
                int count = 0;
                foreach (var (segment, o_indexes) in chunk)
                {
                    var indexes = JsonConvert.DeserializeObject<HashSet<int>>((string)o_indexes);

                    count += indexes.Count;

                    chunk[segment] = indexes;
                }
                count /= 10;
                if (count > self.ChunkSize)
                {
                    foreach (var perm in perms)
                    {
                        if (chunk.TryGetValue(perm, out var o_indexes) && o_indexes is HashSet<int> indexes)
                        {
                            indexes.Remove(index);
                            if(indexes.Count == 0)
                            {
                                chunk.Remove(perm);
                                history.Remove(perm);
                                modHistory = true;
                            }
                        }
                    }

                    var clone = new Dict(chunk);
                    foreach (var pair in clone)
                        clone[pair.Key] = JsonConvert.SerializeObject(pair.Value);

                    await Database.Set(clone, path.MoveDown(master.ToString()));

                    chunk = new Dict(32);

                    master++;
                    await Database.Set(master, path.MoveDown("Master"));
                }
            }
            else
                await Database.Set(master, path.MoveDown("Master"));
            
            foreach(var perm in perms)
            {
                HashSet<int> indexes;
                if (chunk.TryGetValue(perm, out var o_indexes) && o_indexes is HashSet<int>)
                    indexes = (HashSet<int>)o_indexes;
                else
                {
                    chunk[perm] = indexes = new HashSet<int>(32);
                    history.Add(perm, DateTime.UtcNow.Ticks);
                    modHistory = true;
                }

                indexes.Add(index);
            }
            foreach (var pair in chunk)
                chunk[pair.Key] = JsonConvert.SerializeObject(pair.Value);

            await Database.Set(chunk, path.MoveDown(master.ToString()));
            if (modHistory)
                await Database.Set(history, path.MoveDown("History"));
        });

        return this;
    }
    struct Master
    {
        public string Last;
    }
    public Indexer Boolean(string key, bool value)
    {
        clearPrevious(key);

        int index = PathIndex;
        int keyIndex = (PathIndex + key).GetDeterministicHashCode();
        int chunk = Math.Abs(keyIndex % ChunkSize);

        string indexedPath = $"{IndexingUtils.Indexes}/{key}/{(value ? "true" : "false")}/{IndexingUtils.Index}/{chunk}/{keyIndex}";

        //await Database.Set(index, indexedPath);
        patcher.Set(index, indexedPath);

        patcher.Post(indexedPath, IndexPath + '/' + key);

        return this;
    }

    public Indexer Number(string key, int value, int maxLength)
    {
        clearPrevious(key);

        int index = PathIndex;
        int keyIndex = (PathIndex + key).GetDeterministicHashCode();
        int chunk = Math.Abs(keyIndex % ChunkSize);

        var indexedPath = new DBPath($"{IndexingUtils.Indexes}/{key}");

        string rep = value.ToString();
        if (rep.Length > maxLength) throw new Exception($"{value} cannot have more than {maxLength} characters length!");

        rep = rep.PadLeft(maxLength, '0');

        foreach (var r in rep)
        {
            indexedPath.MoveDown(r.ToString());
            if(r is not '0')
            {
                indexedPath.MoveDown(chunk.ToString()).MoveDown(keyIndex.ToString());
                string idxPath = indexedPath.ToString();
                //await Database.Set(index, idxPath);
                patcher.Set(index, idxPath);

                patcher.Post(idxPath, IndexPath + '/' + key);
                break;
            }
        }

        return this;
    }
    void clearPrevious(string key)
    {
        var self = this;
        patcher.WaitForPatching(async Task () =>
        {
            string previousPath = self.IndexPath + '/' + key;

            var previous = await Database.GetAll<string>(previousPath);
            foreach (var item in previous)
                await Database.Delete(item.value);

            await Database.Delete(previousPath);
        });
    }
}