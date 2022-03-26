using Firebase.Database;
using Firebase.Database.Query;
using System;

namespace NipahFirebase.FirebaseCore;

public static class Database
{
    public static FirebaseClient Client;

    public static async Task<T> Get<T>(string path)
    {
        return await Client.Child(path).OnceSingleAsync<T>();
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
}
