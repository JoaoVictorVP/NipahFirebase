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
}