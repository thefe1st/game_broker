using System;
using System.IO;
using System.Text.Json;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class WorkCareerStorageTests
    {
        private string folder, path;
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
        private static string Encode(WorkCareerSave s) => JsonSerializer.Serialize(s, Options);
        private static WorkCareerSave Decode(string s) => JsonSerializer.Deserialize<WorkCareerSave>(s, Options);
        private WorkCareerStorage Store() => new WorkCareerStorage(path, Encode, Decode);
        private static WorkCareerState NewState() => WorkCareerState.FromIntro(WorkCareerTests.Intro(true));
        private static void Finish(WorkCareerState c) { var d = c.CreateDay(); WorkCareerTests.Sell(d, 1); d.EndDay(); Assert.That(c.CompleteDay(d), Is.True); }
        [SetUp] public void Setup() { folder = Path.Combine(Path.GetTempPath(), "work-career-" + Guid.NewGuid()); Directory.CreateDirectory(folder); path = Path.Combine(folder, "broker-employee-work-v3.json"); }
        [TearDown] public void Cleanup() { Directory.Delete(folder, true); }
        [Test] public void MissingFileDoesNotCreateOne()
        { Assert.That(Store().TryLoad(out _, out var notice, out var status), Is.False); Assert.That(status, Is.EqualTo(WorkCareerLoadStatus.Missing)); Assert.That(notice, Is.Empty); Assert.That(Directory.GetFiles(folder), Is.Empty); }
        [Test] public void RoundTripBeyondLedgerWindowKeepsMoneyRelationsAndReport()
        {
            var c = NewState(); for (int n = 0; n < 20; n++) Finish(c);
            Assert.That(Store().TrySave(c, out _), Is.True); Assert.That(Store().TryLoad(out var read, out _), Is.True);
            Assert.That(read.CompletedDays, Is.EqualTo(20)); Assert.That(read.PersonalSavings, Is.EqualTo(c.PersonalSavings));
            Assert.That(read.Clients[3].awaitingReport, Is.EqualTo(c.Clients[3].awaitingReport)); Assert.That(read.Capture().reports.Length, Is.EqualTo(7));
            Assert.That(new FileInfo(path).Length, Is.LessThan(20000)); Assert.That(File.Exists(path + ".tmp"), Is.False);
        }
        [Test] public void RecoveryThenSaveKeepsHealthyBackupAndArchivesBrokenBytes()
        {
            var c = NewState(); Store().TrySave(c, out _); Finish(c); Store().TrySave(c, out _); string backup = File.ReadAllText(path + ".bak");
            File.WriteAllText(path, "broken bytes"); Assert.That(Store().TryLoad(out c, out var notice), Is.True); Assert.That(notice, Does.Contain("резервная"));
            Assert.That(c.CompletedDays, Is.Zero); Assert.That(Store().TrySave(c, out _), Is.True);
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(backup));
            var files = Directory.GetFiles(folder, "*.unreadable-*"); Assert.That(files.Length, Is.EqualTo(1)); Assert.That(File.ReadAllText(files[0]), Is.EqualTo("broken bytes"));
        }
        [TestCase("")] [TestCase("{bad")] [TestCase("{}")] [TestCase("{\"schemaVersion\":99}")]
        public void BadPayloadIsNotWrittenOverOnLoad(string text)
        { File.WriteAllText(path, text); Assert.That(Store().TryLoad(out _, out _), Is.False); Assert.That(File.ReadAllText(path), Is.EqualTo(text)); }
        [Test] public void IntroAndOwnerFilesCannotBeLoadedAsContinuation()
        {
            File.WriteAllText(path, JsonSerializer.Serialize(WorkCareerTests.Intro().Capture(), Options)); Assert.That(Store().TryLoad(out _, out _), Is.False);
            File.WriteAllText(path, JsonSerializer.Serialize(new CareerState().Capture(), Options)); Assert.That(Store().TryLoad(out _, out _), Is.False);
        }
        [Test] public void UnknownVersionIsPreservedByteForByteBeforeExplicitNewStart()
        {
            var text = "{\"schemaVersion\":99,\"mode\":\"employee-work\"}"; File.WriteAllText(path, text);
            Assert.That(Store().TrySave(NewState(), out _), Is.True); Assert.That(File.ReadAllText(Directory.GetFiles(folder, "*.unreadable-*")[0]), Is.EqualTo(text));
        }
        [Test] public void SerializerFailureLeavesGoodProgressUntouched()
        {
            Store().TrySave(NewState(), out _); string before = File.ReadAllText(path);
            Assert.That(new WorkCareerStorage(path, _ => throw new Exception(), Decode).TrySave(NewState(), out _), Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(before)); Assert.That(File.Exists(path + ".tmp"), Is.False);
        }
        [Test] public void InvalidSerializedRoundTripDoesNotReplaceFile()
        {
            Store().TrySave(NewState(), out _); string before = File.ReadAllText(path);
            Assert.That(new WorkCareerStorage(path, _ => "{}", Decode).TrySave(NewState(), out _), Is.False); Assert.That(File.ReadAllText(path), Is.EqualTo(before));
        }
        [Test] public void OversizedFileIsRejected()
        { File.WriteAllText(path, new string('x', WorkCareerStorage.MaxBytes + 1)); Assert.That(Store().TryLoad(out _, out _), Is.False); }
        [Test] public void WorkWritesAndResetLeaveBothOtherModesAndBackupsByteIdentical()
        {
            string intro = Path.Combine(folder, "broker-employee-intro-v1.json"), owner = Path.Combine(folder, "broker-career-v1.json");
            File.WriteAllText(intro, "intro main"); File.WriteAllText(intro + ".bak", "intro backup"); File.WriteAllText(owner, "owner main"); File.WriteAllText(owner + ".bak", "owner backup");
            var c = NewState(); Store().TrySave(c, out _); Finish(c); Store().TrySave(c, out _); Store().TrySave(c.RestartFromOrigin(), out _);
            Assert.That(File.ReadAllText(intro), Is.EqualTo("intro main")); Assert.That(File.ReadAllText(intro + ".bak"), Is.EqualTo("intro backup"));
            Assert.That(File.ReadAllText(owner), Is.EqualTo("owner main")); Assert.That(File.ReadAllText(owner + ".bak"), Is.EqualTo("owner backup"));
        }
        private string LegacyPath => Path.Combine(folder, "broker-employee-work-v1.json");
        private string V2Path => Path.Combine(folder, "broker-employee-work-v2.json");
        private WorkCareerStorage WithLegacy() => new WorkCareerStorage(path, Encode, Decode, V2Path, LegacyPath);
        // Real v1/v2 JSON has no stock fields at all; v1 also lacks complaint fields.
        private static System.Text.Json.Nodes.JsonNode WithoutStocks(WorkCareerState c, int version)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(Encode(c.Capture())); node["schemaVersion"] = version; node.AsObject().Remove("stockHistoryFromDay");
            foreach (string key in new[] { "origin", "checkpoint" })
                foreach (var client in node[key]["clients"].AsArray()) { client.AsObject().Remove("lastStockId"); client.AsObject().Remove("lastStockDay"); }
            foreach (var report in node["reports"].AsArray()) report.AsObject().Remove("soldStockIds");
            return node;
        }
        private static string V2Json(WorkCareerState c) => WithoutStocks(c, 2).ToJsonString();
        private static string LegacyJson(WorkCareerState c)
        {
            var node = WithoutStocks(c, 1);
            foreach (string key in new[] { "origin", "checkpoint" })
                foreach (var client in node[key]["clients"].AsArray()) client.AsObject().Remove("complaintPending");
            foreach (var report in node["reports"].AsArray())
            { report.AsObject().Remove("guaranteed"); report.AsObject().Remove("complaintResolved"); }
            return node.ToJsonString();
        }
        [Test] public void LegacyImportLoadsWithoutWritesAndSavesOnlySeparateV3()
        {
            var c = NewState(); for (int n = 0; n < 12; n++) Finish(c);
            string json = LegacyJson(c); File.WriteAllText(LegacyPath, json); File.WriteAllText(LegacyPath + ".bak", json);
            Assert.That(WithLegacy().TryLoad(out var loaded, out var notice), Is.True);
            Assert.That(notice, Does.Contain("копия v1")); Assert.That(File.Exists(path), Is.False);
            Assert.That(loaded.PersonalSavings, Is.EqualTo(c.PersonalSavings)); Assert.That(loaded.CompletedDays, Is.EqualTo(12));
            Assert.That(loaded.Capture().schemaVersion, Is.EqualTo(3)); Assert.That(WithLegacy().TrySave(loaded, out _), Is.True);
            Finish(loaded); WithLegacy().TrySave(loaded, out _); WithLegacy().TrySave(loaded.RestartFromOrigin(), out _);
            Assert.That(File.ReadAllText(LegacyPath), Is.EqualTo(json)); Assert.That(File.ReadAllText(LegacyPath + ".bak"), Is.EqualTo(json));
            Assert.That(Decode(File.ReadAllText(path)).schemaVersion, Is.EqualTo(3)); Assert.That(File.Exists(V2Path), Is.False);
        }
        [Test] public void V2ImportIsReadOnlyWinsOverV1AndStartsStockHistoryNextDay()
        {
            var c = NewState(); for (int n = 0; n < 12; n++) Finish(c);
            string v2 = V2Json(c), v1 = LegacyJson(NewState());
            File.WriteAllText(V2Path, v2); File.WriteAllText(V2Path + ".bak", v2); File.WriteAllText(LegacyPath, v1);
            Assert.That(WithLegacy().TryLoad(out var loaded, out var notice), Is.True);
            Assert.That(notice, Does.Contain("копия v2")); Assert.That(notice, Does.Contain("в v3")); Assert.That(File.Exists(path), Is.False);
            Assert.That(loaded.CompletedDays, Is.EqualTo(12)); Assert.That(loaded.StockHistoryFromDay, Is.EqualTo(16));
            foreach (var r in loaded.RecentReports) Assert.That(r.soldStockIds, Is.EqualTo(new[] { "", "", "" }));
            Finish(loaded); Assert.That(WithLegacy().TrySave(loaded, out _), Is.True);
            Assert.That(WithLegacy().TryLoad(out var read, out notice), Is.True); Assert.That(notice, Is.Empty);
            Assert.That(read.LastReport.soldStockIds[1], Is.EqualTo(StockCatalog.DefaultFor(read.LastReport.clientIds[1])));
            Assert.That(read.StockHistoryFromDay, Is.EqualTo(16)); Assert.That(Decode(File.ReadAllText(path)).schemaVersion, Is.EqualTo(3));
            Assert.That(File.ReadAllText(V2Path), Is.EqualTo(v2)); Assert.That(File.ReadAllText(V2Path + ".bak"), Is.EqualTo(v2)); Assert.That(File.ReadAllText(LegacyPath), Is.EqualTo(v1));
        }
        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void UnreadableV2NeverFallsBackToHealthyV1(bool backup, bool directory)
        {
            File.WriteAllText(LegacyPath, LegacyJson(NewState())); string p = V2Path + (backup ? ".bak" : "");
            if (directory) Directory.CreateDirectory(p); else File.WriteAllText(p, "future v2 bytes");
            Assert.That(WithLegacy().TryLoad(out var state, out var notice, out var status), Is.False);
            Assert.That(state, Is.Null); Assert.That(status, Is.EqualTo(WorkCareerLoadStatus.Unreadable)); Assert.That(notice, Does.Contain("перенос заблокирован"));
            Assert.That(WithLegacy().TryCreate(NewState(), out _), Is.False); Assert.That(File.Exists(path), Is.False);
        }
        [Test] public void V3BackupWinsOverOlderGenerationsAndV3DataIsNotReadAsV2()
        {
            File.WriteAllText(V2Path, V2Json(NewState())); File.WriteAllText(LegacyPath, LegacyJson(NewState()));
            var c = NewState(); Finish(c); WithLegacy().TrySave(c, out _); Finish(c); WithLegacy().TrySave(c, out _); File.WriteAllText(path, "broken");
            Assert.That(WithLegacy().TryLoad(out var loaded, out var notice), Is.True); Assert.That(notice, Does.Contain("резервная"));
            Assert.That(notice, Does.Not.Contain("копия v")); Assert.That(loaded.CompletedDays, Is.EqualTo(1));
            Directory.Delete(folder, true); Directory.CreateDirectory(folder); File.WriteAllText(V2Path, Encode(NewState().Capture()));
            Assert.That(WithLegacy().TryLoad(out _, out _, out var status), Is.False); Assert.That(status, Is.EqualTo(WorkCareerLoadStatus.Unreadable));
            Assert.Throws<ArgumentException>(() => new WorkCareerStorage(path, Encode, Decode, path.ToUpperInvariant()));
            Assert.Throws<ArgumentException>(() => new WorkCareerStorage(path, Encode, Decode, V2Path, V2Path.ToUpperInvariant()));
        }
        [Test] public void StockHistoryRoundTripsThroughJsonAndStaysBounded()
        {
            var c = NewState();
            for (int n = 0; n < 60; n++) { var d = c.CreateDay(); for (int i = 0; i < 3; i++) WorkCareerTests.Sell(d, i); d.EndDay(); Assert.That(c.CompleteDay(d), Is.True); }
            Assert.That(Store().TrySave(c, out _), Is.True); Assert.That(Store().TryLoad(out var read, out _), Is.True);
            Assert.That(read.LastReport.soldStockIds, Is.EqualTo(c.LastReport.soldStockIds)); Assert.That(read.LastReport.soldStockIds[2], Is.Not.Empty);
            for (int i = 0; i < 6; i++)
            { Assert.That(read.Clients[i].lastStockId, Is.EqualTo(c.Clients[i].lastStockId)); Assert.That(read.Clients[i].lastStockDay, Is.EqualTo(c.Clients[i].lastStockDay)); }
            Assert.That(new FileInfo(path).Length, Is.LessThan(20000));
        }
        [Test] public void LegacyBackupRecoveryDoesNotAlterDamagedPrimaryOrBackup()
        {
            string json = LegacyJson(NewState()); File.WriteAllText(LegacyPath, "broken"); File.WriteAllText(LegacyPath + ".bak", json);
            Assert.That(WithLegacy().TryLoad(out var loaded, out var notice), Is.True); Assert.That(notice, Does.Contain("резервная"));
            Assert.That(WithLegacy().TrySave(loaded, out _), Is.True);
            Assert.That(File.ReadAllText(LegacyPath), Is.EqualTo("broken")); Assert.That(File.ReadAllText(LegacyPath + ".bak"), Is.EqualTo(json));
        }
        [TestCase("{bad")] [TestCase("{\"schemaVersion\":99}")] [TestCase("")]
        public void InvalidNewerFileNeverSilentlyRollsBackToLegacy(string text)
        {
            File.WriteAllText(LegacyPath, LegacyJson(NewState())); File.WriteAllText(path, text);
            Assert.That(WithLegacy().TryLoad(out _, out _), Is.False); Assert.That(File.ReadAllText(path), Is.EqualTo(text));
        }
        [Test] public void NewerBackupTakesPrecedenceOverLegacyAndComplaintsSurvive()
        {
            File.WriteAllText(LegacyPath, LegacyJson(NewState())); var c = ComplaintTests.WithComplaint();
            WithLegacy().TrySave(c, out _); WithLegacy().TrySave(c, out _); File.WriteAllText(path, "broken");
            Assert.That(WithLegacy().TryLoad(out var loaded, out _), Is.True); Assert.That(loaded.Clients[0].complaintPending, Is.True);
            Assert.That(WithLegacy().TrySave(loaded, out _), Is.True); Assert.That(WithLegacy().TryLoad(out loaded, out _), Is.True);
            Assert.That(loaded.Clients[0].complaintPending, Is.True);
        }
        [Test] public void DirectoryAtNewerPathBlocksLegacyFallback()
        {
            File.WriteAllText(LegacyPath, LegacyJson(NewState())); Directory.CreateDirectory(path);
            Assert.That(WithLegacy().TryLoad(out _, out _), Is.False); Assert.That(WithLegacy().TrySave(NewState(), out _), Is.False);
        }
        [Test] public void FutureOrWrongModeLegacyIsNotImported()
        {
            File.WriteAllText(LegacyPath, Encode(NewState().Capture())); Assert.That(WithLegacy().TryLoad(out _, out _), Is.False);
            var node = System.Text.Json.Nodes.JsonNode.Parse(LegacyJson(NewState())); node["mode"] = "employee-intro";
            File.WriteAllText(LegacyPath, node.ToJsonString()); Assert.That(WithLegacy().TryLoad(out _, out _), Is.False);
        }
        [Test] public void FailedImportWriteDoesNotTouchLegacyAndCanRetry()
        {
            string json = LegacyJson(NewState()); File.WriteAllText(LegacyPath, json); WithLegacy().TryLoad(out var loaded, out _);
            Directory.CreateDirectory(path); Assert.That(WithLegacy().TrySave(loaded, out _), Is.False);
            Assert.That(File.ReadAllText(LegacyPath), Is.EqualTo(json)); Directory.Delete(path);
            Assert.That(WithLegacy().TrySave(loaded, out _), Is.True);
        }
        private string SourcePath(bool legacy, bool backup) => (legacy ? LegacyPath : path) + (backup ? ".bak" : "");
        [TestCase(false, false, false)] [TestCase(false, false, true)]
        [TestCase(false, true, false)] [TestCase(false, true, true)]
        [TestCase(true, false, false)] [TestCase(true, false, true)]
        [TestCase(true, true, false)] [TestCase(true, true, true)]
        public void LoadStatusDistinguishesUnreadableFileOrDirectoryFromMissing(bool legacy, bool backup, bool directory)
        {
            string source = SourcePath(legacy, backup);
            if (directory) Directory.CreateDirectory(source); else File.WriteAllText(source, "{\"schemaVersion\":99}");
            Assert.That(WithLegacy().TryLoad(out var state, out var notice, out var status), Is.False);
            Assert.That(state, Is.Null); Assert.That(status, Is.EqualTo(WorkCareerLoadStatus.Unreadable));
            Assert.That(notice, Does.Contain("перенос заблокирован"));
        }
        [TestCase(false, false, "valid")] [TestCase(false, false, "invalid")] [TestCase(false, false, "directory")]
        [TestCase(false, true, "valid")] [TestCase(false, true, "invalid")] [TestCase(false, true, "directory")]
        [TestCase(true, false, "valid")] [TestCase(true, false, "invalid")] [TestCase(true, false, "directory")]
        [TestCase(true, true, "valid")] [TestCase(true, true, "invalid")] [TestCase(true, true, "directory")]
        public void FirstTransferCannotWriteOverExistingContinuationOrBackup(bool legacy, bool backup, string payload)
        {
            string source = SourcePath(legacy, backup);
            string text = payload == "valid" ? (legacy ? LegacyJson(NewState()) : Encode(NewState().Capture())) : "future bytes";
            if (payload == "directory") Directory.CreateDirectory(source); else File.WriteAllText(source, text);
            var files = Directory.GetFiles(folder);
            Assert.That(WithLegacy().TryCreate(NewState(), out var notice), Is.False); Assert.That(notice, Is.Not.Empty);
            CollectionAssert.AreEquivalent(files, Directory.GetFiles(folder));
            if (payload == "directory") Assert.That(Directory.Exists(source), Is.True);
            else Assert.That(File.ReadAllText(source), Is.EqualTo(text));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }
        [Test] public void FirstTransferWritesOnlyV3AndReportsLoadedOnRead()
        {
            Assert.That(WithLegacy().TryCreate(NewState(), out _), Is.True);
            CollectionAssert.AreEquivalent(new[] { path }, Directory.GetFiles(folder));
            Assert.That(WithLegacy().TryLoad(out var read, out _, out var status), Is.True);
            Assert.That(status, Is.EqualTo(WorkCareerLoadStatus.Loaded)); Assert.That(read.CompletedDays, Is.Zero);
        }
        [Test] public void FirstTransferDoesNotOverwriteOrDeleteAnExistingTemporaryFile()
        {
            File.WriteAllText(path + ".tmp", "previous interrupted write");
            Assert.That(WithLegacy().TryCreate(NewState(), out _), Is.True);
            Assert.That(File.ReadAllText(path + ".tmp"), Is.EqualTo("previous interrupted write"));
            Assert.That(WithLegacy().TryCreate(NewState(), out _), Is.False);
            Assert.That(File.ReadAllText(path + ".tmp"), Is.EqualTo("previous interrupted write"));
            CollectionAssert.AreEquivalent(new[] { path, path + ".tmp" }, Directory.GetFiles(folder));
        }
        [Test] public void FirstTransferRechecksFilesAfterSerializationBeforeWriting()
        {
            const string bytes = "newly restored future save";
            var store = new WorkCareerStorage(path, s => { File.WriteAllText(path, bytes); return Encode(s); }, Decode);
            Assert.That(store.TryCreate(NewState(), out _), Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(bytes));
            CollectionAssert.AreEquivalent(new[] { path }, Directory.GetFiles(folder));
        }
    }
}
