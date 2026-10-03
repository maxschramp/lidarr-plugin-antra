using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;
using NzbDrone.Core.Validation;
using NzbDrone.Plugin.Antra;

namespace NzbDrone.Core.Download.Clients.Antra
{
    public class Antra : DownloadClientBase<AntraSettings>
    {
        private readonly IAntraProxy _proxy;

        public Antra(IAntraProxy proxy,
            IConfigService configService,
            IDiskProvider diskProvider,
            IRemotePathMappingService remotePathMappingService,
            ILocalizationService localizationService,
            Logger logger)
            : base(configService, diskProvider, remotePathMappingService, localizationService, logger)
        {
            _proxy = proxy;
        }

        public override string Protocol => nameof(AntraDownloadProtocol);

        public override string Name => "Antra";

        public override IEnumerable<DownloadClientItem> GetItems()
        {
            var queue = _proxy.GetQueue(Settings);

            foreach (var item in queue)
            {
                item.DownloadClientInfo = DownloadClientItemClientInfo.FromDownloadClient(this, false);
                item.OutputPath = _remotePathMappingService.RemapRemoteToLocal(Settings.Host, item.OutputPath);
            }

            return queue;
        }

        public override void RemoveItem(DownloadClientItem item, bool deleteData)
        {
            if (deleteData)
            {
                DeleteItemData(item);
            }

            _proxy.RemoveFromQueue(item.DownloadId, Settings);
        }

        public override Task<string> Download(RemoteAlbum remoteAlbum, IIndexer indexer)
        {
            return _proxy.Download(remoteAlbum, Settings);
        }

        public override DownloadClientInfo GetStatus()
        {
            return new DownloadClientInfo
            {
                IsLocalhost = Settings.Host == "127.0.0.1" || Settings.Host == "localhost",
                OutputRootFolders = new List<OsPath>
                {
                    _remotePathMappingService.RemapRemoteToLocal(Settings.Host, new OsPath(Settings.StagingPath))
                }
            };
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            try
            {
                _proxy.TestConnection(Settings);
            }
            catch (Exception ex)
            {
                failures.Add(new ValidationFailure("BridgeUrl", $"Antra bridge is not reachable: {ex.Message}"));
            }

            failures.AddIfNotNull(TestFolder(Settings.StagingPath, "StagingPath"));
        }
    }
}
