using System.Collections.Generic;

namespace Core.Models
{
    public class ProgressReportModel
    {
        public int PercentageCompleted { get; set; } = 0;
        public int ChannelsCountTotal { get; set; } = 0;
        public int NotWorkingChannelsCount { get; set; } = 0;
        public List<Channel> WorkingChannels { get; set; } = new List<Channel>();

    }
}