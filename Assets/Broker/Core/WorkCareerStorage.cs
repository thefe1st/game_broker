using System;
using System.IO;
using System.Text;

namespace Broker.Core
{
    // Serializer is injected: Unity uses JsonUtility, headless tests use System.Text.Json.
    public sealed class WorkCareerStorage
    {
        public const int MaxBytes = 65536;
        public string SavePath { get; }
        private readonly string legacyPath;
        private readonly Func<WorkCareerSave, string> encode;
        private readonly Func<string, WorkCareerSave> decode;
        public WorkCareerStorage(string path, Func<WorkCareerSave, string> serializer, Func<string, WorkCareerSave> deserializer, string legacyPath = null)
        {
            SavePath = Path.GetFullPath(path); this.legacyPath = legacyPath == null ? null : Path.GetFullPath(legacyPath);
            if (this.legacyPath == SavePath) throw new ArgumentException("Legacy path must be separate.");
            encode = serializer ?? throw new ArgumentNullException(nameof(serializer));
            decode = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        }
        public bool TryLoad(out WorkCareerState state, out string notice)
        {
            // Never roll back to v1 merely because a newer save is broken, unknown, or a directory.
            bool newerExists = File.Exists(SavePath) || File.Exists(SavePath + ".bak")
                || Directory.Exists(SavePath) || Directory.Exists(SavePath + ".bak");
            bool legacy = !newerExists && legacyPath != null;
            string source = legacy ? legacyPath : SavePath;
            state = null; notice = ""; bool found = false;
            foreach (string path in new[] { source, source + ".bak" })
            {
                try
                {
                    if (Directory.Exists(path)) { found = true; continue; }
                    if (!File.Exists(path)) continue;
                    found = true; long size = new FileInfo(path).Length;
                    if (size <= 0 || size > MaxBytes) continue;
                    var data = decode(File.ReadAllText(path, Encoding.UTF8));
                    if ((legacy && (data == null || data.schemaVersion != 1)) || !WorkCareerState.TryRestore(data, out state)) continue;
                    if (path != source) notice = "Восстановлена резервная копия: основной файл сохранения не прочитан. ";
                    if (legacy) notice += "Загружена копия v1. После завершения дня прогресс запишется отдельно в v2; старые v1 и .bak не меняются.";
                    return true;
                }
                catch { state = null; }
            }
            if (found) notice = "Сохранение не прочитано или имеет другую версию. Продолжение не загружено; старые файлы пока не изменены.";
            return false;
        }
        public bool TrySave(WorkCareerState state, out string notice)
        {
            notice = ""; string temporary = SavePath + ".tmp"; bool preservedUnreadable = false;
            try
            {
                if (state == null || !WorkCareerState.TryRestore(state.Capture(), out _)) throw new InvalidDataException();
                string json = encode(state.Capture());
                if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new InvalidDataException();
                // Validate serialized data before replacing the previous save.
                if (!WorkCareerState.TryRestore(decode(json), out _)) throw new InvalidDataException();
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                if (File.Exists(SavePath))
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
