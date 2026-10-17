using System;
using System.IO;
using System.Text.Json;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class CareerStorageTests
    {
        private string folder, path;
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
        private static string Encode(CareerSave s) => JsonSerializer.Serialize(s, Options);
        private static CareerSave Decode(string s) => JsonSerializer.Deserialize<CareerSave>(s, Options);
        private CareerStorage Store() => new CareerStorage(path, Encode, Decode);
        [SetUp] public void Setup() { folder = Path.Combine(Path.GetTempPath(), "broker-tests-" + Guid.NewGuid()); Directory.CreateDirectory(folder); path = Path.Combine(folder, "career.json"); }
        [TearDown] public void Cleanup() { Directory.Delete(folder, true); }
        private static CareerState CompletedDay()
        { var c = new CareerState(); var d = c.CreateDay(); d.EndDay(); c.CompleteDay(d); return c; }
        [Test] public void MissingSaveIsNormalAndCreatesNoFiles()
        { Assert.That(Store().TryLoad(out _, out var notice), Is.False); Assert.That(notice, Is.Empty); Assert.That(Directory.GetFiles(folder), Is.Empty); }
        [Test] public void RoundTripPreservesDebtAndDay()
        {
            Assert.That(Store().TrySave(CompletedDay(), out _), Is.True); Assert.That(Store().TryLoad(out var c, out _), Is.True);
            Assert.That(c.Balance, Is.EqualTo(-340)); Assert.That(c.NextDay, Is.EqualTo(2)); Assert.That(File.Exists(path + ".tmp"), Is.False);
        }
        [Test] public void SecondWriteCreatesBackupAndCorruptionRecoversIt()
        {
            Store().TrySave(new CareerState(), out _); Store().TrySave(CompletedDay(), out _);
            Assert.That(File.Exists(path + ".bak"), Is.True); File.WriteAllText(path, "{broken");
            Assert.That(Store().TryLoad(out var c, out var notice), Is.True); Assert.That(c.CompletedDays, Is.Zero); Assert.That(notice, Does.Contain("резервная"));
        }
        [TestCase("")] [TestCase("{broken")] [TestCase("{\"schemaVersion\":99}")]
        public void BadSaveIsRejectedWithoutChangingFile(string text)
        { File.WriteAllText(path, text); Assert.That(Store().TryLoad(out _, out var notice), Is.False); Assert.That(notice, Is.Not.Empty); Assert.That(File.ReadAllText(path), Is.EqualTo(text)); }
        [Test] public void OversizedSaveIsRejected()
        { File.WriteAllText(path, new string('x', CareerStorage.MaxBytes + 1)); Assert.That(Store().TryLoad(out _, out _), Is.False); }
        [Test] public void SerializationFailurePreservesPreviousFileAndCleansTemporary()
        {
            Store().TrySave(CompletedDay(), out _); string before = File.ReadAllText(path);
            var bad = new CareerStorage(path, _ => throw new Exception("test"), Decode);
            Assert.That(bad.TrySave(new CareerState(), out _), Is.False); Assert.That(File.ReadAllText(path), Is.EqualTo(before));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }
        [Test] public void InvalidSerializedPayloadCannotReplaceGoodSave()
        {
            Store().TrySave(CompletedDay(), out _); string before = File.ReadAllText(path);
            var bad = new CareerStorage(path, _ => "{\"schemaVersion\":99}", Decode);
            Assert.That(bad.TrySave(new CareerState(), out _), Is.False); Assert.That(File.ReadAllText(path), Is.EqualTo(before));
        }
        [Test] public void CompletedCampaignRoundTripsAndCannotStartFourthDay()
        {
            var c = new CareerState(); for (int i = 0; i < 3; i++) { var d = c.CreateDay(); d.EndDay(); c.CompleteDay(d); Assert.That(Store().TrySave(c, out _), Is.True); }
            Assert.That(Store().TrySave(c, out _), Is.True); Assert.That(Store().TryLoad(out var loaded, out _), Is.True);
            Assert.That(loaded.IsFinished, Is.True); Assert.Throws<InvalidOperationException>(() => loaded.CreateDay());
        }
    }
}
