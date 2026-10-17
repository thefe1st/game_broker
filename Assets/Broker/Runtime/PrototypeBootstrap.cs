using System;
using System.Collections.Generic;
using System.Threading;
using Broker.Runtime.Dialogue;
using Broker.Runtime.Voice;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime
{
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        private BrokerDay day;
        private readonly List<DialogueTurn> history = new List<DialogueTurn>();
        private CancellationTokenSource pending;
        private int revision;
        private bool busy;
        private bool useGateway;
        private bool voiceEnabled;
        private VoiceSession voice;
        private bool lastDayEnded;
        private readonly List<string> log = new List<string>();
        private string message = "";
        private Vector2 scroll;
        private GUIStyle wrappedLabel;
        private GUIStyle titleStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (UnityEngine.Object.FindFirstObjectByType<PrototypeBootstrap>() == null)
                new GameObject("Broker Prototype").AddComponent<PrototypeBootstrap>();
        }

        private void Awake()
        {
            voice = gameObject.AddComponent<VoiceSession>();
            voice.OnTranscript = text =>
            {
                if (!day.DayEnded && day.Call == CallStatus.Active && !busy) message = text;
            };
            Restart();
        }
        private void Update()
        {
            if (!busy && (voice == null || !voice.Working)) day.AdvanceTime(Time.unscaledDeltaTime);
            if (day.DayEnded && !lastDayEnded) { CancelPending(); lastDayEnded = true; }
        }

        private void OnDestroy() { CancelPending(); }

        private void CancelPending()
        {
            revision++;
            if (voice != null) voice.Cancel();
            pending?.Cancel();
            pending = null;
            busy = false;
        }

        private async void SendMessageToClient()
        {
            if (busy || day.DayEnded || day.Call != CallStatus.Active || string.IsNullOrWhiteSpace(message)
                || (voice != null && (voice.Recording || voice.Working))) return;
            voice.Cancel();
            string input = message.Trim();
            message = "";
            Add("Вы: " + input);
            // Local rules commit first. AI only rephrases the canonical answer.
            string canonical = day.Say(input);
            var request = new DialogueRequest { message = input, canonicalReply = canonical, history = history.ToArray() };
            history.Add(new DialogueTurn("player", input));
            var source = new CancellationTokenSource();
            pending = source;
            int version = ++revision;
            busy = true;
            try
            {
                IDialogueProvider provider = useGateway
                    ? (IDialogueProvider)new LocalGatewayDialogueProvider()
                    : new OfflineDialogueProvider();
                string reply = await provider.ReplyAsync(request, source.Token);
                if (version != revision || day.DayEnded) return;
                PresentClientReply(reply);
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (version != revision || day.DayEnded) return;
                Add("[Шлюз недоступен или ответ некорректен — локальный ответ]");
                PresentClientReply(canonical);
            }
            finally
            {
                source.Dispose();
                if (version == revision) { pending = null; busy = false; }
            }
        }

        private void PresentClientReply(string reply)
        {
            Add(reply);
            RememberClient(reply);
            if (voiceEnabled && !day.DayEnded) voice.PlayReply(reply);
        }

        private void RememberClient(string reply)
        {
            history.Add(new DialogueTurn("client", reply));
            while (history.Count > 12) history.RemoveAt(0);
        }

        private void Restart()
        {
            CancelPending();
            day = new BrokerDay();
            lastDayEnded = false;
            history.Clear();
            log.Clear();
            log.Add("Офис: До конца дня нужно заработать 100 на аренду. На счету — ноль. Зато есть визитки генерального директора.");
            message = "";
            scroll = Vector2.zero;
        }

        private void Add(string line)
        {
            log.Add(line);
            scroll.y = float.MaxValue;
        }

        private void OnGUI()
        {
            if (wrappedLabel == null)
            {
                wrappedLabel = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 18 };
                titleStyle = new GUIStyle(wrappedLabel) { fontSize = 26, fontStyle = FontStyle.Bold };
            }
            float scale = Mathf.Max(0.1f, Mathf.Min(Screen.width / 1000f, Screen.height / 760f));
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUILayout.BeginArea(new Rect(20, 20, 960, 720), GUI.skin.box);
            GUILayout.Label("АЛЛО, ЭТО ДЕНЬГИ", titleStyle);
            GUILayout.Label("Правила по ключевым словам • голос через локальный сервер");
            GUI.enabled = !busy && day.Call == CallStatus.NotStarted && !day.DayEnded;
            useGateway = GUILayout.Toggle(useGateway, "Локальный шлюз :8787 (mock или ИИ — зависит от настроек сервера)");
            GUI.enabled = true;
            bool enabledNow = GUILayout.Toggle(voiceEnabled, "Голос: разрешить отправку записи внешнему API и синтез речи (нужен ключ на сервере)");
            if (enabledNow != voiceEnabled) { voiceEnabled = enabledNow; if (!voiceEnabled) voice.Cancel(); }
            if (voiceEnabled)
            {
                GUILayout.Label(voice.Status, wrappedLabel);
                if ((voice.Working || voice.Playing || voice.Recording) && GUILayout.Button("Остановить голос", GUILayout.Height(26))) voice.Cancel();
            }
            GUILayout.Label($"Деньги: {day.Cash}   Аренда: {BrokerDay.Rent}   Доверие: {day.Trust}/3   Время: {Mathf.CeilToInt(day.RemainingSeconds)} с", wrappedLabel);
            GUILayout.Space(8);
            GUILayout.Label("Борис • владелец автомоек. Продукт: вымышленный «Мойка-Капитал». Риск убытка, комиссия 2%. В прототипе параметры фиксированы.", wrappedLabel);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(210));
            foreach (string line in log)
            {
                GUILayout.Label(line, wrappedLabel);
                GUILayout.Space(10);
            }
            GUILayout.EndScrollView();

            if (!day.DayEnded)
            {
                if (day.Call == CallStatus.NotStarted)
                {
                    if (GUILayout.Button("Позвонить Борису", GUILayout.Height(38)))
                    {
                        string greeting = day.StartCall();
                        PresentClientReply(greeting);
                    }
                }
                else if (day.Call == CallStatus.Active)
                {
                    if (voiceEnabled)
                    {
                        GUI.enabled = !busy && !voice.Working;
                        if (GUILayout.Button(voice.Recording ? "Закончить запись и распознать" : "Записать реплику (до 20 секунд)", GUILayout.Height(34)))
                        {
                            if (voice.Recording) voice.FinishRecording(); else voice.BeginRecording();
                        }
                    }
                    GUI.enabled = !busy && !voice.Recording && !voice.Working;
                    message = GUILayout.TextField(message, 500, GUILayout.Height(34));
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Отправить", GUILayout.Height(38)) && !string.IsNullOrWhiteSpace(message))
                    {
                        SendMessageToClient();
                        GUI.FocusControl(null);
                    }
                    GUI.enabled = !busy && !voice.Recording && !voice.Working;
                    if (GUILayout.Button("Предложить сделку", GUILayout.Height(38))) PresentClientReply(day.OfferDeal());
                    GUI.enabled = true;
                    if (GUILayout.Button("Повесить трубку", GUILayout.Height(38)))
                    {
                        CancelPending();
                        PresentClientReply(day.EndCall());
                    }
                    GUILayout.EndHorizontal();
                }
                else GUILayout.Label("Звонок завершён. В этом срезе доступен только один клиент.");
                GUI.enabled = true;
                if (busy) GUILayout.Label("Клиент думает… Можно завершить звонок или день.");
                GUILayout.Space(8);
                if (GUILayout.Button("Завершить рабочий день", GUILayout.Height(34)))
                {
                    CancelPending();
                    day.EndDay();
                }
            }
            else
            {
                GUILayout.Label(day.RentCovered ? "Аренда покрыта. Контора проживёт ещё один день." : "Аренда не покрыта. Арендодатель ваш оптимизм не принимает.", wrappedLabel);
                GUILayout.Label($"Остаток / долг после аренды: {day.BalanceAfterRent}");
                if (GUILayout.Button("Начать заново", GUILayout.Height(38))) Restart();
            }
            GUILayout.EndArea();
            GUI.matrix = previous;
        }
    }
}
