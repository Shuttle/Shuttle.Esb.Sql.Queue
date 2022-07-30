namespace Shuttle.Esb.Sql.Queue
{
    public class SqlQueueOptions
    {
        public const string SectionName = "Shuttle:ServiceBus:SqlQueue";

        public string ConnectionStringName { get; set; }
    }
}