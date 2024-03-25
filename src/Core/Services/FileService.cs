using Core.ConventionalServices;
using System;
using System.IO;

namespace Core.Services;

public class FileService : IFileService
{
    public FileInfo Get(string path)
        => new FileInfo(path);

    public bool Exists(string path)
        => File.Exists(path);

    public void Create(string filePath, string content)
    {
        using var stream = new StreamWriter(filePath);
        stream.Write(content);
    }
    public void Append(string filePath, string content)
    {
        File.AppendAllText(filePath, content + Environment.NewLine);
    }
    public void Delete(string path)
        => File.Delete(path);

    public string Read(string filePath)
    {
        using var stream = this.ReadStream(filePath);
        return stream.ReadToEnd();
    }

    public StreamReader ReadStream(string filePath)
    {
        if (!this.Exists(filePath))
        {
            var message = $"File '{filePath}' does not exist.";
            throw new InvalidOperationException(message);
        }

        try
        {
            var stream = new StreamReader(filePath);
            return stream;
        }
        catch (IOException e) when ((e.HResult & 0x0000FFFF) == 32)
        {
            var message = $"Cannot read '{filePath}', because the file is open in another program.";
            throw new InvalidOperationException(message);
        }
    }

    public string GetExtension(string path)
        => Path.GetExtension(path);
}

public interface IFileService : ITransientService
{
    FileInfo Get(string path);
    bool Exists(string path);
    public void Create(string filePath, string content);
    public void Append(string filePath, string content);
    public void Delete(string path);
    public string Read(string name);
    StreamReader ReadStream(string filePath);
    string GetExtension(string path);
}
