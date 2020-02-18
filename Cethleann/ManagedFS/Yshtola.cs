using System;
using System.Collections.Generic;
using System.IO;
using Cethleann.ManagedFS.Support;
using Cethleann.Ninja;
using Cethleann.Structure;
using JetBrains.Annotations;

namespace Cethleann.ManagedFS
{
    /// <summary>
    ///     Manages TN files
    /// </summary>
    [PublicAPI]
    public class Yshtola : IManagedFS
    {
        /// <summary>
        ///     Initialize with standard data.
        /// </summary>
        /// <param name="gameId"></param>
        /// <param name="root"></param>
        /// <param name="settings"></param>
        public Yshtola(DataGame gameId, string root, YshtolaSettings settings)
        {
            GameId = gameId;
            Settings = settings;
            Root = root;

            foreach (var tableName in settings.TableNames) AddDataFS(tableName);
        }

        public Dictionary<string, string> FileList { get; set; } = new Dictionary<string, string>();

        /// <summary>
        ///     Settings to use for decryption and loading.
        /// </summary>
        public YshtolaSettings Settings { get; }

        /// <summary>
        ///     ID Table
        /// </summary>
        public List<PackageTable> Tables { get; set; } = new List<PackageTable>();

        /// <summary>
        ///     Root directory, the one that contains COMMON.
        /// </summary>
        public string Root { get; set; }

        /// <inheritdoc />
        public void Dispose() { }

        /// <inheritdoc />
        public int EntryCount { get; private set; }

        /// <inheritdoc />
        public DataGame GameId { get; }

        /// <inheritdoc />
        public Memory<byte> ReadEntry(int index)
        {
            foreach (var table in Tables)
            {
                if (index < table.Entries.Length) return new Memory<byte>(table.Read(File.ReadAllBytes(Path.Combine(Root, table.Entries[index].Path(table.Buffer, table.Header.Offset))), table.Entries[index], Settings.XorTruth, Settings.Multiplier, Settings.Divisor).ToArray());
                index -= table.Entries.Length;
            }

            return Memory<byte>.Empty;
        }

        /// <inheritdoc />
        public Dictionary<string, string> LoadFileList(string filename = null, DataGame? game = null)
        {
            FileList = ManagedFSHelper.GetSimpleFileList(filename, game ?? GameId, "pkginfo");
            return FileList;
        }

        /// <inheritdoc />
        public string GetFilename(int index, string ext = "bin", DataType dataType = DataType.None)
        {
            foreach (var table in Tables)
            {
                if (index < table.Entries.Length)
                {
                    var entry = table.Entries[index];
                    if (entry.OriginalPathOffset > -1)
                        return entry.OriginalPath(table.Buffer, table.Header.Offset);

                    var path = entry.Path(table.Buffer, table.Header.Offset);
                    if (!FileList.TryGetValue(path, out var resultPath)) resultPath = path + $".{ext}";
                    return resultPath;
                }

                index -= table.Entries.Length;
            }

            throw new ArgumentOutOfRangeException();
        }

        /// <inheritdoc />
        public void AddDataFS(string path)
        {
            var tablePath = Path.Combine(Root, path);
            if (!File.Exists(tablePath)) return;
            var table = new PackageTable(File.ReadAllBytes(tablePath), IDTableFlags.Compressed | IDTableFlags.Encrypted, Settings.XorTruth, Settings.Multiplier, Settings.Divisor);
            Tables.Add(table);
            EntryCount += table.Entries.Length;
        }
    }
}
