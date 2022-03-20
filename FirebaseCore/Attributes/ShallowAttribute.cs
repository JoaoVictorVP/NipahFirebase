namespace NipahFirebase.FirebaseCore.Attributes.Members;

/// <summary>
/// Makes this field or property shallow by default
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class ShallowAttribute : Attribute
{

}