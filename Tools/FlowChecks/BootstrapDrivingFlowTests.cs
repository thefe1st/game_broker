using System.IO;
using System.Linq;
using Broker.Core;
using Broker.Runtime.Office;
using NUnit.Framework;
using UnityEngine;

namespace Broker.FlowChecks
{
    public sealed partial class BootstrapFlowTests
    {
        [Test] public void TheTestCarStartsOnlyInTheCityKeepsTheClockAndTouchesNoMoneyOrFiles()
        {
            Begin(); var office = Get<OfficeScene>("office");
            Interact(OfficeStation.Car); Assert.That(office.Driving, Is.False);
            Interact(OfficeStation.Street); float seconds = Day.RemainingSeconds; int cash = Day.Cash;
            var files = Directory.GetFiles(folder).Select(File.ReadAllText).ToArray();
            Interact(OfficeStation.Car);
            Assert.That(office.Driving, Is.True); Assert.That(Get<OfficeNavigation>("navigation").IsExploring, Is.True);
            Frame(); Frame();
            Assert.That(Day.RemainingSeconds, Is.LessThan(seconds)); Assert.That(Day.Cash, Is.EqualTo(cash));
            Assert.That(office.WorldMotionEnabled, Is.True); Assert.That(office.Player.ControlEnabled, Is.True);
            Assert.That(UnityEngine.Networking.UnityWebRequest.Sent, Is.Zero);
            Assert.That(Directory.GetFiles(folder).Select(File.ReadAllText).ToArray(), Is.EqualTo(files));
            // Behind the wheel the stands are out of reach.
            Interact(OfficeStation.CityMap); Assert.That(Get<OfficeNavigation>("navigation").IsExploring, Is.True);
            Interact(OfficeStation.ReturnOffice); Assert.That(office.IsInCity, Is.True); Assert.That(office.Driving, Is.True);
        }
        [Test] public void DrivingHudShowsSpeedGearKeysAndWhereToGetOut()
        {
            Begin(); var office = Get<OfficeScene>("office"); Interact(OfficeStation.Street);
            Assert.That(Pictograms(), Does.Not.Contain("Gauge")); Assert.That(GUI.Labels, Does.Not.Contain("км/ч"));
            Interact(OfficeStation.Car); office.CarSpeedKmh = 47.4f; office.CarGear = "3";
            var pictures = Pictograms(); var labels = GUI.Labels.ToArray();
            Assert.That(labels, Does.Contain("47")); Assert.That(labels, Does.Contain("км/ч")); Assert.That(labels, Does.Contain("3"));
            // Away from the lay-by: the parking sign, the arrow to it and the metres instead of a sentence.
            Assert.That(pictures, Does.Contain("Parking")); Assert.That(pictures, Does.Contain("Arrow")); Assert.That(labels.Any(l => l.EndsWith(" м")), Is.True);
            Assert.That(labels, Does.Not.Contain("Выйти")); Assert.That(pictures, Does.Not.Contain("Exit"));
            // The first seconds behind the wheel: the pedals, the wheel and the handbrake as keys with pictograms.
            Assert.That(pictures, Is.SupersetOf(new[] { "Gauge", "Wheel", "Handbrake", "Space", "Mouse", "Pause" }));
            Assert.That(pictures, Does.Not.Contain("Walker")); Assert.That(pictures, Does.Not.Contain("Pointer"));
            office.CarGear = "R"; Pictograms(); Assert.That(GUI.Labels, Does.Contain("R"));
            office.Parked = true; pictures = Pictograms();
            Assert.That(GUI.Labels, Does.Contain("Выйти")); Assert.That(GUI.Labels, Does.Contain("E"));
            Assert.That(pictures, Does.Contain("Exit")); Assert.That(pictures, Does.Not.Contain("Parking"));
            Time.realtimeSinceStartupAsDouble = 11; Assert.That(Pictograms(), Does.Not.Contain("Handbrake"));
            string text = string.Join("\n", GUI.Labels);
            Assert.That(text, Does.Not.Contain("ручник")); Assert.That(text, Does.Not.Contain("передача")); Assert.That(text, Does.Not.Contain("задний ход"));
        }
        [Test] public void TheDriverGetsOutOnlyAtTheParkingAndNeverDuringAPause()
        {
            Begin(); var office = Get<OfficeScene>("office"); Interact(OfficeStation.Street); Interact(OfficeStation.Car);
            office.Player.OnLeaveRequest(); Assert.That(office.Driving, Is.True);
            Assert.That(Get<double>("carNoticeUntil"), Is.GreaterThan(Time.realtimeSinceStartupAsDouble));
            office.Parked = true; Call("PauseGameplay");
            office.Player.OnLeaveRequest(); Assert.That(office.Driving, Is.True);
            // The pause lists the car's controls with getting out at the parking: E, the door and the parking sign.
            Assert.That(Pictograms(), Is.SupersetOf(new[] { "Gauge", "Wheel", "Handbrake", "Exit", "Parking", "Mouse" }));
            Click("Продолжить · Esc"); office.Player.OnLeaveRequest();
            Assert.That(office.Driving, Is.False); Assert.That(Get<OfficeNavigation>("navigation").IsExploring, Is.True);
            Assert.That(office.IsInCity, Is.True); Assert.That(Day.Cash, Is.Zero);
        }
        [Test] public void TheMapFollowsTheCarAndOffersTheParking()
        {
            Begin(); var office = Get<OfficeScene>("office"); Interact(OfficeStation.Street); Interact(OfficeStation.Car);
            Call("PauseGameplay"); Click("Карта района"); Click("Ориентир / Парковка у сквера");
            Assert.That(Get<string>("cityDestinationId"), Is.EqualTo("parking"));
            string text = NotebookText();
            Assert.That(text, Does.Contain("В машине — по кольцу и трассе")); Assert.That(text, Does.Contain("КОЛЬЦО")); Assert.That(text, Does.Contain("ТРАССА"));
            Assert.That(text, Does.Contain(CityDistrictPlan.Find("parking").Description));
            OfficeInput.Escape = true; Call("Update"); Assert.That(Get<OfficeNavigation>("navigation").View, Is.EqualTo(OfficeView.Pause));
            Call("Restart"); Assert.That(office.Driving, Is.False);
        }
    }
}
