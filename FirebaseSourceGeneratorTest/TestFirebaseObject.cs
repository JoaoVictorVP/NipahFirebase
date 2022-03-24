using NipahFirebase.FirebaseCore;
using NipahFirebase.FirebaseCore.Attributes;
using NUnit.Framework;

namespace FirebaseSourceGeneratorTest;

[Firebase("Iza_eu_te_amo!")]
public partial class TestFirebaseObject
{
    public string Name;
    public int Age;
    AnotherFirebaseObject _ref;
}

[Firebase]
public partial class AnotherFirebaseObject
{
    public bool Alive;

    NowWithStruct value;
}

[Firebase]
public partial struct NowWithStruct
{
    public decimal Money;
}