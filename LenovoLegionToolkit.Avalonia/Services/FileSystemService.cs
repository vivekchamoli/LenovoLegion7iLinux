using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;

namespace LenovoLegionToolkit.Avalonia.Services
{
    public class FileSystemService : IFileSystemService
    {
        private readonly IFileSystem _fileSystem;

        public IFileSystem FileSystem => _fileSystem;

        public FileSystemService() : this(new FileSystem())
        {
        }

        public FileSystemService(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public async Task<string> ReadFileAsync(string path)
        {
            return await _fileSystem.File.ReadAllTextAsync(path);
        }

        public async Task WriteFileAsync(string path, string content)
        {
            await _fileSystem.File.WriteAllTextAsync(path, content);
        }

        public async Task<string[]> ReadAllLinesAsync(string path)
        {
            return await _fileSystem.File.ReadAllLinesAsync(path);
        }

        public async Task WriteAllLinesAsync(string path, IEnumerable<string> lines)
        {
            await _fileSystem.File.WriteAllLinesAsync(path, lines);
        }

        public bool FileExists(string path)
        {
            return _fileSystem.File.Exists(path);
        }

        public bool DirectoryExists(string path)
        {
            return _fileSystem.Directory.Exists(path);
        }

        public string[] GetFiles(string path, string searchPattern = "*", bool recursive = false)
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return _fileSystem.Directory.GetFiles(path, searchPattern, searchOption);
        }

        public string[] GetDirectories(string path)
        {
            return _fileSystem.Directory.GetDirectories(path);
        }

        public void CreateDirectory(string path)
        {
            _fileSystem.Directory.CreateDirectory(path);
        }

        public void DeleteFile(string path)
        {
            _fileSystem.File.Delete(path);
        }

        public void DeleteDirectory(string path, bool recursive = false)
        {
            _fileSystem.Directory.Delete(path, recursive);
        }

        public string CombinePath(params string[] paths)
        {
            return _fileSystem.Path.Combine(paths);
        }

        public string GetDirectoryName(string path)
        {
            return _fileSystem.Path.GetDirectoryName(path) ?? string.Empty;
        }

        public string GetFileName(string path)
        {
            return _fileSystem.Path.GetFileName(path);
        }

        public string GetFileNameWithoutExtension(string path)
        {
            return _fileSystem.Path.GetFileNameWithoutExtension(path);
        }
    }
}