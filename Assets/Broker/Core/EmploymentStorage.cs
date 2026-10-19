using System;
using System.IO;
using System.Text;

namespace Broker.Core
{
    // Serializer is injected: Unity uses JsonUtility, headless tests use System.Text.Json.
    public sealed class EmploymentStorage
    {
        public const int MaxBytes = 65536;
        public string SavePath { get; }
        private readonly Func<EmploymentSave, string> encode;
        private readonly Func<string, EmploymentSave> decode;
        public EmploymentStorage(string path, Func<EmploymentSave, string> serializer, Func<string, EmploymentSave> deserializer)
        {
            SavePath = Path.GetFullPath(path); encode = serializer ?? throw new ArgumentNullException(nameof(serializer));
            decode = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        }
        public bool TryLoad(out EmploymentState state, out string notice)
        {
            state = null; notice = ""; bool found = false;
            foreach (string path in new[] { SavePath, SavePath + ".bak" })
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    found = true; long size = new FileInfo(path).Length;
                    if (size <= 0 || size > MaxBytes) continue;
                    if (!EmploymentState.TryRestore(decode(File.ReadAllText(path, Encoding.UTF8)), out state)) continue;
                    if (path != SavePath) notice = "Восстановлена резервная копия: основной файл сохранения не прочитан.";
                    return true;
                }
                catch { state = null; }
            }
            if (found) notice = "Сохранение не прочитано или имеет другую версию. Открыта новая вводная глава; старые файлы пока не изменены.";
            return false;
        }
        public bool TrySave(EmploymentState state, out string notice)
        {
            notice = ""; string temporary = SavePath + ".tmp";
            try
            {
                if (state == null || !EmploymentState.TryRestore(state.Capture(), out _)) throw new InvalidDataException();
                string json = encode(state.Capture());
                if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new InvalidDataException();
                // Validate serialized data before replacing the previous save.
                if (!EmploymentState.TryRestore(decode(json), out _)) throw new InvalidDataException();
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                if (File.Exists(SavePath)) File.Replace(temporary, SavePath, SavePath + ".bak");
                else File.Move(temporary, SavePath);
                notice = "Прогресс между днями сохранён."; return true;
            }
            catch { notice = "Не удалось сохранить прогресс на диск. В текущем запуске можно продолжить."; return false; }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
        }
    }
}
