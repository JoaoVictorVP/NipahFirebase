using NipahFirebase.FirebaseCore;
using NipahFirebase.FirebaseCore.Attributes;
using NipahFirebase.FirebaseCore.Attributes.Members;
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

    [Test, Order(3)]
    public async Task PraticalCaseTest_Save()
    {
        var person = new PraticalCaseObject_Person("Michaelis", 35);

        person.Cryptos.Result.XMR = 53;

        await person.Save("Clients/MyClient-000");

        Assert.Pass();
    }
    [Test, Order(5)]
    public async Task PraticalCaseTest_Load()
    {
        var client = await PraticalCaseObject_Person.Load("Clients/MyClient-000");
        
        Console.WriteLine(client);

        var cryptos = await client.Cryptos;

        Console.WriteLine($"BTC [{cryptos.BTC}], ETH [{cryptos.ETH}], XMR [{cryptos.XMR}]");

        Assert.AreEqual(53, cryptos.XMR);

        await client.Delete("Clients/MyClient-000");
    }

    [Test, Order(0)]
    public async Task ListTest_Save()
    {
        var list = new ListTestCase
        {
            Email = "sample@mail.com"
        };
        await list.Spent.Add(("A random book", 305));
        await list.Spent.Add(("A new collection of shoes", 5000));
        await list.Spent.Add(("A order for the book Man, The Economy and the state", 103));

        await list.Save("Funds");

        Assert.Pass();
    }
    [Test, Order(1)]
    public async Task ListTest_Load()
    {
        var list = await ListTestCase.Load("Funds");
        //var list = new ListTestCase();

        Assert.AreEqual(("A random book", 305), await list.Spent.Get(0));
        Assert.AreEqual("A new collection of shoes", (await list.Spent.Get(1)).desc);
        Assert.AreEqual(103, (await list.Spent.Get(2)).spent);

        await list.Delete("Funds");
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

        simple.Delete();
    }
}
[Firebase]
public partial class PraticalCaseObject_Person
{
    public string? Name;
    public int Age;
    Cryptos cryptos = new Cryptos { BTC = 1, ETH = 0.759m, XMR = 37 };

    public PraticalCaseObject_Person() { }
    public PraticalCaseObject_Person(string name, int age) { Name = name; Age = age; AsLoaded_cryptos(); }
}
[Firebase]
public partial class Cryptos
{
    public decimal BTC, XMR, ETH;
}

[Firebase]
public partial class ListTestCase
{
    [DatabaseName("Email")]
    public string Email;
    public FList<(string desc, decimal spent)> Spent = new ("---ListTestCase-Spent");
}
