using System;
using System.IO;
using System.Text.Json;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class FirmCareerStorageTests
    {
        private string folder, path;
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
        private static string Encode(FirmCareerSave s) => JsonSerializer.Serialize(s, Options);
        private static FirmCareerSave Decode(string json) => JsonSerializer.Deserialize<FirmCareerSave>(json, Options);
        private FirmCareerStorage Store() => new FirmCareerStorage(path, Encode, Decode);
        [SetUp] public void Setup()
        { folder = Path.Combine(Path.GetTempPath(), "broker-firm-" + Guid.NewGuid()); Directory.CreateDirectory(folder); path = Path.Combine(folder, "broker-founder-v3.json"); }
        [TearDown] public void Cleanup() { Directory.Delete(folder, true); }
        [Test] public void MissingFileDoesNotWriteAnything()
        {
            Assert.That(Store().TryLoad(out var state, out var notice, out var status), Is.False);
            Assert.That(state, Is.Null); Assert.That(notice, Is.Empty); Assert.That(status, Is.EqualTo(FirmCareerLoadStatus.Missing));
            Assert.That(Directory.GetFiles(folder), Is.Empty);
        }
        [Test] public void CreateAndPrunedRoundTripKeepReserveAndFirmMoneySeparate()
        {
            var firm = FirmCareerTests.NewFirm(); Assert.That(Store().TryCreate(firm, out _), Is.True);
            CollectionAssert.AreEquivalent(new[] { path }, Directory.GetFiles(folder));
            for (int n = 0; n < 20; n++) FirmCareerTests.Finish(firm, n % 2 == 0);
            Assert.That(Store().TrySave(firm, out _), Is.True); Assert.That(Store().TryLoad(out var loaded, out _, out var status), Is.True);
            Assert.That(status, Is.EqualTo(FirmCareerLoadStatus.Loaded)); Assert.That(loaded.CompletedDays, Is.EqualTo(20));
            Assert.That(loaded.Balance, Is.EqualTo(firm.Balance)); Assert.That(loaded.PersonalReserve, Is.EqualTo(firm.PersonalReserve));
            Assert.That(loaded.NextDayNumber, Is.EqualTo(firm.NextDayNumber)); Assert.That(loaded.RecentReports.Length, Is.EqualTo(7));
            Assert.That(new FileInfo(path).Length, Is.LessThan(20000));
        }
        [TestCase(false, "valid")] [TestCase(true, "valid")]
        [TestCase(false, "invalid")] [TestCase(true, "invalid")]
        [TestCase(false, "directory")] [TestCase(true, "directory")]
        public void OpeningCannotOverwriteExistingFileOrBackup(bool backup, string kind)
        {
            string source = path + (backup ? ".bak" : "");
            string bytes = kind == "valid" ? Encode(FirmCareerTests.NewFirm().Capture()) : "future progress";
            if (kind == "directory") Directory.CreateDirectory(source); else File.WriteAllText(source, bytes);
            var files = Directory.GetFiles(folder);
            Assert.That(Store().TryCreate(FirmCareerTests.NewFirm(), out var notice), Is.False); Assert.That(notice, Is.Not.Empty);
            CollectionAssert.AreEquivalent(files, Directory.GetFiles(folder));
            if (kind == "directory") Assert.That(Directory.Exists(source), Is.True); else Assert.That(File.ReadAllText(source), Is.EqualTo(bytes));
            Assert.That(Store().TryLoad(out _, out _, out var status), Is.EqualTo(kind == "valid"));
            Assert.That(status, Is.EqualTo(kind == "valid" ? FirmCareerLoadStatus.Loaded : FirmCareerLoadStatus.Unreadable));
        }
        [TestCase("")] [TestCase("{bad")] [TestCase("{}")] [TestCase("{\"schemaVersion\":99}")]
        public void UnreadableLoadPreservesBytesAndBlocksOpening(string bytes)
        {
            File.WriteAllText(path, bytes); Assert.That(Store().TryLoad(out _, out var notice, out var status), Is.False);
            Assert.That(status, Is.EqualTo(FirmCareerLoadStatus.Unreadable)); Assert.That(notice, Does.Contain("заблокировано"));
            Assert.That(Store().TryCreate(FirmCareerTests.NewFirm(), out _), Is.False); Assert.That(File.ReadAllText(path), Is.EqualTo(bytes));
        }
        [Test] public void BackupRecoveryThenSaveArchivesDamagedPrimaryWithoutReplacingBackup()
        {
            var firm = FirmCareerTests.NewFirm(); Store().TryCreate(firm, out _); FirmCareerTests.Finish(firm, true); Store().TrySave(firm, out _);
            string backup = File.ReadAllText(path + ".bak"); File.WriteAllText(path, "damaged bytes");
            Assert.That(Store().TryLoad(out firm, out var notice, out _), Is.True); Assert.That(notice, Does.Contain("резервной"));
            Assert.That(firm.CompletedDays, Is.Zero); Assert.That(Store().TrySave(firm, out _), Is.True);
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(backup));
            var archives = Directory.GetFiles(folder, "*.unreadable-*"); Assert.That(archives.Length, Is.EqualTo(1));
            Assert.That(File.ReadAllText(archives[0]), Is.EqualTo("damaged bytes"));
        }
        [Test] public void AllOldSaveModesAreRejectedAsFounder()
        {
            foreach (var data in new object[] { new CareerState().Capture(), WorkCareerTests.Intro().Capture(), FirmCareerTests.ReadyWork().Capture() })
            {
                File.WriteAllText(path, JsonSerializer.Serialize(data, Options));
                Assert.That(Store().TryLoad(out _, out _, out var status), Is.False); Assert.That(status, Is.EqualTo(FirmCareerLoadStatus.Unreadable));
            }
        }
        [Test] public void FounderSaveAndResetNeverTouchOtherFilesOrTheirBackups()
        {
            var paths = new[] { "broker-career-v1.json", "broker-employee-intro-v1.json", "broker-employee-work-v1.json", "broker-employee-work-v2.json",
                "broker-employee-work-v3.json", "broker-founder-v1.json", "broker-founder-v2.json" };
            foreach (string name in paths) { File.WriteAllText(Path.Combine(folder, name), name); File.WriteAllText(Path.Combine(folder, name + ".bak"), name + ".bak"); }
            var firm = FirmCareerTests.NewFirm(); Store().TryCreate(firm, out _); FirmCareerTests.Finish(firm); Store().TrySave(firm, out _);
            Store().TrySave(firm.RestartFromOrigin(), out _);
            foreach (string name in paths) { Assert.That(File.ReadAllText(Path.Combine(folder, name)), Is.EqualTo(name)); Assert.That(File.ReadAllText(Path.Combine(folder, name + ".bak")), Is.EqualTo(name + ".bak")); }
        }
        [TestCase("throw")] [TestCase("empty")] [TestCase("invalid")] [TestCase("oversized")] [TestCase("decodeThrow")]
        public void BadSerializerDoesNotChangeHealthyProgress(string kind)
        {
            var firm = FirmCareerTests.NewFirm(); Store().TryCreate(firm, out _); string before = File.ReadAllText(path);
            var store = new FirmCareerStorage(path, s => kind == "throw" ? throw new Exception() : kind == "empty" ? ""
                : kind == "invalid" ? "{}" : kind == "oversized" ? new string('x', FirmCareerStorage.MaxBytes + 1) : Encode(s),
                json => kind == "decodeThrow" ? throw new Exception() : Decode(json));
            Assert.That(store.TrySave(firm, out _), Is.False); Assert.That(File.ReadAllText(path), Is.EqualTo(before));
            CollectionAssert.AreEquivalent(new[] { path }, Directory.GetFiles(folder));
        }
        [Test] public void OversizedInputIsUnreadableAndCannotBeOverwrittenByCreate()
        {
            string bytes = new string('x', FirmCareerStorage.MaxBytes + 1); File.WriteAllText(path, bytes);
            Assert.That(Store().TryLoad(out _, out _, out var status), Is.False); Assert.That(status, Is.EqualTo(FirmCareerLoadStatus.Unreadable));
            Assert.That(Store().TryCreate(FirmCareerTests.NewFirm(), out _), Is.False); Assert.That(File.ReadAllText(path), Is.EqualTo(bytes));
        }
        [Test] public void FailedCreationCanRetryAndDoesNotTouchUnrelatedTemporaryFile()
        {
            var firm = FirmCareerTests.NewFirm(); Directory.CreateDirectory(path); File.WriteAllText(path + ".tmp", "old interrupted write");
            Assert.That(Store().TryCreate(firm, out _), Is.False); Directory.Delete(path);
            Assert.That(Store().TryCreate(firm, out _), Is.True); Assert.That(File.ReadAllText(path + ".tmp"), Is.EqualTo("old interrupted write"));
            Assert.That(Directory.GetFiles(folder, "*.tmp-*").Length, Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void FirstOpeningRechecksFilesAfterEncoding(bool backup)
        {
            string source = path + (backup ? ".bak" : "");
            var store = new FirmCareerStorage(path, s => { File.WriteAllText(source, "appearing bytes"); return Encode(s); }, Decode);
            Assert.That(store.TryCreate(FirmCareerTests.NewFirm(), out _), Is.False);
            Assert.That(File.ReadAllText(source), Is.EqualTo("appearing bytes")); CollectionAssert.AreEquivalent(new[] { source }, Directory.GetFiles(folder));
        }
        [Test] public void NullStateCannotBeSavedOrCreated()
        { Assert.That(Store().TrySave(null, out _), Is.False); Assert.That(Store().TryCreate(null, out _), Is.False); Assert.That(Directory.GetFiles(folder), Is.Empty); }
        private string LegacyPath => Path.Combine(folder, "broker-founder-v1.json");
        private string V2Path => Path.Combine(folder, "broker-founder-v2.json");
        private FirmCareerStorage MigratingStore() => new FirmCareerStorage(path, Encode, Decode, V2Path, LegacyPath);
        // Real v1/v2 JSON has no stock fields at all.
        private static System.Text.Json.Nodes.JsonNode WithoutStocks(FirmCareerState firm, int version)
        {
            var json = System.Text.Json.Nodes.JsonNode.Parse(Encode(firm.Capture())); json["schemaVersion"] = version; json.AsObject().Remove("stockHistoryFromDay");
            foreach (string key in new[] { "origin", "checkpoint" })
                foreach (var client in json[key]["clients"].AsArray()) { client.AsObject().Remove("lastStockId"); client.AsObject().Remove("lastStockDay"); }
            foreach (var report in json["reports"].AsArray()) report.AsObject().Remove("soldStockIds");
            return json;
        }
        private void WriteLegacy(bool backup = false)
        {
            // Real v1 JSON has no employee fields, rather than explicit zero values.
            var json = WithoutStocks(FirmCareerTests.NewFirm(), 1); json.AsObject().Remove("assistantHireDay");
            foreach (var report in json["reports"].AsArray()) report.AsObject().Remove("salary");
            File.WriteAllText(LegacyPath + (backup ? ".bak" : ""), json.ToJsonString());
        }
        [Test] public void V2ImportWinsOverV1KeepsHireAndWritesOnlyV3()
        {
            WriteLegacy(); var firm = FirmCareerTests.NewFirm(); firm.TryHireAssistant(out firm);
            for (int n = 0; n < 9; n++) FirmCareerTests.Finish(firm, true);
            string v2 = WithoutStocks(firm, 2).ToJsonString(); File.WriteAllText(V2Path, v2); File.WriteAllText(V2Path + ".bak", v2);
            var files = Directory.GetFiles(folder); var bytes = Array.ConvertAll(files, File.ReadAllText);
            var store = MigratingStore(); Assert.That(store.TryLoad(out var loaded, out var notice, out _), Is.True);
            Assert.That(notice, Does.Contain("копия конторы v2")); Assert.That(notice, Does.Contain("в v3")); Assert.That(File.Exists(path), Is.False);
            Assert.That(loaded.HasAssistant, Is.True); Assert.That(loaded.Balance, Is.EqualTo(firm.Balance)); Assert.That(loaded.StockHistoryFromDay, Is.EqualTo(firm.NextDayNumber));
            Assert.That(store.TryCreate(FirmCareerTests.NewFirm(), out _), Is.False);
            FirmCareerTests.Finish(loaded, true); Assert.That(store.TrySave(loaded, out _), Is.True);
            Assert.That(store.TryLoad(out var read, out notice, out _), Is.True); Assert.That(notice, Is.Empty);
            Assert.That(read.LastReport.soldStockIds[0], Is.EqualTo(StockCatalog.DefaultFor(read.LastReport.clientIds[0])));
            Assert.That(Decode(File.ReadAllText(path)).schemaVersion, Is.EqualTo(3));
            for (int i = 0; i < files.Length; i++) Assert.That(File.ReadAllText(files[i]), Is.EqualTo(bytes[i]));
        }
        [Test] public void HireFromV2ImportWritesOnlyV3()
        {
            var firm = FirmCareerTests.NewFirm(); FirmCareerTests.Finish(firm, true);
            string v2 = WithoutStocks(firm, 2).ToJsonString(); File.WriteAllText(V2Path, v2);
            var store = MigratingStore(); Assert.That(store.TryLoad(out var loaded, out _, out _), Is.True);
            Assert.That(store.TryHireAssistant(loaded, out var hired, out _), Is.True); Assert.That(hired.HasAssistant, Is.True);
            Assert.That(store.TryLoad(out var read, out _, out _), Is.True); Assert.That(read.HasAssistant, Is.True);
            Assert.That(File.ReadAllText(V2Path), Is.EqualTo(v2)); CollectionAssert.AreEquivalent(new[] { path, V2Path }, Directory.GetFiles(folder));
        }
        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void UnreadableV2FounderNeverFallsBackToV1(bool backup, bool directory)
        {
            WriteLegacy(); string p = V2Path + (backup ? ".bak" : "");
            if (directory) Directory.CreateDirectory(p); else File.WriteAllText(p, Encode(FirmCareerTests.NewFirm().Capture()));
            Assert.That(MigratingStore().TryLoad(out var state, out _, out var status), Is.False);
            Assert.That(state, Is.Null); Assert.That(status, Is.EqualTo(FirmCareerLoadStatus.Unreadable));
            Assert.That(MigratingStore().TryCreate(FirmCareerTests.NewFirm(), out _), Is.False); Assert.That(File.Exists(path), Is.False);
        }
        [TestCase(false)] [TestCase(true)]
        public void LegacyLoadHireSaveAndResetPreserveOriginalBytes(bool backup)
        {
            WriteLegacy(backup); if (backup) File.WriteAllText(LegacyPath, "damaged legacy");
            var files = Directory.GetFiles(folder); var bytes = Array.ConvertAll(files, File.ReadAllText);
            var store = MigratingStore(); Assert.That(store.TryLoad(out var firm, out var notice, out _), Is.True);
            Assert.That(notice, Does.Contain("копия конторы v1")); Assert.That(File.Exists(path), Is.False);
            Assert.That(store.TryCreate(FirmCareerTests.NewFirm(), out _), Is.False);
            Assert.That(store.TryHireAssistant(firm, out firm, out _), Is.True); Assert.That(firm.Balance, Is.EqualTo(200));
            Assert.That(store.TryLoad(out firm, out _, out _), Is.True); Assert.That(firm.HasAssistant, Is.True);
            FirmCareerTests.Finish(firm); Assert.That(store.TrySave(firm, out _), Is.True);
            Assert.That(store.TrySave(firm.RestartFromOrigin(), out _), Is.True);
            Assert.That(store.TryLoad(out firm, out _, out _), Is.True); Assert.That(firm.HasAssistant, Is.False);
            for (int i = 0; i < files.Length; i++) Assert.That(File.ReadAllText(files[i]), Is.EqualTo(bytes[i]));
        }
        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void UnreadableNewerFileNeverFallsBackToHealthyLegacy(bool backup, bool directory)
        {
            WriteLegacy(); string p = path + (backup ? ".bak" : "");
            if (directory) Directory.CreateDirectory(p); else File.WriteAllText(p, "future newer");
            Assert.That(MigratingStore().TryLoad(out _, out _, out var status), Is.False);
            Assert.That(status, Is.EqualTo(FirmCareerLoadStatus.Unreadable));
            Assert.That(MigratingStore().TryCreate(FirmCareerTests.NewFirm(), out _), Is.False);
        }
        [TestCase(false)] [TestCase(true)]
        public void UnknownLegacyFileBlocksNewCreationWithoutWriting(bool backup)
        {
            string p = LegacyPath + (backup ? ".bak" : ""); File.WriteAllText(p, "future founder");
            Assert.That(MigratingStore().TryLoad(out _, out _, out var status), Is.False);
            Assert.That(status, Is.EqualTo(FirmCareerLoadStatus.Unreadable));
            Assert.That(MigratingStore().TryCreate(FirmCareerTests.NewFirm(), out _), Is.False);
            Assert.That(File.ReadAllText(p), Is.EqualTo("future founder")); Assert.That(File.Exists(path), Is.False);
        }
        [Test] public void HireWriteFailureDoesNotMutateCareerAndRetryChargesOnlyOnce()
        {
            var firm = FirmCareerTests.NewFirm(); Assert.That(Store().TryCreate(firm, out _), Is.True); string bytes = File.ReadAllText(path);
            var bad = new FirmCareerStorage(path, _ => throw new Exception(), Decode);
            Assert.That(bad.TryHireAssistant(firm, out var hired, out _), Is.False); Assert.That(hired, Is.Null);
            Assert.That(firm.HasAssistant, Is.False); Assert.That(firm.Balance, Is.EqualTo(300)); Assert.That(File.ReadAllText(path), Is.EqualTo(bytes));
            Assert.That(Store().TryHireAssistant(firm, out hired, out _), Is.True);
            Assert.That(Store().TryHireAssistant(firm, out _, out _), Is.False);
            Assert.That(Store().TryHireAssistant(hired, out _, out _), Is.False);
            Assert.That(Store().TryLoad(out hired, out _, out _), Is.True); Assert.That(hired.Balance, Is.EqualTo(200));
        }
        [TestCase("changed")] [TestCase("broken")] [TestCase("missing")] [TestCase("hired")]
        public void HireRechecksSavedStateAndCannotReplaceNewOrUnknownProgress(string kind)
        {
            var firm = FirmCareerTests.NewFirm(); Store().TryCreate(firm, out _);
            if (kind == "broken") File.WriteAllText(path, "unknown");
            else if (kind == "missing") File.Delete(path);
            else if (kind == "hired") { Store().TryHireAssistant(firm, out _, out _); }
            else { var changed = FirmCareerTests.NewFirm(); FirmCareerTests.Finish(changed); Store().TrySave(changed, out _); }
            string bytes = File.Exists(path) ? File.ReadAllText(path) : null;
            Assert.That(Store().TryHireAssistant(firm, out _, out _), Is.False);
            Assert.That(File.Exists(path) ? File.ReadAllText(path) : null, Is.EqualTo(bytes)); Assert.That(firm.HasAssistant, Is.False);
        }
        [Test] public void NewerHireSaveWinsOverLegacyAndPayrollSurvivesReadback()
        {
            WriteLegacy(); var store = MigratingStore(); store.TryLoad(out var firm, out _, out _);
            store.TryHireAssistant(firm, out firm, out _); FirmCareerTests.Finish(firm); store.TrySave(firm, out _);
            Assert.That(store.TryLoad(out firm, out _, out _), Is.True); Assert.That(firm.Balance, Is.EqualTo(100));
            Assert.That(firm.LastReport.salary, Is.EqualTo(40)); Assert.That(firm.HasAssistant, Is.True);
            Assert.Throws<ArgumentException>(() => new FirmCareerStorage(path, Encode, Decode, path.ToUpperInvariant()));
        }
        [TestCase("broken")] [TestCase("future")] [TestCase("missing")]
        public void HireCannotTreatMatchingBackupAsUnchangedPrimary(string kind)
        {
            var firm = FirmCareerTests.NewFirm(); var store = Store(); store.TryCreate(firm, out _);
            store.TrySave(firm, out _); store.TryLoad(out firm, out _, out _);
            if (kind == "missing") File.Delete(path);
            else File.WriteAllText(path, kind == "future" ? "{\"schemaVersion\":99}" : "damaged primary");
            var files = Directory.GetFiles(folder); var bytes = Array.ConvertAll(files, File.ReadAllText);
            Assert.That(store.TryHireAssistant(firm, out var hired, out var notice), Is.False);
            Assert.That(hired, Is.Null); Assert.That(notice, Does.Contain("Найм не записан"));
            Assert.That(firm.HasAssistant, Is.False); Assert.That(firm.Balance, Is.EqualTo(300));
            CollectionAssert.AreEquivalent(files, Directory.GetFiles(folder));
            for (int i = 0; i < files.Length; i++) Assert.That(File.ReadAllText(files[i]), Is.EqualTo(bytes[i]));
        }
        [TestCase(false)] [TestCase(true)]
        public void RecoveredV3MustSaveBeforeHiringWithoutDamagingBackup(bool missing)
        {
            var firm = FirmCareerTests.NewFirm(); var store = Store(); store.TryCreate(firm, out _); store.TrySave(firm, out _);
            string backup = File.ReadAllText(path + ".bak");
            if (missing) File.Delete(path); else File.WriteAllText(path, "damaged primary");
            Assert.That(store.TryLoad(out firm, out _, out _), Is.True);
            Assert.That(store.TryHireAssistant(firm, out _, out _), Is.False);
            Assert.That(store.TrySave(firm, out _), Is.True); Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(backup));
            Assert.That(store.TryHireAssistant(firm, out firm, out _), Is.True); Assert.That(firm.Balance, Is.EqualTo(200));
        }
        [TestCase("broken")] [TestCase("changed")]
        public void HireRechecksPrimaryAfterEncodingEvenWithMatchingBackup(string kind)
        {
            var firm = FirmCareerTests.NewFirm(); Store().TryCreate(firm, out _); Store().TrySave(firm, out _);
            string backup = File.ReadAllText(path + ".bak"); var changed = firm.RestartFromOrigin(); FirmCareerTests.Finish(changed);
            string bytes = kind == "broken" ? "unknown primary" : Encode(changed.Capture());
            var store = new FirmCareerStorage(path, s => { File.WriteAllText(path, bytes); return Encode(s); }, Decode);
            Assert.That(store.TryHireAssistant(firm, out var hired, out _), Is.False); Assert.That(hired, Is.Null);
            Assert.That(File.ReadAllText(path), Is.EqualTo(bytes)); Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(backup));
            CollectionAssert.AreEquivalent(new[] { path, path + ".bak" }, Directory.GetFiles(folder));
        }
        [Test] public void SerializerDroppingHireCannotActivateUnsavedAssistant()
        {
            var firm = FirmCareerTests.NewFirm(); Store().TryCreate(firm, out _); string bytes = File.ReadAllText(path);
            var lossy = new FirmCareerStorage(path, s => { s.assistantHireDay = 0; return Encode(s); }, Decode);
            Assert.That(lossy.TryHireAssistant(firm, out var hired, out _), Is.False); Assert.That(hired, Is.Null);
            Assert.That(File.ReadAllText(path), Is.EqualTo(bytes)); Assert.That(firm.HasAssistant, Is.False);
        }
        [Test] public void ValidButDifferentSerializedStateCannotReplaceSnapshot()
        {
            var firm = FirmCareerTests.NewFirm(); Store().TryCreate(firm, out _); string bytes = File.ReadAllText(path);
            FirmCareerTests.Finish(firm, true);
            var wrong = new FirmCareerStorage(path, _ => bytes, Decode);
            Assert.That(wrong.TrySave(firm, out _), Is.False); Assert.That(File.ReadAllText(path), Is.EqualTo(bytes));
        }
    }
}
