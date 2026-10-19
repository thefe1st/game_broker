using System;

namespace Broker.Core
{
    public enum WorkKind { Idle, Research, Leads, Break }

    public sealed class ClientRecord
    {
        public string Id { get; }
        public string Name { get; }
        public string Role { get; }
        public string Brief { get; }
        public int Commission { get; }
        public int Trust { get; internal set; }
        public CallStatus Call { get; internal set; }
        public bool NeedsKnown { get; internal set; }
        public bool RisksExplained { get; internal set; }
        public bool FeesExplained { get; internal set; }
        public bool SpecialDiscussed { get; internal set; }
        public bool Researched { get; internal set; }
        public bool Returning { get; internal set; }
        public bool ComplaintPending { get; internal set; }
        public bool ComplaintAcknowledged { get; internal set; }
        public bool ComplaintRiskExplained { get; internal set; }
        public bool ComplaintResolved { get; internal set; }
        public bool FalseGuaranteeMade { get; internal set; }
        public bool ReportReviewed { get; internal set; } = true;
        public bool DealClosed { get; internal set; }
        public bool ObjectionRaised { get; internal set; }
        public bool ObjectionResolved { get; internal set; }
        public bool PressureWarned { get; internal set; }
        public bool PressureRecovered { get; internal set; }
        public bool PressureTrustLost { get; internal set; }
        public int UnclearReplies { get; internal set; }
        public ClientRecord(string id, string name, string role, string brief, int commission, int trust)
        { Id = id; Name = name; Role = role; Brief = brief; Commission = commission; Trust = trust; }
    }

    public sealed class EmployeeRecord
    {
        public string Name { get; }
        public string Trait { get; }
        public int Morale { get; internal set; } = 65;
        public WorkKind Work { get; internal set; }
        public float Remaining { get; internal set; }
        public float Duration { get; internal set; }
        public int TargetClient { get; internal set; }
        public bool SideDealDone { get; internal set; }
        public EmployeeRecord(string name, string trait) { Name = name; Trait = trait; }
    }

    public sealed class OfficeEvent
    {
        public int Id { get; }
        public string Title { get; }
        public string Description { get; }
        public string FirstChoice { get; }
        public int FirstChoiceCost { get; }
        public string SecondChoice { get; }
        public OfficeEvent(int id, string title, string description, string first, string second, int firstCost)
        { Id = id; Title = title; Description = description; FirstChoice = first; SecondChoice = second; FirstChoiceCost = firstCost; }
    }
}
