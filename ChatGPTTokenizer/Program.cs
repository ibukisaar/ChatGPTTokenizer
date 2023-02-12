using ChatGPTTokenizer;

using var tokenizer = new BpeTokenizer(File.ReadAllText("merges.txt"));
string text = """
    print("Hello world!")
    """;
var tokens = tokenizer.Encode(text);
Console.WriteLine($"count: {tokens.Length}"); // count: 6
Console.WriteLine(string.Join(',', tokens.Select(t => t.Id))); // 4798,7203,15496,995,2474,8
