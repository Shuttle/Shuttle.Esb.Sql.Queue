namespace Shuttle.Esb.Sql.Queue;

public class SqlQueueOptions
{
    public const string SectionName = "Shuttle:SqlQueue";

    public string ConnectionStringName { get; set; } = string.Empty;
    public string Schema { get; set; } = "dbo";
}