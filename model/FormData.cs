namespace Task2.model
{

    public enum QueryType
    {
        Create,
        Update,
        Delete,
        Retrieve
    };
  

        public class FormData
        {
            public IFormFile? File { get; set; }
            public string FileName { get; set; }
            public string Owner { get; set; }
            public string? Description { get; set; }
            public QueryType QueryType { get; set;}
        }


    }

