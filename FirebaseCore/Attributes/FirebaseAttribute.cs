namespace NipahFirebase.FirebaseCore.Attributes;

/// <summary>
/// Specify that this type should not be just deeply serialized and should have it's own Save and Load methods inside
/// </summary>
[AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
public class FirebaseAttribute : Attribute
{
    public string DefaultPath;

    public FirebaseAttribute() => DefaultPath = null;
    public FirebaseAttribute(string defaultPath) => DefaultPath = defaultPath;
}