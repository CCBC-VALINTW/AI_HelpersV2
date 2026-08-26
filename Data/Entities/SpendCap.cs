namespace AiHelpers.Data.Entities;

public class SpendCap
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public decimal MonthlyCapAmount { get; set; }
}
