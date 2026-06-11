using SuperIronRuby.Parser;

// Prints a preorder dump of the Prism AST for a file, in a format identical to
// Tools/ast_dump.rb (the MRI reference), so the two can be diffed to verify the
// C# deserializer against ruby's own Prism.

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: AstDump FILE");
    return 1;
}

byte[] bytes = File.ReadAllBytes(args[0]);
string source = System.Text.Encoding.UTF8.GetString(bytes);
var result = PrismParser.Parse(source, args[0]);

void Dump(Node? node, int depth)
{
    if (node is null) return;
    Console.WriteLine($"{new string(' ', depth * 2)}{node.Type} @{node.Location.StartOffset},{node.Location.Length}");
    foreach (var child in node.ChildNodes())
        if (child is not null) Dump(child, depth + 1);
}

Dump(result.Root, 0);
Console.WriteLine($"errors: {result.Errors.Count}");
foreach (var e in result.Errors) Console.WriteLine(e.Message);
return 0;
