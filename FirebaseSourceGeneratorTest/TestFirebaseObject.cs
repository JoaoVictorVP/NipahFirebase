using NipahFirebase.FirebaseCore;
using NipahFirebase.FirebaseCore.Attributes;

namespace FirebaseSourceGeneratorTest;

[Firebase("Iza eu te amo!")]
public partial class TestFirebaseObject
{
    public string Name;
    public int Age;
    public AnotherFirebaseObject Ref;
}

[Firebase]
public class AnotherFirebaseObject
{
    public bool Alive;
}