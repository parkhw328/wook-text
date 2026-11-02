using WookText.Core.Pdf;

if (args.Length > 0)
{
    await Console.OpenStandardInput().CopyToAsync(Stream.Null);
    if (args[0] == "exit") return 17;
    if (args[0] == "invalid") { Console.Write("invalid response"); return 0; }
    if (args[0] == "hang")
    {
        if (args.Length > 1) await File.WriteAllTextAsync(args[1], Environment.ProcessId.ToString());
        await Task.Delay(Timeout.Infinite);
    }
}
return await PdfWorker.RunAsync(Console.OpenStandardInput(), Console.OpenStandardOutput());
