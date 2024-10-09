using NTS.Domain.Setup.Entities;
using NTS.Judge.Blazor.Ports;
using System.Diagnostics;

namespace NTS.Judge.Adapters;

public class TagWriterBehind : ITagWrite
{
    public async Task<Tag> WriteAsync(int number)
    {
        string output = String.Empty;
        string error = String.Empty;
        ProcessStartInfo processStartInfo = new ProcessStartInfo
        {
            FileName = @"C:\Work\not-timing-system\src-v2\NTS.TagRfidControllers\bin\Debug\net8.0\NTS.TagRfidControllers.exe",
            Arguments = number.ToString(),

            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using (Process process = Process.Start(processStartInfo))
            {
                output = await process.StandardOutput.ReadToEndAsync();
                error = await process.StandardError.ReadToEndAsync();

                process.WaitForExit();
            }
        }
        catch
        {
            // Log error.
        }
        string tagId = output.Split(" ").Last();
        return new Tag(tagId, number);
    }
}
