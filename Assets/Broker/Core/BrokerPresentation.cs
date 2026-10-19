namespace Broker.Core
{
    public enum ConversationStage { AwaitingCall, Report, Goal, Risk, Fee, Term, Service, Proposal, Objection, Trust, Ready, Closed, DayEnded }

    public static class BrokerPresentation
    {
        public static string ShortHint(ConversationStage stage, string clientId)
        {
            switch (stage)
            {
                case ConversationStage.AwaitingCall: return "Начни звонок";
                case ConversationStage.Report: return "Обсуди прошлый отчёт";
                case ConversationStage.Goal: return "Узнай цель клиента";
                case ConversationStage.Risk: return "Расскажи о риске";
                case ConversationStage.Fee: return "Назови комиссию 2%";
                case ConversationStage.Term: return "Срок — один год";
                case ConversationStage.Service: return "Предложи личное сопровождение";
                case ConversationStage.Proposal: return "Предложи сделку";
                case ConversationStage.Objection: return ClientConversation.AnswerIntent(clientId) == DialogueIntent.Reserve ? "Оставьте резерв" : ClientConversation.AnswerIntent(clientId) == DialogueIntent.Written ? "Условия — письменно" : "Останься на связи";
                case ConversationStage.Trust: return "Нужно больше доверия";
                case ConversationStage.Ready: return "Можно закрыть сделку";
                case ConversationStage.Closed: return "Выбери следующего клиента";
                default: return "День завершён";
            }
        }
        public static string TaskLabel(WorkKind work)
        {
            switch (work) { case WorkKind.Research: return "Досье"; case WorkKind.Leads: return "Обзвон"; case WorkKind.Break: return "Перерыв"; default: return "Без поручения"; }
        }
    }
}
