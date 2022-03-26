using System.Linq.Expressions;
using System.Reflection;

namespace NipahFirebase.FirebaseCore;

public static class DatabaseHelper<T>
{
    public static bool IsCodeGenEnabled = true;

    static Type[] loadParams = new[] { typeof(string) };
    static Dictionary<Type, Func<string, Task<T>>> cachedLoads = new (32);
    public static Func<string, Task<T>> GetStaticLoad()
    {
        var type = typeof(T);

        if (cachedLoads.TryGetValue(type, out var loader))
            return loader;
        else
        {
            var method = type.GetMethod("Load", BindingFlags.Public|BindingFlags.Static, loadParams)!;

            loader = IsCodeGenEnabled switch
            {
                true => doExpression___Load(method),
                false => Task<T> (path) => (Task<T>)method.Invoke(null, new[] { path })
            };

            cachedLoads.Add(type, loader);
        }
        return loader;
    }
    static Func<string, Task<T>> doExpression___Load(MethodInfo m_load)
    {
        var param = Expression.Parameter(typeof(string));
        //var local = Expression.Variable(typeof(T));
        var body = Expression.Call(m_load, param);
        return Expression.Lambda<Func<string, Task<T>>>(body, param).Compile();
    }
}