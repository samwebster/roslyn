using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TestCompiler
{
    class Program
    {
        static void Main(string[] args)
        {
            var program_text = @"

using System;
using System.Threading.Tasks;

while (true)
{
    await Task1();
    Check(arrayInMem: false);
    Console.Write(""."");
}

static void Check(bool arrayInMem)
{
    var totalMem = GC.GetTotalMemory(true);
    if (totalMem > 1_000_000_000 ^ arrayInMem ||
        Program.WeakRef.TryGetTarget(out _) ^ arrayInMem)
        throw new Exception();
}

static async Task<int> Task1()
{
    var result = await Task2();
    // this shows reversed stack -> Console.WriteLine(Environment.StackTrace);
    // At this point the array should be collectable but isn't
    Check(arrayInMem: true);
    await Task.Yield(); // prevents mem leaking to Main
    return result;
}

static async Task<int> Task2()
{
    Check(arrayInMem: false);
    var localMem = new byte[1_000_000_000];
    Program.WeakRef.SetTarget(localMem);
    Check(arrayInMem: true);
    return await Task3(localMem);
}

static async Task<int> Task3(byte[] mem)
{
    await Task.Delay(1);
    return mem[0];
}

public partial class Program
{
    static WeakReference<byte[]?> WeakRef = new WeakReference<byte[]?>(null);
}
            ";

            var syntax_tree = CSharpSyntaxTree.ParseText(program_text);

            var compilation = CSharpCompilation.Create(
                Guid.NewGuid().ToString("D"),
                new[] { syntax_tree },
                new[] {
                    MetadataReference.CreateFromFile(@"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5.2\mscorlib.dll"),
                    MetadataReference.CreateFromFile(@"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5.2\System.dll")
                },
                options: new CSharpCompilationOptions(OutputKind.ConsoleApplication, optimizationLevel: OptimizationLevel.Release));

            var ms = new MemoryStream();
            Console.WriteLine(compilation.Options.OptimizationLevel);
            var emit_result = compilation.Emit(ms);
            var bytes = ms.ToArray();
            File.WriteAllBytes(@"c:\work\asyncreverse\asyncreverse.exe", bytes);
        }
    }
}
