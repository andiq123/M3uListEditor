using System.Collections.Generic;
using System.Linq;
using Core.Models;

namespace Infrastructure.Functionalities
{
    public class CheckChannelsForDoubles
    {
        public static List<Channel> Check(ref List<Channel> channels)
        {
            List<Channel> channelsFiltered = new List<Channel>();
            foreach (var channel in channels)
            {
                var link = channelsFiltered.SingleOrDefault(x => x.Link == channel.Link);
                if (link == null)
                {
                    channelsFiltered.Add(channel);
                }
            }
            return channelsFiltered;
        }
    }
}