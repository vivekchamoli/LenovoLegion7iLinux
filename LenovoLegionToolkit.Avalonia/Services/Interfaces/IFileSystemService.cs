using System.Collections.Generic;
using System.IO.Abstractions;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Avalonia.Services.Interfaces
{
    public interface IFileSystemService
    {
        IFileSystem FileSystem { get; }

        Task<string> ReadFileAsync(string path);
        Task WriteFileAsync(string path, string content);
        Task<string[]> ReadAllLinesAsync(string path);
        Task WriteAllLinesAsync(string path, IEnumerable<string> lines);

        bool FileExists(string path);
        bool DirectoryExists(string path);

        string[] GetFiles(string path, string searchPattern = "*", bool recursive = false);
        string[] GetDirectories(string path);

        void CreateDirectory(string path);
        void DeleteFile(string path);
        void DeleteDirectory(string path, bool recursive = false);

        string CombinePath(params string[] paths);
        string GetDirectoryName(string path);
        string GetFileName(string path);
        string GetFileNameWithoutExtension(string path);
    }
}