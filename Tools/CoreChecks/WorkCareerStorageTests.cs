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
        [SetUp] public void Setup() { folder = Path.Combine(Path.GetTempPath(), "work-career-" + Guid.NewGuid()); Directory.CreateDirectory(folder); path = Path.Combine(folder, "broker-employee-work-v1.json"); }
        [TearDown] public void Cleanup() { Directory.Delete(folder, true); }
        [Test] public void MissingFileDoesNotCreateOne()
        { Assert.That(Store().TryLoad(out _, out var notice), Is.False); Assert.That(notice, Is.Empty); Assert.That(Directory.GetFiles(folder), Is.Empty); }
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
    }
}
