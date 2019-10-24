using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using TraktToPlex.Plex;

namespace TraktToPlex.Models
{
    public class AuthViewModel
    {
        public string TraktKey { get; set; }
        public string PlexKey { get; set; }
        public string PlexServerKey { get; set; }
        public List<SelectListItem> PlexServers { get; set; }
    }
}
