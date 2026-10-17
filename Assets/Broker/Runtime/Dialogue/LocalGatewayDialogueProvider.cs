using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Broker.Runtime.Dialogue
{
    // Desktop/editor development only. No credentials in the game client.
    public sealed class LocalGatewayDialogueProvider : IDialogueProvider
    {
        [Serializable] private sealed class ReplyEnvelope { public string reply; }

        public async Task<string> ReplyAsync(DialogueRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var web = new UnityWebRequest("http://127.0.0.1:8787/dialogue", "POST"))
            {
                web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(request)));
                web.downloadHandler = new DownloadHandlerBuffer();
                web.SetRequestHeader("Content-Type", "application/json");
                web.timeout = 12;
                var operation = web.SendWebRequest();
                while (!operation.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        web.Abort();
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    await Task.Yield();
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (web.result != UnityWebRequest.Result.Success)
                    throw new InvalidOperationException("Dialogue gateway unavailable");
                if (web.downloadHandler.data.Length > 8192)
                    throw new InvalidOperationException("Dialogue response too large");
                var result = JsonUtility.FromJson<ReplyEnvelope>(web.downloadHandler.text);
                if (result == null || string.IsNullOrWhiteSpace(result.reply) || result.reply.Length > 400)
                    throw new InvalidOperationException("Invalid dialogue response");
                return result.reply;
            }
        }
    }
}
