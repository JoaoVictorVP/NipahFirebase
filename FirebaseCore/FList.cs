using NipahFirebase.FirebaseCore.Attributes;

namespace NipahFirebase.FirebaseCore;

/// <summary>
/// Specify a shallow-oriented list for Firebase, this makes the owner object to need not to load all the internal items in order to load itself, and them makes possible to lazily load all the data when needed
/// </summary>
/// <typeparam name="T"></typeparam>
[Firebase]
public class FList<T> : ICustomInstanceFirebaseObject
{
    public bool IsLoaded { get; }

    public async Task Load(string path) { }
    public async Task Save(string path) { }
}
