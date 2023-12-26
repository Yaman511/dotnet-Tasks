namespace Task2.model
{

    public enum SortType
    {
        Ascending,
        Descending
    };
    public class FilterByDateFormData
    {
        public string Owner { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public SortType? SortType { get; set; }
    }
}
