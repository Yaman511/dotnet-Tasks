namespace Task2.model
{
    public class FileContent
    {
        public IFormFile? File { get; set; }
        public string FileName { get; set; }
        public string Owner { get; set; }
        public string? Description { get; set; }
        public QueryType QueryType { get; set; }
        public string CreationDate { get; set; }
        public string ModificationDate { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public SortType SortType { get; set; }
        public string[] Name { get; set; }
    }
}
