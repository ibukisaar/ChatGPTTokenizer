using ChatGPTTokenizer;

using var tokenizer = new BpeTokenizer(File.ReadAllText("merges.txt"));
string text = """
    print("Hello world!")
    """;
var tokens = tokenizer.Encode(text);
Console.WriteLine($"count: {tokens.Length}");
Console.WriteLine(string.Join(',', tokens.Select(t => t.Id)));
