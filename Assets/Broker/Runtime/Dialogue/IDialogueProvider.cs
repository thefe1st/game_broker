using System;
using System.Threading;
using System.Threading.Tasks;

namespace Broker.Runtime.Dialogue
{
    [Serializable]
    public sealed class DialogueTurn
    {
        public string role;
        public string text;
        public DialogueTurn(string role, string text) { this.role = role; this.text = text; }
    }

    [Serializable]
    public sealed class DialogueRequest
    {
        public string clientId = "boris";
        public string message;
        public string canonicalReply;
        public DialogueTurn[] history;
    }

    public interface IDialogueProvider
    {
        Task<string> ReplyAsync(DialogueRequest request, CancellationToken cancellationToken);
    }

    public sealed class OfflineDialogueProvider : IDialogueProvider
    {
        public Task<string> ReplyAsync(DialogueRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(request.canonicalReply);
        }
    }
}
