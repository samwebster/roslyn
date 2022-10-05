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

// <copyright file=""Program.cs"" company=""Microsoft"">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

#nullable enable

using System;
using System.Threading.Tasks;

public class Program
{
    private static WeakReference<byte[]?> weakRef = new WeakReference<byte[]?>(null);

    public static void Main()
    {
        RunAsync().GetAwaiter().GetResult();
    }

    public static async Task RunAsync()
    {
        while (true)
        {
            await Task1Async();
            Check(arrayInMem: false);
            Console.Write(""."");
        }
    }

    private static void Check(bool arrayInMem)
    {
        var totalMem = GC.GetTotalMemory(true);
        if (totalMem > 1_000_000_000 ^ arrayInMem ||
            Program.weakRef.TryGetTarget(out _) ^ arrayInMem)
        {
            throw new Exception();
        }
    }

    private static async Task<int> Task1Async()
    {
        var result = await Task2Async();

        // this shows reversed stack -> Console.WriteLine(Environment.StackTrace);
        // At this point the array should be collectable but isn't
        Check(arrayInMem: true);
        await Task.Yield(); // prevents mem leaking to Main
        return result;
    }

    private static async Task<int> Task2Async()
    {
        Check(arrayInMem: false);
        var localMem = new byte[1_000_000_000];
        Program.weakRef.SetTarget(localMem);
        Check(arrayInMem: true);
        return await Task3Async(localMem);
    }

    private static async Task<int> Task3Async(byte[] mem)
    {
        await Task.Delay(1);
        return mem[0];
    }
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
