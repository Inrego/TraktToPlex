using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TraktToPlex.Plex.Models.Shows
{
    public class Show : IMediaItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string ExternalProvider { get; set; }
        public string ExternalProviderId { get; set; }
        [JsonProperty("guid")]
        public string ExternalProviderInfo
        {
            get => null;
            set
            {
                var match = Regex.Match(value, @"\.(?<provider>[a-z]+)://(?<id>[^\?]+)");
                ExternalProvider = match.Groups["provider"].Value;
                ExternalProviderId = match.Groups["id"].Value;
            }
        }
        public Season[] Seasons { get; set; }
    }
}
