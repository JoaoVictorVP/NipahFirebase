using Firebase.Auth;
using LiteDB;
using Norgerman.Cryptography.Scrypt;
using System.Security.Cryptography;
using System.Text;

namespace NipahFirebase.FirebaseCore;

public static class Auth
{
    static User loggedUser => Client.User;
    public static string FirebaseToken => loggedUser.Credential.IdToken;

    static event Action _login, _logout;
    public static void OnLogin(Action callback)
    {
        _login += callback;
    }
    public static void OnLogout(Action callback)
    {
        _logout += callback;
    }

    // public static event CanSignUpEvent CanSignUp;

    static string ObtainPassword(string email, string input)
    {
        Span<byte> salt = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(Shared.ApiKey + email));
        var password = ScryptUtil.Scrypt(input, salt, 16384, 8, 1, 64);

        return Convert.ToBase64String(password);
    }
    static string GetUID(string email, string password)
    {
        const int passwordDerivationIterations = 1300,
            keySize = 32;

        byte[] salt = new byte[32];
        new Random((email + password).GetHashCode()).NextBytes(salt);
        //Rfc2898DeriveBytes.Pbkdf2(password, salt, HashAlgorithmName.SHA512, )
        var derive = new Rfc2898DeriveBytes(password, salt, passwordDerivationIterations);
        var key = derive.GetBytes(keySize);
        int emailSize = Encoding.Unicode.GetByteCount(email);
        Span<byte> data = new byte[emailSize + keySize];
        Encoding.Unicode.GetBytes(email, data);
        key.CopyTo(data[emailSize..]);

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(data, hash);
        return Convert.ToHexString(hash);
    }

    public static async Task SignInAnonymously()
    {
        await Client.SignInAnonymouslyAsync();
    }

    public static async Task SignIn(string email, string password)
    {
        await Client.SignInWithEmailAndPasswordAsync(email, password);
    }
    // public delegate bool CanSignUpEvent(ref SignUpData data);
    public static async Task<SignUpData> SignUp(string email, string password, string name)
    {
        // Strong cryptographic password
        password = ObtainPassword(email, password);

        var signUpData = new SignUpData(name, email, password, GetUID(email, password));

        /*if (!CanSignUp?.Invoke(ref signUpData) ?? true)
            return default;*/

        await Client.CreateUserWithEmailAndPasswordAsync(email, password);

        _login?.Invoke();

        return signUpData;
}
    public static async void SignOut()
    {
        await Client.SignOutAsync();
    }

    static FirebaseAuthClient Client;

    public static void Initialize()
    {
        Client = new FirebaseAuthClient(new FirebaseAuthConfig
        {
            ApiKey = Shared.ApiKey,
            AuthDomain = Shared.AuthDomain
        });
        Client.AuthStateChanged += (s, p) =>
        {
            //if (p.User != null)
            //    canLogin = true;
            //_login?.Invoke();
            //else
            if (p.User is null)
                _logout?.Invoke();
        };
#if TEST
        SignIn("test@email.com", "12345");
#endif
    }
}
public struct SignUpData
{
    public readonly string Name, Email, Password, UID;

    public SignUpData(string name, string email, string password, string uid)
    {
        Name = name;
        Email = email;
        Password = password;

        UID = uid;
    }
}