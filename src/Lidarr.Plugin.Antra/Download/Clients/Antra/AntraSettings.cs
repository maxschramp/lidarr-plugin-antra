using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;

namespace NzbDrone.Core.Download.Clients.Antra
{
    public class AntraSettingsValidator : AbstractValidator<AntraSettings>
    {
        public AntraSettingsValidator()
        {
            RuleFor(c => c.BridgeUrl).ValidRootUrl();
            RuleFor(c => c.StagingPath).IsValidPath();
            RuleFor(c => c.TimeoutSeconds).GreaterThan(0);
        }
    }

    public class AntraSettings : IProviderConfig
    {
        private static readonly AntraSettingsValidator Validator = new AntraSettingsValidator();

        [FieldDefinition(0, Label = "Bridge URL", HelpText = "Base URL of the Antra bridge service, e.g. http://127.0.0.1:8890")]
        public string BridgeUrl { get; set; } = "http://127.0.0.1:8890";

        [FieldDefinition(1, Label = "Bridge API Token", Type = FieldType.Textbox, Privacy = PrivacyLevel.ApiKey, Advanced = true, HelpText = "Only needed when BRIDGE_API_TOKEN is set on the bridge.")]
        public string BridgeApiToken { get; set; } = "";

        [FieldDefinition(2, Label = "Staging Path", Type = FieldType.Path, HelpText = "Directory the bridge downloads into, as seen from this Lidarr instance. Used as the import source and as the path-mapping target.")]
        public string StagingPath { get; set; } = "";

        [FieldDefinition(3, Label = "Host", Advanced = true, HelpText = "Host name used for remote path mappings. Use 'localhost' when Lidarr shares the filesystem with the bridge.")]
        public string Host { get; set; } = "localhost";

        [FieldDefinition(4, Label = "Preferred Format", Advanced = true, HelpText = "Antra output format: flac (best lossless), lossless-24, lossless-16, alac, mp3, ...")]
        public string Format { get; set; } = "flac";

        [FieldDefinition(5, Label = "Sources", Advanced = true, HelpText = "auto, or a comma list to restrict the source chain (apple, hifi, qobuz, deezer, youtube, jiosaavn).")]
        public string Sources { get; set; } = "auto";

        [FieldDefinition(6, Label = "Request Timeout", Type = FieldType.Number, Unit = "seconds", Advanced = true)]
        public int TimeoutSeconds { get; set; } = 120;

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
