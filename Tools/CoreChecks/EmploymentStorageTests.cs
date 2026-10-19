using System;
using System.IO;
using System.Text.Json;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class EmploymentStorageTests
    {
        private string folder, path;
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
        private static string Encode(EmploymentSave s) => JsonSerializer.Serialize(s, Options);
        private static EmploymentSave Decode(string s) => JsonSerializer.Deserialize<EmploymentSave>(s, Options);
        private EmploymentStorage Store() => new EmploymentStorage(path, Encode, Decode);
        [SetUp] public void Setup() { folder = Path.Combine(Path.GetTempPath(), "employee-tests-" + Guid.NewGuid()); Directory.CreateDirectory(folder); path = Path.Combine(folder, "broker-employee-intro-v1.json"); }
        [TearDown] public void Cleanup() { Directory.Delete(folder, true); }
        private static EmploymentState CompletedDay()
        { var c = new EmploymentState(); var d = c.CreateDay(); EmploymentTests.Sell(d, 0); d.EndDay(); c.CompleteDay(d); return c; }
        [Test] public void MissingFileDoesNotWriteAnything()
        { Assert.That(Store().TryLoad(out _, out var n), Is.False); Assert.That(n, Is.Empty); Assert.That(Directory.GetFiles(folder), Is.Empty); }
        [Test] public void RoundTripPreservesSavingsAndReturningClient()
        {
            Assert.That(Store().TrySave(CompletedDay(), out _), Is.True); Assert.That(Store().TryLoad(out var c, out _), Is.True);
            Assert.That(c.PersonalSavings, Is.EqualTo(30)); Assert.That(c.CreateDay().Clients[0].Returning, Is.True);
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }
        [Test] public void CorruptionRecoversPreviousCompletedDay()
        {
            var c = CompletedDay(); Store().TrySave(c, out _); var d = c.CreateDay(); d.EndDay(); c.CompleteDay(d); Store().TrySave(c, out _);
            File.WriteAllText(path, "{bad"); Assert.That(Store().TryLoad(out var loaded, out var n), Is.True);
            Assert.That(loaded.CompletedDays, Is.EqualTo(1)); Assert.That(n, Does.Contain("резервная"));
        }
        [TestCase("")] [TestCase("{bad")] [TestCase("{}")] [TestCase("{\"schemaVersion\":99}")]
        public void BadFileIsNotChangedOnLoad(string text)
        { File.WriteAllText(path, text); Assert.That(Store().TryLoad(out _, out var n), Is.False); Assert.That(n, Is.Not.Empty); Assert.That(File.ReadAllText(path), Is.EqualTo(text)); }
        [Test] public void OwnerSaveCannotBeMisreadAsEmployeeSave()
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new CareerState().Capture(), Options));
            Assert.That(Store().TryLoad(out _, out _), Is.False);
        }
        [Test] public void EmployeeWritesLeaveOldOwnersMainAndBackupUntouched()
        {
            string ownerPath = Path.Combine(folder, "broker-career-v1.json");
            var ownerStore = new CareerStorage(ownerPath, s => JsonSerializer.Serialize(s, Options), s => JsonSerializer.Deserialize<CareerSave>(s, Options));
            var c = new CareerState(); ownerStore.TrySave(c, out _); var d = c.CreateDay(); d.EndDay(); c.CompleteDay(d); ownerStore.TrySave(c, out _);
            string before = File.ReadAllText(ownerPath), backup = File.ReadAllText(ownerPath + ".bak");
            Store().TrySave(CompletedDay(), out _); Store().TrySave(new EmploymentState(), out _);
            Assert.That(File.ReadAllText(ownerPath), Is.EqualTo(before)); Assert.That(File.ReadAllText(ownerPath + ".bak"), Is.EqualTo(backup));
            Assert.That(ownerStore.TryLoad(out var loaded, out _), Is.True); Assert.That(loaded.Balance, Is.EqualTo(-340));
        }
        [Test] public void SerializerFailureLeavesGoodFileAndNoTemporaryFile()
        {
            Store().TrySave(CompletedDay(), out _); string before = File.ReadAllText(path);
            var bad = new EmploymentStorage(path, _ => throw new Exception(), Decode);
            Assert.That(bad.TrySave(new EmploymentState(), out _), Is.False); Assert.That(File.ReadAllText(path), Is.EqualTo(before));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }
        [Test] public void InvalidSerializedPayloadCannotReplaceGoodFile()
        {
            Store().TrySave(CompletedDay(), out _); string before = File.ReadAllText(path);
            Assert.That(new EmploymentStorage(path, _ => "{}", Decode).TrySave(new EmploymentState(), out _), Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
        }
        [Test] public void OversizedFileIsRejected()
        { File.WriteAllText(path, new string('x', EmploymentStorage.MaxBytes + 1)); Assert.That(Store().TryLoad(out _, out _), Is.False); }
        [Test] public void FinishedIntroReloadsAsSummaryNotFourthDay()
        {
            var c = new EmploymentState(); for (int i = 0; i < 3; i++) { var d = c.CreateDay(); d.EndDay(); c.CompleteDay(d); }
            Assert.That(Store().TrySave(c, out _), Is.True); Assert.That(Store().TryLoad(out var loaded, out _), Is.True);
            Assert.That(loaded.IsIntroComplete, Is.True); Assert.Throws<InvalidOperationException>(() => loaded.CreateDay());
        }
    }
}
