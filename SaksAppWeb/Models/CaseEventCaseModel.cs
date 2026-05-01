namespace SaksAppWeb.Models;

public class CaseEventCase : SoftDeletableEntity
{
    public int Id { get; set; }

    public int CaseEventId { get; set; }
    public CaseEvent CaseEvent { get; set; } = null!;

    public int BoardCaseId { get; set; }
    public BoardCase BoardCase { get; set; } = null!;
}
