using System.Collections.Generic;

namespace Core.Models
{
    public class DoubleDeleteReportAndChannels
    {
        public int DoublesRemoved { get; set; }
        public IReadOnlyList<Channel> Channels { get; set; }
    }
}