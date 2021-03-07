namespace Core.Models
{
    public class Channel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string GroupName { get; set; } = "";
        public string Link { get; set; }
    }
}