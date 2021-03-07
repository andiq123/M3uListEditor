namespace Core.Models
{
    public class Args
    {
        public string TimeOut { get; set; } = "10";
        public bool IsLinkSourcePath { get; set; } = false;
        public string SourcePath { get; set; }
        public string ExportPath { get; set; }
        public bool RemoveDoubles { get; set; } = true;
    }
}