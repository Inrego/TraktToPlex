using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TraktToPlex.Plex.Auth
{
    public class OAuthResponse
    {
        public string Url { get; set; }
        public string Id { get; set; }
        public string Code { get; set; }
    }
}
