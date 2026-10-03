using System;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Indexers.Antra
{
    public class Antra : HttpIndexerBase<AntraIndexerSettings>
    {
        public override string Name => "Antra";

        public override string Protocol => nameof(AntraDownloadProtocol);

        public override bool SupportsRss => false;

        public override bool SupportsSearch => true;

        public override int PageSize => 100;

        public override TimeSpan RateLimit => TimeSpan.FromSeconds(2);

        public Antra(IHttpClient httpClient,
            IIndexerStatusService indexerStatusService,
            IConfigService configService,
            IParsingService parsingService,
            Logger logger)
            : base(httpClient, indexerStatusService, configService, parsingService, logger)
        {
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            return new AntraRequestGenerator
            {
                Settings = Settings
            };
        }

        public override IParseIndexerResponse GetParser()
        {
            return new AntraParser
            {
                Settings = Settings
            };
        }
    }
}
