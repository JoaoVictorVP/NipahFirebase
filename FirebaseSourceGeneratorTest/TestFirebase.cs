using NipahFirebase.FirebaseCore;
using NUnit.Framework;

namespace FirebaseSourceGeneratorTest;

[TestFixture]
public class TestFirebase
{
    [OneTimeSetUp]
    public async Task Setup()
    {
        Shared.Initialize("AIzaSyAfxz9mv9BB6u2pQ-ze9wcchU0L5TMT4B4", "nipah-firebase-test.firebaseapp.com", "https://nipah-firebase-test-default-rtdb.firebaseio.com/");
        Auth.Initialize();
        Database.Initialize();

        await Auth.SignInAnonymously();
    }
    [Test, Order(0)]
    public async Task TrySimpleObject()
    {
        var simple = new TestFirebaseObject
        {
            Name = "John",
            Age = 20,
            Ref = new AnotherFirebaseObject
            {
                Alive = true,
                Value = new NowWithStruct
                {
                    Money = 305.908604954389m
                }.AsValueTask()
            }.AsValueTask()
        };simple.SetDatabasePath("");
        await simple.Save();

        Assert.IsNotNull(await Database.Client.Child("Iza eu te amo!").OnceAsync<object>(), "Boo null return!");
    }
    [Test, Order(1)]
    public async Task TryLoadSimpleObject()
    {
        var simple = await TestFirebaseObject.Load();

        Assert.IsNotNull(simple);
    }
}