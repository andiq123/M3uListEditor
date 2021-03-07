using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Core.Models;
using Infrastructure.Handlers;

namespace Infrastructure.Functionalities
{
    public class ChannelGathering
    {
        public async static Task<DoubleDeleteReportAndChannels> GetChannels(string path, bool removeDoubles)
        {
            try
            {
                List<Channel> channels = new List<Channel>();
                var lines = await File.ReadAllLinesAsync(path);

                var names = lines.Where(x => x.Contains("#EXTINF:") || x.Contains("#EXTUBF:")).ToArray();
                var groupNames = lines.Where(x => x.Contains("#EXTGRP:")).ToArray();
                var links = lines.Where(x => !String.IsNullOrEmpty(x) && (x.Substring(0, 4) == "http" || x.Substring(0, 6) == "plugin")).ToArray();

                if (groupNames.Count() > 0)
                {
                    //Populate if there are groups
                    ErrorHandling.CheckForUnEqualAmounts(names.Count(), groupNames.Count(), links.Count());
                    for (int i = 0; i < names.Count(); i++)
                    {
                        channels.Add(new Channel { Id = i, Name = names[i], GroupName = groupNames[i], Link = links[i] });
                    }
                }
                else
                {
                    //Populate without groups
                    ErrorHandling.CheckForUnEqualAmounts(names.Count(), links.Count());
                    for (int i = 0; i < names.Count(); i++)
                    {
                        channels.Add(new Channel { Id = i, Name = names[i], Link = links[i] });
                    }
                }

                var channelsBeforeDeletionCount = 0;
                if (removeDoubles)
                {
                    channelsBeforeDeletionCount = channels.Count;
                    channels = CheckChannelsForDoubles.Check(ref channels);
                    channelsBeforeDeletionCount -= channels.Count;
                }

                return new DoubleDeleteReportAndChannels { Channels = channels, DoublesRemoved = channelsBeforeDeletionCount };
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
    }
}