using System;

namespace Broker.Core
{
    // Engine-free diagnostics. Never returns provider text, credentials or audio data.
    public static class VoiceDiagnostics
    {
        public static float Rms(float[] samples)
        {
            if (samples == null || samples.Length == 0) return 0f;
            double sum = 0;
            foreach (float sample in samples)
            {
                float value = float.IsNaN(sample) || float.IsInfinity(sample) ? 0f : Math.Max(-1f, Math.Min(1f, sample));
                sum += (double)value * value;
            }
            return (float)Math.Sqrt(sum / samples.Length);
        }

        public static string SignalHint(float rms)
        {
            if (float.IsNaN(rms) || float.IsInfinity(rms) || rms < 0.003f)
                return "Почти тишина: проверь устройство, разрешения и громкость микрофона.";
            if (rms >= 0.7f) return "Очень громкий сигнал: уменьши усиление микрофона.";
            return "Сигнал есть. Прослушай запись: индикатор не отличает речь от шума.";
        }

        public static string HealthHint(string status, string mode, string voice)
        {
            if (status != "ok" || (mode != "mock" && mode != "ai") || (voice != "disabled" && voice != "ai"))
                return "Сервер ответил в неизвестном формате. Проверь версию шлюза и порт 8787.";
            string dialogue = mode == "ai" ? "Диалоги: ИИ." : "Диалоги: локальные.";
            return voice == "ai"
                ? "Шлюз доступен. " + dialogue + " Голос включён. Ключ и оплата API этим тестом НЕ проверяются."
                : "Шлюз доступен. " + dialogue + " Голос выключен: задай VOICE_MODE=ai в Server/.env и перезапусти сервер.";
        }

        public static string RequestFailure(long httpCode)
        {
            switch (httpCode)
            {
                case 400: return "Шлюз отклонил формат запроса (400). Проверь версию проекта и сервера.";
                case 403: return "Шлюз отклонил локальное подключение (403). Используй сервер на этом же компьютере.";
                case 404: case 405: return "Аудиомаршрут не найден (404/405). Обнови папку Server и перезапусти сервер.";
                case 413: return "Аудиозапрос слишком большой (413). Попробуй более короткую реплику.";
                case 415: return "Шлюз не принял формат аудио (415). Нужен PCM16 WAV.";
                case 429: return "Слишком много запросов (429). Подожди минуту и повтори.";
                case 502: return "Ошибка внешнего API (502). Проверь локально ключ, оплату, URL, модели и поддержку аудио.";
                case 503: return "Голос не настроен (503). Задай VOICE_MODE=ai в Server/.env и перезапусти сервер.";
                case 504: return "Шлюз не дождался внешнего API (504). Попробуй позже или сократи реплику.";
                case 0: return "Нет связи со шлюзом или истекло время ожидания. Запусти Server: npm.cmd start; порт 8787.";
                default: return "Не удалось получить ответ шлюза. Проверь сервер и совместимость API.";
            }
        }
    }
}
