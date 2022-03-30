using NipahFirebase.FirebaseCore;

// using Patcher = System.Collections.Generic.Dictionary<string, object>;

namespace NipahFirebase.Indexing;

public readonly struct Indexer
{
    public string Path { get; init; }
    public int PathIndex => Path.GetHashCode();
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
        clearPrevious(key);

        List<string> perms = new List<string>(32);
        IndexingUtils.SmartPermuteString(value, perms);
        int index = PathIndex;
        int keyIndex = (PathIndex + key).GetHashCode();
        int chunk = Math.Abs(keyIndex % ChunkSize);

        foreach (var perm in perms)
        {
            string indexedPath = $"{IndexingUtils.Indexes}/{key}/{perm}/{IndexingUtils.Index}/{chunk}/{keyIndex}";
            patcher.Set(index, indexedPath);

            // For latter changes
            patcher.Post(indexedPath, IndexPath + '/' + key);
        }

        return this;
    }
    public Indexer Boolean(string key, bool value)
    {
        clearPrevious(key);

        int index = PathIndex;
        int keyIndex = (PathIndex + key).GetHashCode();
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
        int keyIndex = (PathIndex + key).GetHashCode();
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