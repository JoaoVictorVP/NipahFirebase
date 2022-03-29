# Nipah Firebase
## Introduction
What is ***Nipah Firebase***?
A complex framework that makes heavy use of the C# source generator to facilitate the use of Google's Firebase services.

## Installation
```
dotnet add package FirebaseCore
dotnet add package NipahFirebase.SourceGenerator
```
Then set it up like this:
```csharp
public void InitializeThingsHere()
{
	// any other initializations...
	Shared.Initialize(
		/* string */ apiKey,
		/* string */ authDomain
		/* string */ databaseUrl);
	Auth.Initialize();
	Database.Initialize();
}
```
And you'll be ready to go.

## How to use it?
To use this system, it is very simple, but it is important to understand its key concepts:
  * All classes that will use the system need to be designed with this in mind
  * You still need to have an idea of where you are going to save your data, although the system makes the process 90% easier, but some things will still be up to the programmer

That said, we can start with a simple example, a simple registration system and messages related to these users.

The user class:
```csharp
using NipahFirebase.FirebaseCore;

[Firebase]
public partial class User
{
 [Ignore]
 public string Id;
    public string Name;
    public string Email;
    [DatabaseName("messages")]
    Messages messages;
    
    public User(string name, string email)
    {
     Id = (name + email).GetHashCode().ToString();
     Name = name;
     Email = email;
     
     messages = new Messages(Id);
    }
}
```

The messages class:
```csharp
[Firebase]
public partial class Messages
{
    public FList<Message> List;
    public struct Message
    {
        public string To;
        public string Content;
        public Message(string to, string content)
        {
	        To = to;
	        Content = content;
        }
    }
    public Messages(string id)
    {
     // Initializes the list and then set its default reading/saving/loading path
     List = new FList<Message>($"---User_Messages-{id}");
    }
}
```
As we can see, these two classes were designed in a way that they were related and easily usable. We see that the immediate advantage of this system is that from there we don't have to worry too much about how to handle things.

Currently the FList type is the only supported collection, but I plan to support more as this lib is adopted, such as arbitrarily sortable dictionaries and collections.

We can register a new user as follows:
```csharp
public async void NewUser(string name, string email)
{
	// name and email validation things here...
	var user = new User(name, email);
	await user.Save("Users/{user.Id}");
}
```
Simple as that, we will have our user saved in the database and ready to be used at any time!

To be able to load a user saved in the database:
```csharp
public async void LoadUserFromId(string id)
{
	var user = await User.Load($"Users/{id}");
	...
}
```
In order to be able, if necessary, to delete a user from the database:
```csharp
public async void DeleteUser(User user)
{
	await user.Delete($"Users/{user.Id}");
}
```
And finally, to be able to use the messages object at will, including sending a message:
```csharp
public async void SendMessageTo(User user, string content, string to)
{
	// Do message processing and validation here...
	var messages = await user.Messages;
	await messages.List.Add(new Message(to, content));
	...
}
```
But what did we do there, exactly?

We can see that the use of asynchronous terms was densely used in the accesses and writings to firebase objects, the reason for this is that the generated methods Save, Load and Delete are all asynchronous in nature, and therefore need to be waited for in their execution.
The same happens with automatically generated properties, like 'Messages', it is actually a ValueTask\<Message\> that needs to be waited for to be used (this has important implications as, the first time the property is called, its value is retrieved from the database, and the second time the cached value is returned until it has somehow been invalidated). This method of doing things makes it lighter and less costly in terms of storage and memory to use entire objects in firebase, since you can simply load the heavier properties on demand (they are not saved inside the objects by themselves, so there is no risk of them being returned in queries that return large collections).

Essentially, this is the ABC's of using this system!

More things to come, stay tuned!
______
### Donations Are Welcome!
In order for my work to be sustainable, please consider contributing a cup of coffee.
BTC: bc1qy009xk2wgve56adrkpqsur7njmjf7tp9rgd6vq

![QR Code BTC](https://github.com/JoaoVictorVP/About-Me/blob/main/QR/BTC-QRCode.PNG)

Monero: 8A2PBhRt7Zn59wvhtLr5jRPGJG1g9pmjt6236qCFdVcHHgPN8vhAD5578n7Np5xKSPCEtUQeWwJmVfps3YsT7uuw1noVzcY

![QR Code Monero](https://github.com/JoaoVictorVP/About-Me/blob/main/QR/Monero-QRCode.png)

Nano: nano_1rd8uiqxws711hn591kac4hotqs6uf73o6wowmns8f8cckudkggykrkdy9ie

![QR Code Nano](https://github.com/JoaoVictorVP/About-Me/blob/main/QR/Nano-QRCode.jpg)

ZCash: zs1xvlegpccccj0krnwp3frqefd8cyzgzpdm53rnz0mmpl98e9zftljxz9ejvus3mwtjj9k54pwqtx

![QR Code ZCash](https://github.com/JoaoVictorVP/About-Me/blob/main/QR/ZCash-QRCode.PNG)

Patreon: [Link https://www.patreon.com/joaovictor_nipah](https://www.patreon.com/joaovictor_nipah)
