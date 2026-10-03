using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.Antra
{
    public class AntraIndexerSettingsValidator : AbstractValidator<AntraIndexerSettings>
    {
        public AntraIndexerSettingsValidator()
        {
            RuleFor(c => c.BaseUrl).ValidRootUrl();
            RuleFor(c => c.TimeoutSeconds).GreaterThan(0);
        }
    }

    public class AntraIndexerSettings : IIndexerSettings
    {
        private static readonly AntraIndexerSettingsValidator Validator = new AntraIndexerSettingsValidator();

        [FieldDefinition(0, Label = "Bridge URL", HelpText = "Base URL of the Antra bridge service, e.g. http://127.0.0.1:8890")]
        public string BaseUrl { get; set; } = "http://127.0.0.1:8890";

        [FieldDefinition(1, Label = "Bridge API Token", Type = FieldType.Textbox, Privacy = PrivacyLevel.ApiKey, Advanced = true, HelpText = "Only needed when BRIDGE_API_TOKEN is set on the bridge.")]
        public string BridgeApiToken { get; set; } = "";

        [FieldDefinition(2, Label = "Resolve Timeout", Type = FieldType.Number, Unit = "seconds", Advanced = true, HelpText = "How long the bridge may take to match and probe an album.")]
        public int TimeoutSeconds { get; set; } = 180;

        [FieldDefinition(3, Label = "Early Download Limit", Type = FieldType.Number, Unit = "days", Advanced = true, HelpText = "Time before release date Lidarr will download from this indexer. Empty is no limit.")]
        public int? EarlyReleaseLimit { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
