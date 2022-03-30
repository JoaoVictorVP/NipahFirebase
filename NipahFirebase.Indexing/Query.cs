// using Patcher = System.Collections.Generic.Dictionary<string, object>;

namespace NipahFirebase.Indexing;

public readonly struct Query
{
    readonly List<QuerySegment> segments;

    internal List<QuerySegment> AsSegments() => segments;

    public static Query New() => new Query(new List<QuerySegment>(32));

    Query(List<QuerySegment> segments) => this.segments = segments;

    public Query WhereEqual(string key, string value)
    {
        segments.Add(new QuerySegment(key, value, QType.StrEqual));
        return this;
    }
    public Query WhereEqual(string key, int value, int maxLength)
    {
        segments.Add(new QuerySegment(key, (value, maxLength), QType.NumEqual));
        return this;
    }
    public Query WhereEqual(string key, bool value)
    {
        segments.Add(new QuerySegment(key, value, QType.BoolEqual));
        return this;
    }

    public Query WhereContains(string key, string value)
    {
        segments.Add(new QuerySegment(key, value, QType.StrContains));
        return this;
    }

    public Query WhereDifferent(string key, string value)
    {
        segments.Add(new QuerySegment(key, value, QType.StrDifferent));
        return this;
    }
    public Query WhereDifferent(string key, bool value)
    {
        segments.Add(new QuerySegment(key, value, QType.BoolDifferent));
        return this;
    }

    [Obsolete("WIP (Not working yet)", true)]
    public Query WhereGreater(string key, int value, int maxLength)
    {
        segments.Add(new QuerySegment(key, (value, maxLength), QType.NumGreater));
        return this;
    }
    [Obsolete("WIP (Not working yet)", true)]
    public Query WhereLower(string key, int value, int maxLength)
    {
        segments.Add(new QuerySegment(key, (value, maxLength), QType.NumLower));
        return this;
    }

    internal readonly record struct QuerySegment(string Key, object Value, QType Type)
    {
        public QueryFilter Filter { get; init; }
    }
    internal readonly record struct QueryFilter
    {

    }
    internal readonly struct QueryState
    {
        Dictionary<string, object> reg { get; init; }

        public QueryState Set(string key, object value)
        {
            QueryState self;
            if (reg is null)
                self = this with { reg = new(32) };
            else
                self = this;

            self.reg[key] = value;

            return self;
        }
        public T? Get<T>(string key)
        {
            if (reg is null)
                return default;
            reg.TryGetValue(key, out var value);
            return value is null ? default : (T)value;
        }
    }
    public enum QType
    {
        StrEqual,
        StrContains,
        StrDifferent,

        NumGreater,
        NumLower,
        NumEqual,

        BoolEqual,
        BoolDifferent
    }
}
