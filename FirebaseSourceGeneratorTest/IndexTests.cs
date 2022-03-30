using Newtonsoft.Json;
using NipahFirebase.FirebaseCore;
using NipahFirebase.FirebaseCore.Attributes;
using NipahFirebase.Indexing;
using NUnit.Framework;

namespace FirebaseSourceGeneratorTest;

[TestFixture]
public class IndexTests
{
    [OneTimeSetUp]
    public async Task Setup()
    {
        Shared.Initialize("AIzaSyAfxz9mv9BB6u2pQ-ze9wcchU0L5TMT4B4", "nipah-firebase-test.firebaseapp.com", "https://nipah-firebase-test-default-rtdb.firebaseio.com/");
        Auth.Initialize();
        Database.Initialize();

        await Auth.SignInAnonymously();
    }

    const string IndexableUserPath = "Users/Client-000",
        IndexableUserPath1 = "Users/Client-001",
        IndexableUserPath2 = "Users/Client-002";

    [Test]
    public async Task TestIndexableUser_QueryStrContains()
    {
        var results = await Query.New().WhereContains("Email", "1").WhereContains("Email", "a").WhereContains("Email", "A").Execute();

        Console.WriteLine(JsonConvert.SerializeObject(results));

        Assert.Pass();
    }
    [Test]
    public async Task TestIndexableUser_QueryStrEquals()
    {
        var results = await Query.New().WhereEqual("Email", "diadetedio@outlook.com").Execute();

        Console.WriteLine(JsonConvert.SerializeObject(results));

        Assert.Pass();
    }
    [Test]
    public async Task TestIndexableUser_QueryStrDifferent()
    {
        var results = await Query.New().WhereDifferent("Email", "o").Execute();

        Console.WriteLine(JsonConvert.SerializeObject(results));

        Assert.Pass();
    }

    [Test]
    public async Task TestIndexableUser_QueryNumEqual()
    {
        var results = await Query.New().WhereEqual("Money", 1000, 5).Execute();

        Console.WriteLine(JsonConvert.SerializeObject(results));

        Assert.Pass();
    }

    [Test]
    public async Task TestIndexableUser_StressIndexing()
    {
        string randString(int size)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            Span<char> stringChars = new char[size];
            var random = new Random();

            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            return stringChars.ToString();
        }
        int randInteger(int min, int max)
        {
            var random = new Random();
            return random.Next(min, max);
        }
        bool randBool()
        {
            var random = new Random();
            return random.Next(0, 2) == 0;
        }
        IndexableUser newUser()
        {
            string name = randString(10);
            string email = randString(10);
            int money = randInteger(0, 1000000);
            bool isGood = randBool();

            return new IndexableUser(name, email, money, isGood);
        }
        async Task testCase(int index, IndexableUser user)
        {
            string path = $"Users/Client-{index.ToString().PadLeft(3, '0')}";

            string name = user.Name, email = user.Email, money = user.Money.ToString();
            bool isGood = user.Good;

            await user.Save(path);

            await new Indexer().WithChunkSize(100).For(path)
                .String("Name", name)
                .String("Email", email)
                .String("Money", money)
                .Boolean("Good", isGood)
                .AsPatcher()
                    .DoPatch(false);
        }

        for (int i = 0; i < 100; i++)
        {
            var user = newUser();
            try
            {
                await testCase(i, user);
            }catch (Exception exc)
            {
                Assert.Fail($"Failed on {i} with\n{JsonConvert.SerializeObject(user)}\n\n\nException: {exc}");
            }
        }

        Assert.Pass("Passed");
    }

    [Test]
    public async Task TestIndexableUser_SaveAndIndex()
    {
        var user = new IndexableUser("John", "diadetedio@outlook.com", 1000, true);

        await user.Save(IndexableUserPath);

        await new Indexer().WithChunkSize(100).For(IndexableUserPath)
            .String("Name", user.Name)
            .String("Email", user.Email)
            .Number("Money", user.Money, 5)
            .Boolean("Good", user.Good)
            .AsPatcher()
                .DoPatch(false);

        // Other user with name as "Marry" and email as "marry@email.com"
        user = new IndexableUser("Marry", "marry@email.com", 300, true);

        await user.Save(IndexableUserPath1);

        await new Indexer().WithChunkSize(100).For(IndexableUserPath1)
            .String("Name", user.Name)
            .String("Email", user.Email)
            .Number("Money", user.Money, 5)
            .Boolean("Good", user.Good)
            .AsPatcher()
                .DoPatch(false);

        // Other user with name as "Johannn" and email as "imjohannn@hotmail.com"
        user = new IndexableUser("Johannn", "imjohannn@hotmail.com", 10, false);

        await user.Save(IndexableUserPath2);

        await new Indexer().WithChunkSize(100).For(IndexableUserPath2)
            .String("Name", user.Name)
            .String("Email", user.Email)
            .Number("Money", user.Money, 5)
            .Boolean("Good", user.Good)
            .AsPatcher()
                .DoPatch(false);

        Assert.Pass();
    }

    [Test]
    public void PatcherTest()
    {
        var patcher = Patcher.New();

        patcher.Set(true, "Clients/Johnson/Good/Fine");

        patcher.Set("John", "Clients/Johnson/Good/Name");

        patcher.Post(100, "Clients/Johnson/Good");

        Console.WriteLine(JsonConvert.SerializeObject(patcher.AsDict(), new JsonSerializerSettings { Formatting = Formatting.Indented }));

        Assert.Pass();
    }
}

[Firebase]
public partial class IndexableUser
{
    public string Name;
    public string Email;
    public int Money;

    public bool Good;

    public IndexableUser(string name, string email, int money, bool good)
    {
        Name = name;
        Email = email;
        Money = money;

        Good = good;
    }
    public IndexableUser() { }
}