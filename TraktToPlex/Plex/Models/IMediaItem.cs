using Newtonsoft.Json;

namespace TraktToPlex.Plex.Models
{
    public interface IMediaItem
    {
        [JsonProperty("ratingKey")]
        string Id { get; set; }
        [JsonProperty("title")]
        string Title { get; set; }
        string ExternalProvider { get; set; }
        string ExternalProviderId { get; set; }
    }
}
