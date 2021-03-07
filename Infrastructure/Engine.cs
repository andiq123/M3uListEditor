using System;
using System.Net.Http;
using System.Threading.Tasks;
using Core.Models;
using Infrastructure.Functionalities;
using Infrastructure.Handlers;

namespace Infrastructure
{
    public class Engine
    {
        //Fields
        public EventHandler<ProgressReportModel> reportProgressEvent;
        private Args _argsModel = new Args { ExportPath = null, SourcePath = null };

        //Constructor
        public Engine() { }

        //Methods
        public void SetArgs(Args argsModel)
        {
            _argsModel = argsModel;
        }

        public async Task<FinalChannelReport> Start()
        {
            if (string.IsNullOrEmpty(_argsModel.ExportPath) || string.IsNullOrEmpty(_argsModel.ExportPath)) return null;

            var client = new HttpClient();
            client.Timeout = new TimeSpan(0, 0, int.Parse(_argsModel.TimeOut));
            var filterWorkingChannels = new FilterWorkingChannels(client, _argsModel.RemoveDoubles);

            Progress<ProgressReportModel> progress = new Progress<ProgressReportModel>();

            progress.ProgressChanged += reportProgress;
            var report = await filterWorkingChannels.Filter(_argsModel.SourcePath, _argsModel.ExportPath, progress);

            if (_argsModel.IsLinkSourcePath)
                FileHandler.RemoveFileIfExits(_argsModel.SourcePath);

            return report;
        }

        //Events
        private void reportProgress(object sender, ProgressReportModel e)
        {
            reportProgressEvent?.Invoke(sender, e);
        }

    }
}