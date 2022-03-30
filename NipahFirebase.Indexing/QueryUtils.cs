using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NipahFirebase.FirebaseCore;

using Dict = System.Collections.Generic.Dictionary<string, object>;
using TimeStamp = System.Collections.Generic.Dictionary<string, NipahFirebase.Indexing.HistoryStamp>;

namespace NipahFirebase.Indexing;

public static class QueryUtils
{
    public static async Task<HashSet<string>> Execute(this Query query)
    {
        HashSet<string> results = new(32);

        var segments = query.AsSegments();

        foreach (var segment in segments)
            await execute(segment, results);

        return results;
    }
    static async ValueTask execute(Query.QuerySegment segment, HashSet<string> results)
    {
        // var locals = new HashSet<string>(32);

        var final = new ImmutableDBPath($"{IndexingUtils.Indexes}/Final");
        var path = new ImmutableDBPath($"{IndexingUtils.Indexes}/{segment.Key}");

        string? sval = segment.Type switch
        {
            Query.QType.StrContains or Query.QType.StrEqual or Query.QType.StrDifferent => (string)segment.Value,
            _ => null
        };
        (int value, int maxLength) ival = segment.Type switch
        {
            Query.QType.NumEqual or Query.QType.NumGreater or Query.QType.NumLower => ((int, int))segment.Value,
            _ => default
        };

        bool bval = segment.Type switch
        {
            Query.QType.BoolEqual or Query.QType.BoolDifferent => (bool)segment.Value,
            _ => false
        };

        switch(segment.Type)
        {
            case Query.QType.StrEqual:
                results.Add(await obtainPath(path.MoveDown("Directs").MoveDown(IndexingUtils.FormatPath(sval!)), final));
                break;
            case Query.QType.StrContains:
                await strContains(sval!, path, final, results);
                break;
            case Query.QType.StrDifferent:
                await strDifferent(sval!, path, final, results);
                break;

            case Query.QType.NumEqual:
                await numEqual(ival.value, ival.maxLength, path, final, results);
                break;
            case Query.QType.NumGreater:
                await numGreater(ival.value, ival.maxLength, path, final, results);
                break;
            case Query.QType.NumLower:
                await numLower(ival.value, ival.maxLength, path, final, results);
                break;

            case Query.QType.BoolEqual:
                await boolEqual(bval, path, final, results);
                break;
            case Query.QType.BoolDifferent:
                await boolDifferent(bval, path, final, results);
                break;
        }
    }

    #region bool
    static ValueTask boolDifferent(bool val, ImmutableDBPath path, ImmutableDBPath final, HashSet<string> results)
    {
        return boolEqual(!val, path, final, results);
    }
    static async ValueTask boolEqual(bool val, ImmutableDBPath path, ImmutableDBPath final, HashSet<string> results)
    {
        var chunks = await Database.Get<Dict>(path.MoveDown(val ? "true" : "false").MoveDown(IndexingUtils.Index));

        foreach((_, var o_chunk) in chunks)
        {
            if(o_chunk is Dict chunk)
            {
                foreach((_, var o_index) in chunk)
                {
                    if(o_index is string index)
                    {
                        var fpath = await obtainPathFromIndex(index, final);
                        results.Add(fpath);
                    }
                }
            }
        }
    }
    #endregion

    #region num
    static async ValueTask numLower(int val, int maxLength, ImmutableDBPath path, ImmutableDBPath final, HashSet<string> results)
    {
        async ValueTask doStep(ImmutableDBPath npath, char? limiter = null)
        {
            int? min = null;
            if (limiter is not null)
                min = int.Parse(limiter.Value.ToString());

            for (int i = 9; i > -9; i--)
            {
                if (min is not null && i > min)
                    break;
                npath.MoveDown(i.ToString());
                var chunks = await Database.Get<Dict>(npath.MoveDown(IndexingUtils.Index));
                foreach (var (_, o_chunk) in chunks)
                {
                    if (o_chunk is Dict chunk)
                    {
                        foreach ((_, var index) in chunk)
                        {
                            var fpath = await obtainPathFromIndex(npath.MoveDown((string)index), final);
                            results.Add(fpath);
                        }
                    }
                }
            }
        }

        string sval = val.ToString().PadLeft(maxLength, '0');

        var numPath = path;
        bool begin = false;
        bool first = true;
        foreach (var s in sval)
        {
            if (s is not '0')
                begin = true;

            if (begin)
                await doStep(numPath, s);

            numPath = numPath.MoveDown(s.ToString());
        }
    }
    static async ValueTask numGreater(int val, int maxLength, ImmutableDBPath path, ImmutableDBPath final, HashSet<string> results)
    {
        async ValueTask doStep(ImmutableDBPath npath, char? limiter = null, bool allowNegative = false)
        {
            int min = -1;
            if (limiter is not null)
                min = int.Parse(limiter.Value.ToString());

            for(int i = allowNegative ? -9 : 0; i < 9; i++)
            {
                if (min is not -1 && i < min)
                    break;
                npath = npath.MoveDown(i.ToString());
                var s_chunks = await Database.Get<string>(npath.MoveDown(IndexingUtils.Index));

                if (s_chunks is null) continue;

                var chunks = JsonConvert.DeserializeObject<Dict>(s_chunks);

                foreach (var (_, no_chunk) in chunks)
                {
                    var o_chunk = no_chunk;
                    if (o_chunk is JObject) o_chunk = ((JObject)o_chunk).ToObject<Dict>();
                    if (o_chunk is Dict chunk)
                    {
                        foreach ((_, var index) in chunk)
                        {
                            var fpath = await obtainPathFromIndex(npath.MoveDown((string)index), final);
                            results.Add(fpath);
                        }
                    }
                }
            }
        }

        string sval = val.ToString().PadLeft(maxLength, '0');

        var numPath = path;
        bool first = true;
        foreach (var s in sval)
        {
            if (s is not '0')
            {
                await doStep(numPath, s, allowNegative: first);
                break;
            }
            else
                await doStep(numPath, allowNegative: first);

            numPath = numPath.MoveDown(s.ToString());

            first = false;
        }
    }
    static async ValueTask numEqual(int val, int maxLength, ImmutableDBPath path, ImmutableDBPath final, HashSet<string> results)
    {
        string sval = val.ToString().PadLeft(maxLength, '0');

        var numPath = path;
        foreach (var s in sval)
        {
            numPath = numPath.MoveDown(s.ToString());
            if (s is not '0')
                break;
        }

        var chunks = await Database.Get<Dict>(numPath = numPath.MoveDown(IndexingUtils.Index));
        foreach(var (cidx, _o_chunk) in chunks)
        {
            var o_chunk = _o_chunk;
            if (o_chunk is JObject jsO) o_chunk = jsO.ToObject<Dict>();
            if(o_chunk is Dict chunk)
            {
                foreach(var (idx, index) in chunk)
                {
                    var fpath = await obtainPath(numPath.MoveDown(cidx).MoveDown(idx), final);
                    results.Add(fpath);
                }
            }
        }
    }
    #endregion

    #region str
    static async ValueTask strDifferent(string val, ImmutableDBPath path, ImmutableDBPath final, HashSet<string> results)
    {
        var history = await Database.Get<TimeStamp>(path.MoveDown("History"));

        HashSet<int> masters = new(32);

        foreach(var (part, (m, _)) in history)
        {
            if (part != val)
                masters.Add(m);
        }

        foreach(var master in masters)
        {
            var chunk = await Database.Get<Dict>(path.MoveDown(master.ToString()));

            foreach((_, var o_segment) in chunk)
            {
                if(o_segment is string str_segment)
                {
                    var segment = JsonConvert.DeserializeObject<HashSet<int>>(str_segment);

                    foreach (var index in segment)
                        results.Add(await obtainPathFromIndex(index, final));
                }
            }
        }
    }
    static async ValueTask strContains(string val, ImmutableDBPath path, ImmutableDBPath final, HashSet<string> results)
    {
        var history = await Database.Get<TimeStamp>(path.MoveDown("History"));

        int master = 0;
        long lastTime = long.MaxValue;
        foreach(var (part, (m, t)) in history)
        {
            if(part == val && t < lastTime)
            {
                master = m;
                lastTime = t;
            }
        }
        // var set = await Database.Get<int[]>(path.MoveDown(master.ToString()).MoveDown(val));
        string s_set = await Database.Get<string>(path.MoveDown(master.ToString()).MoveDown(val));
        var set = JsonConvert.DeserializeObject<HashSet<int>>(s_set);

        foreach (var index in set)
            results.Add(await obtainPathFromIndex(index, final));
    }
    #endregion

    static async ValueTask<string> obtainPathFromIndex(int index, ImmutableDBPath final)
    {
        return await Database.Get<string>(final.MoveDown(index.ToString()));
    }
    static async ValueTask<string> obtainPathFromIndex(string index, ImmutableDBPath final)
    {
        return await Database.Get<string>(final.MoveDown(index));
    }
    static async ValueTask<string> obtainPath(ImmutableDBPath path, ImmutableDBPath final)
    {
        string index = await Database.Get<string>(path);
        return await Database.Get<string>(final.MoveDown(index));
    }
}