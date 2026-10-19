using UnityEngine;

namespace Broker.Runtime
{
    public sealed partial class PrototypeBootstrap
    {
        private enum ConfirmationKind { None, RestartDay, NewCareer, SelectMode, EndCall }
        private ConfirmationKind confirmation;
        private bool showVoiceSetup;
        private bool UtilityOverlayOpen => showVoiceSetup || showCallLog || confirmation != ConfirmationKind.None;

        private void OpenVoiceSetup()
        {
            CancelPending(); voice.RefreshDevices(); showVoiceSetup = true;
            office.Player.SetControl(false); GUI.FocusControl(null);
        }

        private void CloseUtilityOverlay()
        {
            voice.Cancel(); showVoiceSetup = false; showCallLog = false; confirmation = ConfirmationKind.None;
            // Also called from Update on Escape: do not call IMGUI APIs here.
            // Return to the original intro/pause/desk, not directly to exploration.
        }

        private void RequestConfirmation(ConfirmationKind kind)
        {
            CancelPending(); confirmation = kind; office.Player.SetControl(false); GUI.FocusControl(null);
        }

        private void DrawConfirmation()
        {
            Scrim(); Panel(340, 220, 760, 510, "ПОДТВЕРЖДЕНИЕ / ВРЕМЯ НА ПАУЗЕ");
            if (confirmation == ConfirmationKind.SelectMode)
            {
                Label(372, 290, 696, 64, "Вернуться к выбору режима?", theme.Title);
                Label(372, 386, 696, 112, "Незавершённый день будет начат заново. Завершённые дни обоих режимов останутся в отдельных сохранениях. При ошибке записи сначала повторите сохранение.", theme.Text);
                if (Button(372, 546, 696, 52, "Остаться / Esc", true, true)) CloseUtilityOverlay();
                if (Button(372, 626, 696, 52, "Выбрать режим", saveSucceeded)) { CloseUtilityOverlay(); selectingMode = true; }
                return;
            }
            if (confirmation == ConfirmationKind.EndCall)
            {
                Label(372, 290, 696, 64, "Завершить звонок: " + day.Current.Name + "?", theme.Title);
                Label(372, 386, 696, 112, "Сегодня этому клиенту больше не позвонить. Сделка не заключена, комиссия не начислится. Можно вернуться в разговор — его условия и черновик сохранятся.", theme.Text);
                if (Button(372, 546, 696, 52, "Продолжить разговор / Esc", true, true)) CloseUtilityOverlay();
                if (Button(372, 626, 696, 52, "Да, завершить без сделки", day.Call == Broker.Core.CallStatus.Active && !day.DayEnded))
                {
                    CloseUtilityOverlay(); CancelPending(); message = "";
                    PresentClientReply(day.EndCall());
                }
                return;
            }
            bool wholeCareer = confirmation == ConfirmationKind.NewCareer;
            Label(372, 290, 696, 64, wholeCareer ? "Начать новую кампанию?" : "Начать этот день заново?", theme.Title);
            Label(372, 386, 696, 112, wholeCareer
                ? (IsEmployeeMode ? "Будет сброшена только вводная глава наёмного брокера: личные деньги, отношения и результаты. Сохранение владельца не изменится." : "Будет сброшена только кампания владельца: деньги, долг, отношения и результаты. Сохранение наёмного брокера не изменится.")
                : "Все действия текущего дня будут потеряны: звонки, покупки и поручения. Завершённые дни сохранятся. Утро текущего дня начнётся заново.", theme.Text);
            if (Button(372, 546, 696, 52, "Нет, вернуться / Esc", true, true)) CloseUtilityOverlay();
            if (Button(372, 626, 696, 52, wholeCareer ? "Да, сбросить кампанию" : "Да, начать день заново"))
            {
                CloseUtilityOverlay();
                if (wholeCareer) NewCareer(); else Restart();
            }
        }

        private void DrawVoiceSetup()
        {
            Scrim(); Panel(240, 76, 960, 808, "ПРОВЕРКА ГОЛОСА / ИГРОВОЕ ВРЕМЯ НА ПАУЗЕ");
            Label(272, 132, 896, 52, "1. Микрофон — без сервера, ключа и внешних запросов", theme.Heading);
            string device = string.IsNullOrEmpty(voice.SelectedDevice) ? "Микрофон не найден" : voice.SelectedDevice;
            if (device.Length > 80) device = device.Substring(0, 77) + "…";
            Label(272, 190, 896, 44, "Устройство: " + device, theme.Text);
            bool idle = !voice.Recording && !voice.Working;
            if (Button(272, 244, 438, 40, "Следующий микрофон", idle && voice.Devices.Length > 1)) voice.SelectNextDevice();
            if (Button(730, 244, 438, 40, "Обновить список", idle)) { voice.Cancel(); voice.RefreshDevices(); }
            Label(272, 298, 896, 36, "Запись только по кнопке, до 20 секунд. Для прослушивания лучше надеть наушники.", theme.Small);
            if (Button(272, 344, 438, 48, voice.Recording ? "Закончить тестовую запись" : "Записать тест микрофона", !voice.Working, true))
            { if (voice.Recording) voice.FinishRecording(); else voice.BeginRecording(true); }
            if (Button(730, 344, 438, 48, "Прослушать запись", idle && voice.HasLocalPreview)) voice.PlayLocalPreview();
            // RMS is amplified visually only. This does not modify recorded samples.
            Meter(272, 406, 896, voice.InputLevel * 5f, voice.InputLevel >= 0.7f ? theme.Red : theme.Green);
            Label(272, 424, 896, 66, voice.Status, theme.Small);
            Label(272, 510, 896, 40, "2. Локальный шлюз — проверка доступности, не ключа API", theme.Heading);
            if (Button(272, 560, 438, 44, "Проверить сервер :8787", idle)) voice.CheckGateway();
            if (Button(730, 560, 438, 44, "Остановить / удалить тестовую запись")) voice.Cancel();
            Label(272, 620, 896, 84, voice.GatewayStatus, theme.Text);
            Label(272, 718, 896, 64, string.IsNullOrEmpty(voice.LastFailure)
                ? "Тестовая запись хранится только в памяти и удаляется при закрытии окна. Здесь нет распознавания и отправки аудио. Для разговора с API включи голос отдельно на рабочем столе."
                : "Последняя ошибка голоса: " + voice.LastFailure, theme.Small);
            if (Button(272, 802, 896, 48, "Вернуться / Esc", true, true)) CloseUtilityOverlay();
        }
    }
}
