using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TraktToPlex.Plex.Models.Shows
{
    public class Episode
    {
        [JsonProperty("ratingKey")]
        public string Id { get; set; }
        [JsonProperty("index")]
        public int No { get; set; }
        [JsonProperty("viewCount")]
        public int ViewCount { get; set; }
    }
}
