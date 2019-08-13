using Newtonsoft.Json;

namespace TraktToPlex.Plex.Models
{
    public interface IMediaItem : IHasId
    {
        [JsonProperty("title")]
        string Title { get; set; }
        string ExternalProvider { get; set; }
        string ExternalProviderId { get; set; }
    }
}
