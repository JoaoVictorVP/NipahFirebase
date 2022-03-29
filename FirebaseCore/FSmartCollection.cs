namespace NipahFirebase.FirebaseCore;

public class FSmartCollection<T> : ICustomInstanceFirebaseObject, IPublicFirebaseObject
{
    public bool IsLoaded => throw new NotImplementedException();

    public Task Delete(string path)
    {
        throw new NotImplementedException();
    }

    public Task Load(string path)
    {
        throw new NotImplementedException();
    }

    public Task Save(string path)
    {
        throw new NotImplementedException();
    }
}
