﻿using Newtonsoft.Json;
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
        IndexableUserPath1 = "Users/Client-001";

    [Test]
    public async Task TestIndexableUser_SaveAndIndex()
    {
        var user = new IndexableUser("John", "diadetedio@outlook.com");

        await user.Save(IndexableUserPath);

        await new Indexer().WithChunkSize(100).For(IndexableUserPath)
            .String("Name", user.Name)
            .String("Email", user.Email)
            .AsPatcher()
                .DoPatch(false);

        // Other user with name as "Marry" and email as "marry@email.com"
        user = new IndexableUser("Marry", "marry@email.com");

        await user.Save(IndexableUserPath1);

        await new Indexer().WithChunkSize(100).For(IndexableUserPath1)
            .String("Name", user.Name)
            .String("Email", user.Email)
            .AsPatcher()
                .DoPatch(false);
        //await indexer.String("Name", user.Name);
        //await indexer.String("Email", user.Email);

        string url = await Database.Merge("", IndexableUserPath);
        Console.WriteLine(url);

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

    public IndexableUser(string name, string email)
    {
        Name = name;
        Email = email;
    }
    public IndexableUser() { }
}