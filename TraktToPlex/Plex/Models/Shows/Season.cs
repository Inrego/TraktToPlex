using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TraktToPlex.Plex.Models.Shows
{
    public class Season
    {
        [JsonProperty("ratingKey")]
        public string Id { get; set; }
        [JsonProperty("index")]
        public int No { get; set; }

        public Episode[] Episodes { get; set; }
    }
}
