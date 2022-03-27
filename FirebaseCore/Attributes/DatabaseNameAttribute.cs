namespace NipahFirebase.FirebaseCore.Attributes.Members;

/// <summary>
/// Fixed naming for path usage on databases, prevents errors when future renaming occur on this field/property and grants structural immutability and compatibility to database
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class DatabaseNameAttribute : Attribute
{
    string name;
    /// <summary>
    /// Implements this <paramref name="name"/> as path on this field/property
    /// </summary>
    /// <param name="name"></param>
    public DatabaseNameAttribute(string name) => this.name = name;
}