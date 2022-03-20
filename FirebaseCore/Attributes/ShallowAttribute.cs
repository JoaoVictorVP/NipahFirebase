namespace NipahFirebase.FirebaseCore.Attributes.Members;

/// <summary>
/// Makes this field or property shallow by default
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
[Obsolete("Don't use this, instead, the Source Generator and handmade implementations should work just fine!", true)]
public class ShallowAttribute : Attribute
{

}