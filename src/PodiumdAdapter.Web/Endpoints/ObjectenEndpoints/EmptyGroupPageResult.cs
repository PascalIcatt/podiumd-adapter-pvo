public class EmptyGroupPageResult
{
    public int Count { get; set; }
    public string? Next { get; set; }
    public string? Previous { get; set; }
    public required object[] Results { get; set; }
}
