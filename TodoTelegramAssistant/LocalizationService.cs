using Microsoft.Extensions.Localization;
using System.Diagnostics.CodeAnalysis;

namespace TodoTelegramAssistant
{
    public class LocalizationService
    {
        private readonly IStringLocalizer _localizer = null!;

        public LocalizationService(IStringLocalizerFactory factory) =>
        _localizer = factory.Create(typeof(LocalizationService));

        [return: NotNullIfNotNull("_localizer")]
        public string? GetFormattedMessage(string message)
        {
            LocalizedString localizedString = _localizer[message];
            return localizedString;
        }
    }
}
