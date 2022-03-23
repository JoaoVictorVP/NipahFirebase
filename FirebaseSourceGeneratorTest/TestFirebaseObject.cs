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

    public NowWithStruct Value;
}

[Firebase]
public partial struct NowWithStruct
{
    public decimal Money;
}

public class TestAsyncPerop
{
    TestFirebaseObject _cached;
    public ValueTask<TestFirebaseObject> Prop
    {
        get
        {
            if (_cached is null)
                return new ValueTask<TestFirebaseObject>(Task.Run<TestFirebaseObject>(() => { _cached = null; return _cached; }));
            else
                return new ValueTask<TestFirebaseObject>(_cached);
        }
    }

    [Test]
    public static async void Test()
    {
        var test = new TestAsyncPerop();
        var result = await test.Prop;
        var nextResultCall = await test.Prop;
    }
}