namespace NipahFirebase.FirebaseCore.Attributes.Members;

/// <summary>
/// Makes this field or property shallow by default (it means lazy)
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class ShallowAttribute : Attribute
{
    public string? Name;

    /// <summary>
    /// This field or property should be marked as shallow and the generated property should be named as it is without initial _ (if has) and with first letter capitalization
    /// </summary>
    public ShallowAttribute() { }
    /// <summary>
    /// This field or property should be marked as shallow and the generated property should be named as <paramref name="name"/>
    /// </summary>
    /// <param name="name"></param>
    public ShallowAttribute(string name) => Name = name;
}