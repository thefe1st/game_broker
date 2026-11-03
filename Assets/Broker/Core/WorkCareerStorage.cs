using System;
using System.IO;
using System.Text;

namespace Broker.Core
{
    public enum WorkCareerLoadStatus { Missing, Loaded, Unreadable }

    // Serializer is injected: Unity uses JsonUtility, headless tests use System.Text.Json.
    // Writes go only to SavePath (v3). Older generations are read-only imports, newest first.
    public sealed class WorkCareerStorage
    {
        public const int MaxBytes = 65536;
        public string SavePath { get; }
        private sealed class Generation { public string FilePath, Label; public int MaxVersion; }
        private readonly Generation[] generations;
        private readonly Func<WorkCareerSave, string> encode;
        private readonly Func<string, WorkCareerSave> decode;
        // legacyPath: previous v2 file (accepts v1/v2 data); olderLegacyPath: original v1 file.
        public WorkCareerStorage(string path, Func<WorkCareerSave, string> serializer, Func<string, WorkCareerSave> deserializer,
            string legacyPath = null, string olderLegacyPath = null)
        {
            SavePath = Path.GetFullPath(path);
            var list = new System.Collections.Generic.List<Generation> { new Generation { FilePath = SavePath, Label = "v3", MaxVersion = WorkCareerState.SchemaVersion } };
            if (legacyPath != null) list.Add(new Generation { FilePath = Path.GetFullPath(legacyPath), Label = "v2", MaxVersion = 2 });
            if (olderLegacyPath != null) list.Add(new Generation { FilePath = Path.GetFullPath(olderLegacyPath), Label = "v1", MaxVersion = 1 });
            for (int i = 0; i < list.Count; i++) for (int j = i + 1; j < list.Count; j++)
                if (string.Equals(list[i].FilePath, list[j].FilePath, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Legacy path must be separate.");
            generations = list.ToArray();
            encode = serializer ?? throw new ArgumentNullException(nameof(serializer));
            decode = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        }
        public bool TryLoad(out WorkCareerState state, out string notice)
            => TryLoad(out state, out notice, out _);
        public bool TryLoad(out WorkCareerState state, out string notice, out WorkCareerLoadStatus status)
        {
            // The newest generation with any file wins. Never roll back to an older one
            // merely because a newer save is broken, unknown, or a directory.
            var generation = Source(); bool legacy = generation != generations[0]; string source = generation.FilePath;
            state = null; notice = ""; status = WorkCareerLoadStatus.Missing; bool found = false;
            foreach (string path in new[] { source, source + ".bak" })
            {
                try
                {
                    if (!ExistsOrUnreadable(path)) continue;
                    if (Directory.Exists(path)) { found = true; continue; }
                    found = true; long size = new FileInfo(path).Length;
                    if (size <= 0 || size > MaxBytes) continue;
                    var data = decode(File.ReadAllText(path, Encoding.UTF8));
                    if (data == null || data.schemaVersion > generation.MaxVersion || !WorkCareerState.TryRestore(data, out state)) continue;
                    if (path != source) notice = "Восстановлена резервная копия: основной файл сохранения не прочитан. ";
                    if (legacy) notice += "Загружена копия " + generation.Label + ". После завершения дня прогресс запишется отдельно в v3; старые "
                        + generation.Label + " и .bak не меняются.";
                    status = WorkCareerLoadStatus.Loaded; return true;
                }
                catch { found = true; state = null; }
            }
            if (found)
            {
                status = WorkCareerLoadStatus.Unreadable;
                notice = "Сохранение не прочитано или имеет другую версию. Новый перенос заблокирован; файлы не изменены. Проверьте сохранение и .bak, затем повторите загрузку.";
            }
            return false;
        }
        public bool TrySave(WorkCareerState state, out string notice)
            => TryWrite(state, false, out notice);
        public bool TryCreate(WorkCareerState state, out string notice)
            => TryWrite(state, true, out notice);
        private bool TryWrite(WorkCareerState state, bool createOnly, out string notice)
        {
            notice = ""; string temporary = SavePath + (createOnly ? ".tmp-" + Guid.NewGuid().ToString("N") : ".tmp"); bool preservedUnreadable = false;
            try
            {
                if (state == null || !WorkCareerState.TryRestore(state.Capture(), out _)) throw new InvalidDataException();
                string json = encode(state.Capture());
                if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new InvalidDataException();
                // Validate serialized data before replacing the previous save.
                if (!WorkCareerState.TryRestore(decode(json), out _)) throw new InvalidDataException();
                if (createOnly)
                {
                    TryLoad(out _, out notice, out var status);
                    if (status != WorkCareerLoadStatus.Missing)
                    {
                        if (status == WorkCareerLoadStatus.Loaded) notice = "Продолжение уже существует. Повторите загрузку; новый перенос не записан.";
                        return false;
                    }
                }
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                // Creation must never replace a primary that appeared after the load check.
                if (createOnly) File.Move(temporary, SavePath);
                else if (File.Exists(SavePath))
                {
                    // When the primary is damaged, retain the last usable backup instead of
                    // backing up the corrupt bytes on top of it during the first recovery save.
                    bool validPrimary = IsValidFile(SavePath);
                    if (!validPrimary)
                    {
                        // Also preserve unknown-version files byte-for-byte. If copying fails,
                        // abort the write rather than silently discard the user's old progress.
                        File.Copy(SavePath, SavePath + ".unreadable-" + Guid.NewGuid().ToString("N"));
                        preservedUnreadable = true;
                    }
                    File.Replace(temporary, SavePath, validPrimary ? SavePath + ".bak" : null);
                }
                else File.Move(temporary, SavePath);
                notice = preservedUnreadable ? "Прогресс сохранён. Прежний нечитаемый файл оставлен отдельно (.unreadable-*); резервная копия сохранена." : "Прогресс между днями сохранён."; return true;
            }
            catch { notice = "Не удалось сохранить прогресс на диск. В текущем запуске можно продолжить."; return false; }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
        }
        private Generation Source()
        {
            foreach (var g in generations) if (ExistsOrUnreadable(g.FilePath) || ExistsOrUnreadable(g.FilePath + ".bak")) return g;
            return generations[0];
        }
        private static bool ExistsOrUnreadable(string path)
        {
            try { File.GetAttributes(path); return true; }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            catch { return true; }
        }
        private bool IsValidFile(string path)
        {
            try
            {
                var file = new FileInfo(path);
                return file.Exists && file.Length > 0 && file.Length <= MaxBytes
                    && WorkCareerState.TryRestore(decode(File.ReadAllText(path, Encoding.UTF8)), out _);
            }
            catch { return false; }
        }

    }
}
