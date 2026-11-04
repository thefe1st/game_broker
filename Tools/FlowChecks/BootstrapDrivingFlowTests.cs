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
            Assert.That(NotebookText(), Does.Not.Contain("км/ч"));
            Interact(OfficeStation.Car); office.CarSpeedKmh = 47.4f; office.CarGear = "3";
            string text = NotebookText();
            Assert.That(text, Does.Contain("47")); Assert.That(text, Does.Contain("км/ч · передача 3"));
            Assert.That(text, Does.Contain("Выйти можно только на парковке")); Assert.That(text, Does.Contain("Пробел · ручник"));
            Assert.That(text, Does.Not.Contain("WASD · ходить")); Assert.That(text, Does.Not.Contain("Прогулка · лифт ведёт в офис"));
            office.CarGear = "R"; Assert.That(NotebookText(), Does.Contain("км/ч · задний ход"));
            office.Parked = true; text = NotebookText();
            Assert.That(text, Does.Contain("Выйти из машины")); Assert.That(text, Does.Not.Contain("Выйти можно только на парковке"));
            Time.realtimeSinceStartupAsDouble = 11; Assert.That(NotebookText(), Does.Not.Contain("Пробел · ручник"));
        }
        [Test] public void TheDriverGetsOutOnlyAtTheParkingAndNeverDuringAPause()
        {
            Begin(); var office = Get<OfficeScene>("office"); Interact(OfficeStation.Street); Interact(OfficeStation.Car);
            office.Player.OnLeaveRequest(); Assert.That(office.Driving, Is.True);
            Assert.That(Get<double>("carNoticeUntil"), Is.GreaterThan(Time.realtimeSinceStartupAsDouble));
            office.Parked = true; Call("PauseGameplay");
            office.Player.OnLeaveRequest(); Assert.That(office.Driving, Is.True);
            Assert.That(NotebookText(), Does.Contain("E · выйти на парковке у сквера"));
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
            Assert.That(text, Does.Contain("В машине — только по кольцу")); Assert.That(text, Does.Contain("КОЛЬЦО"));
            Assert.That(text, Does.Contain(CityDistrictPlan.Find("parking").Description));
            OfficeInput.Escape = true; Call("Update"); Assert.That(Get<OfficeNavigation>("navigation").View, Is.EqualTo(OfficeView.Pause));
            Call("Restart"); Assert.That(office.Driving, Is.False);
        }
    }
}
