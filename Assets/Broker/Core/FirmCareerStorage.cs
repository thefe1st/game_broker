using System;
using System.IO;
using System.Text;

namespace Broker.Core
{
    public enum FirmCareerLoadStatus { Missing, Loaded, Unreadable }

    // Read-only v1 import; all new founder writes go to the separate v2 file.
    public sealed class FirmCareerStorage
    {
        public const int MaxBytes = 65536;
        public string SavePath { get; }
        private readonly string legacyPath;
        private readonly Func<FirmCareerSave, string> encode;
        private readonly Func<string, FirmCareerSave> decode;
        public FirmCareerStorage(string path, Func<FirmCareerSave, string> serializer, Func<string, FirmCareerSave> deserializer, string legacyPath = null)
        {
            SavePath = Path.GetFullPath(path); this.legacyPath = legacyPath == null ? null : Path.GetFullPath(legacyPath);
            if (string.Equals(this.legacyPath, SavePath, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Legacy path must be separate.");
            encode = serializer ?? throw new ArgumentNullException(nameof(serializer));
            decode = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        }
        public bool TryLoad(out FirmCareerState state, out string notice, out FirmCareerLoadStatus status)
        {
            state = null; notice = ""; status = FirmCareerLoadStatus.Missing;
            bool legacy = legacyPath != null && !ExistsOrUnreadable(SavePath) && !ExistsOrUnreadable(SavePath + ".bak");
            string source = legacy ? legacyPath : SavePath;
            foreach (var path in new[] { source, source + ".bak" })
            {
                try
                {
                    File.GetAttributes(path); status = FirmCareerLoadStatus.Unreadable;
                    if (!ValidFile(path, out state, legacy ? 1 : 2)) continue;
                    status = FirmCareerLoadStatus.Loaded;
                    if (path != source) notice = "Своя контора восстановлена из резервной копии; основной файл не прочитан. ";
                    if (path != source && !legacy) notice += "Перед наймом восстановите основной файл либо завершите и сохраните день. ";
                    if (legacy) notice += "Загружена копия конторы v1. Найм и новые дни запишутся отдельно в v2; исходные v1 и .bak не меняются.";
                    return true;
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                catch { status = FirmCareerLoadStatus.Unreadable; state = null; }
            }
            if (status == FirmCareerLoadStatus.Unreadable) notice = "Сохранение своей конторы не прочитано. Новое открытие заблокировано; проверьте файл и .bak, затем повторите загрузку. Файлы не изменены.";
            return false;
        }
        public bool TryCreate(FirmCareerState state, out string notice) => Write(state, true, out notice);
        public bool TrySave(FirmCareerState state, out string notice) => Write(state, false, out notice);
        public bool TryHireAssistant(FirmCareerState expected, out FirmCareerState hired, out string notice)
        {
            hired = null; notice = "";
            if (expected == null || !expected.TryHireAssistant(out var candidate))
            { notice = "Найм недоступен: нужен один свободный пост и минимум $200 в сохранённой кассе."; return false; }
            if (!Write(candidate, false, out notice, expected)) return false;
            hired = candidate; return true;
        }
        private bool Write(FirmCareerState state, bool createOnly, out string notice, FirmCareerState expected = null)
        {
            notice = ""; string temporary = SavePath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                if (state == null || !FirmCareerState.TryRestore(state.Capture(), out _)) throw new InvalidDataException();
                string json = encode(state.Capture());
                if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new InvalidDataException();
                var serialized = decode(json);
                if (serialized == null || serialized.schemaVersion != 2 || !FirmCareerState.TryRestore(serialized, out var roundtrip)
                    || !state.SameSnapshot(roundtrip)) throw new InvalidDataException();
                if (createOnly)
                {
                    TryLoad(out _, out notice, out var status);
                    if (status != FirmCareerLoadStatus.Missing)
                    {
                        if (status == FirmCareerLoadStatus.Loaded) notice = "Своя контора уже существует. Повторите загрузку; новое открытие не записано.";
                        return false;
                    }
                }
                if (expected != null)
                {
                    // Recovery is valid for playing, not proof that the primary is unchanged.
                    // A read-only legacy import is safe only while both v2 paths are absent.
                    bool legacy = legacyPath != null && !ExistsOrUnreadable(SavePath) && !ExistsOrUnreadable(SavePath + ".bak");
                    FirmCareerState stored;
                    bool loaded = legacy ? TryLoad(out stored, out _, out _) : ValidFile(SavePath, out stored);
                    if (!loaded || !stored.SameSnapshot(expected))
                    { notice = "Найм не записан: основной файл изменился или не прочитан. Повторите загрузку; после восстановления из .bak сохраните завершённый день перед наймом."; return false; }
                }
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath)); File.WriteAllText(temporary, json, new UTF8Encoding(false));
                bool archived = false;
                if (createOnly) File.Move(temporary, SavePath);
                else if (File.Exists(SavePath))
                {
                    bool valid = ValidFile(SavePath, out _);
                    if (!valid) { File.Copy(SavePath, SavePath + ".unreadable-" + Guid.NewGuid().ToString("N")); archived = true; }
                    File.Replace(temporary, SavePath, valid ? SavePath + ".bak" : null);
                }
                else File.Move(temporary, SavePath);
                notice = archived ? "Контора сохранена. Нечитаемый файл оставлен отдельно (.unreadable-*); .bak сохранена." : "Своя контора сохранена между днями.";
                return true;
            }
            catch { notice = "Не удалось записать свою контору на диск. Повторите запись; текущий запуск не гарантирует сохранение прогресса."; return false; }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
        }
        private static bool ExistsOrUnreadable(string path)
        {
            try { File.GetAttributes(path); return true; }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            catch { return true; }
        }
        private bool ValidFile(string path, out FirmCareerState state, int version = 2)
        {
            state = null;
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length <= 0 || file.Length > MaxBytes) return false;
                var data = decode(File.ReadAllText(path, Encoding.UTF8));
                return data != null && data.schemaVersion == version && FirmCareerState.TryRestore(data, out state);
            }
            catch { state = null; return false; }
        }
    }
}
