namespace NipahFirebase.FirebaseCore;

public interface IFirebaseObject
{
    Task Save(string path);
}
public interface ICustomFirebaseObject<T> : IFirebaseObject
{
    // abstract static T Load(string path);
    // abstract static Task Delete(string path);
}
public interface ICustomInstanceFirebaseObject : IFirebaseObject
{
    bool IsLoaded { get; }
    Task Load(string path);
    Task Delete(string path);
}
public interface IPublicFirebaseObject : IFirebaseObject { }