namespace NipahFirebase.FirebaseCore;

public static class Shared
{
    static string apiKey, authDomain, dbUrl;
    public static string ApiKey => apiKey;
    public static string AuthDomain => authDomain;
    public static string DatabaseUrl => dbUrl;

    public static void Initialize(string apiKey, string authDomain, string dbUrl)
    {
        if (Shared.dbUrl is not null)
            throw new Exception("Cannot initialize Shared more than once");

        Shared.dbUrl = dbUrl;
        Shared.apiKey = apiKey;
        Shared.authDomain = authDomain;
    }
}