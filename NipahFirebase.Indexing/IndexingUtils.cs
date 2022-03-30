namespace NipahFirebase.Indexing;

public static class IndexingUtils
{
	public static readonly string Indexes = Uri.UnescapeDataString("!Indexes"),
		Index = Uri.UnescapeDataString("!Index"),
		Final = Uri.UnescapeDataString("!Final");


    static char[] separators = new[] { ' ', ',', '.', '!', ':', '/', '\\', '\n', '*', '+', '-', '#', '@', '$', '%', '&', '?' };
    
    public static string[] SplitWithSeparators(string input) => input.Replace("\r", "").Split(separators, StringSplitOptions.RemoveEmptyEntries);

    public static void SmartPermuteString(string input, List<string> perms)
    {
		HashSet<string> perma = new(32);

		string[] tokens = input.Replace("\r", "").Split(separators, StringSplitOptions.RemoveEmptyEntries);

		Action<string> addable = v => perma.Add(v);
		foreach (var token in tokens)
			PermuteString(token, addable);

		perms.AddRange(perma);

		foreach (var c in input)
		{
			string cS = c.ToString();
			if (tokens.Contains(cS))
				perms.Add(cS);
		}
	}
    
    public static void PermuteString(string input, Action<string> addPerms)
    {
		int size = input.Length;
		int startOn = 0;
	loop:
		if (startOn < size)
		{
			int nextOn = startOn;
			string cur = "";

		nloop:
			if (nextOn >= size) { startOn++; goto loop; }
			cur += input[nextOn];
			addPerms(cur);
			nextOn++;
			goto nloop;
		}
	}

	public static int GetDeterministicHashCode(this string str)
	{
		unchecked
		{
			int hash1 = (5381 << 16) + 5381;
			int hash2 = hash1;

			for (int i = 0; i < str.Length; i += 2)
			{
				hash1 = ((hash1 << 5) + hash1) ^ str[i];
				if (i == str.Length - 1)
					break;
				hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
			}

			return hash1 + (hash2 * 1566083941);
		}
	}
}