namespace NipahFirebase.FirebaseCore;

public interface IFirebaseObject
{
    Task Save(string path);
}
public interface ICustomFirebaseObject<T> : IFirebaseObject
{
    //abstract static T Load(string path);
}
public interface ICustomInstanceFirebaseObject : IFirebaseObject
{
    Task Load(string path);
}