using System;
using System.Net.Http;
using System.Threading.Tasks;
using Core.Models;
using Infrastructure.Handlers;

namespace Infrastructure.Functionalities
{
    public class FilterWorkingChannels
    {
        private readonly SignalTester _signalTester;
        private readonly bool _removeDoubles;
        public FilterWorkingChannels(HttpClient client, bool removeDoubles)
        {
            _removeDoubles = removeDoubles;
            _signalTester = new SignalTester(client);
        }
        public async Task<FinalChannelReport> Filter(string path, string exportPath, IProgress<ProgressReportModel> progress)
        {
            ProgressReportModel report = new ProgressReportModel();

            var doubleRemovedAndReport = await ChannelGathering.GetChannels(path, _removeDoubles);

            report.ChannelsCountTotal = doubleRemovedAndReport.Channels.Count;

            foreach (var channel in doubleRemovedAndReport.Channels)
            {
                if (await _signalTester.IsLinkAlive(channel.Link))
                {
                    report.WorkingChannels.Add(channel);
                }
                else
                {
                    report.NotWorkingChannelsCount++;
                }

                progress.Report(report);
            }

            foreach (var channel in doubleRemovedAndReport.Channels)
            {
                FileHandler.AddChannel(exportPath, channel);
            }

            return new FinalChannelReport { TotalChannelsCount = doubleRemovedAndReport.Channels.Count, WorkingChannelsCount = report.WorkingChannels.Count, DoublesRemovedCount = doubleRemovedAndReport.DoublesRemoved };

        }
    }
}