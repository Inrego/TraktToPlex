using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TraktToPlex.Plex.Auth;
using TraktToPlex.Plex.Models;
using TraktToPlex.Plex.Models.Shows;

namespace TraktToPlex.Plex
{
    public class PlexClient
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _clientId;
        public PlexClient(IConfiguration config)
        {
            _config = config;
            _clientId = _config["PlexConfig:ClientSecret"];
            _httpClient.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
            _httpClient.DefaultRequestHeaders.Add("X-Plex-Product", "Trakt To Plex");
            _httpClient.DefaultRequestHeaders.Add("X-Plex-Platform", "Web");
            _httpClient.DefaultRequestHeaders.Add("X-Plex-Device", "Trakt To Plex (Web)");
            _httpClient.DefaultRequestHeaders.Add("X-Plex-Client-Identifier", _clientId);
        }
        public async Task<OAuthResponse> GetOAuthUrl(string redirectUrl)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, "https://plex.tv/api/v2/pins.json?strong=true"))
            {
                var resp = await _httpClient.SendAsync(request);
                var respStr = await resp.Content.ReadAsStringAsync();
                dynamic respJson = JsonConvert.DeserializeObject(respStr);
                return new OAuthResponse
                {
                    Url = $"https://app.plex.tv/auth#?context[device][product]=Trakt%20To%20Plex&context[device][environment]=bundled&context[device][layout]=desktop&context[device][platform]=Web&context[device][device]=Trakt%20To%20Plex%20(Web)&clientID={_clientId}&forwardUrl={redirectUrl}&code={respJson.code}",
                    Id = respJson.id
                };
            }
        }

        public async Task<string> GetAuthToken(string oauthId)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, "https://plex.tv/api/v2/pins/" + oauthId))
            {
                var resp = await _httpClient.SendAsync(request);
                var respStr = await resp.Content.ReadAsStringAsync();
                dynamic respJson = JsonConvert.DeserializeObject(respStr);
                return respJson.authToken;
            }
        }

        public void SetAuthToken(string authToken)
        {
            _httpClient.DefaultRequestHeaders.Add("X-Plex-Token", authToken);
        }

        public void SetPlexServerUrl(string url)
        {
            _httpClient.BaseAddress = new Uri(url);
        }

        public async Task<Server[]> GetServers()
        {
            var servers = new List<Server>();
            using (var request = new HttpRequestMessage(HttpMethod.Get, "https://plex.tv/api/resources"))
            {
                var resp = await _httpClient.SendAsync(request);
                var respStr = await resp.Content.ReadAsStringAsync();
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(respStr);
                var container = xmlDoc.ChildNodes.OfType<XmlNode>().FirstOrDefault(x => x.Name == "MediaContainer");
                if (container == null)
                    return new Server[0];
                foreach (var serverNode in container.ChildNodes.OfType<XmlNode>().Where(x => x.Attributes["product"].Value == "Plex Media Server" && x.ChildNodes.OfType<XmlNode>().Any(y => y.Attributes["local"].Value == "0")))
                {
                    servers.Add(new Server
                    {
                        Name = serverNode.Attributes["name"].Value,
                        Id = serverNode.Attributes["clientIdentifier"].Value,
                        Url = serverNode.ChildNodes.OfType<XmlNode>().First(x => x.Attributes["local"].Value == "0").Attributes["uri"].Value
                    });
                }

                return servers.ToArray();
            }
        }

        public async Task<Movie[]> GetMovies()
        {
            var sections = await GetSections();
            var movieSections = sections.Where(x => x.Type == "movie");
            var movies = new List<Movie>();
            foreach (var movieSection in movieSections)
            {
                var plexMovies = await GetMovies(movieSection.Id);
                if (plexMovies != null)
                    movies.AddRange(plexMovies);
            }

            return movies.ToArray();
        }

        private async Task<Movie[]> GetMovies(string sectionId)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, $"/library/sections/{sectionId}/all"))
            {
                var resp = await _httpClient.SendAsync(request);
                var respStr = await resp.Content.ReadAsStringAsync();
                var jObj = JObject.Parse(respStr);
                return jObj["MediaContainer"]["Metadata"]?.ToObject<Movie[]>();
            }
        }

        public async Task<Show[]> GetShows()
        {
            var sections = await GetSections();
            var tvSections = sections.Where(x => x.Type == "show");
            var shows = new List<Show>();
            foreach (var tvSection in tvSections)
            {
                var plexShows = await GetShows(tvSection.Id);
                if (plexShows != null)
                    shows.AddRange(plexShows);
            }

            return shows.ToArray();
        }

        private async Task<Section[]> GetSections()
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/library/sections"))
            {
                var resp = await _httpClient.SendAsync(request);
                var respStr = await resp.Content.ReadAsStringAsync();
                var jObj = JObject.Parse(respStr);
                var sections = (JArray)jObj["MediaContainer"]["Directory"];
                return sections.ToObject<Section[]>();
            }
        }

        private async Task<Show[]> GetShows(string sectionId)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, $"/library/sections/{sectionId}/all"))
            {
                var resp = await _httpClient.SendAsync(request);
                var respStr = await resp.Content.ReadAsStringAsync();
                var jObj = JObject.Parse(respStr);
                return jObj["MediaContainer"]["Metadata"]?.ToObject<Show[]>();
            }
        }

        public async Task PopulateSeasons(Show show)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, $"/library/metadata/{show.Id}/children"))
            {
                var resp = await _httpClient.SendAsync(request);
                var respStr = await resp.Content.ReadAsStringAsync();
                var jObj = JObject.Parse(respStr);
                show.Seasons = jObj["MediaContainer"]["Metadata"].ToObject<Season[]>();
            }
        }

        public async Task PopulateEpisodes(Season season)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, $"/library/metadata/{season.Id}/children"))
            {
                var resp = await _httpClient.SendAsync(request);
                var respStr = await resp.Content.ReadAsStringAsync();
                var jObj = JObject.Parse(respStr);
                season.Episodes = jObj["MediaContainer"]["Metadata"].ToObject<Episode[]>();
            }
        }

        public async Task Scrobble(IHasId item)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get,
                "/:/scrobble?identifier=com.plexapp.plugins.library&key=" + item.Id))
            {
                var resp = await _httpClient.SendAsync(request);
                var respStr = await resp.Content.ReadAsStringAsync();
            }
        }
    }
}
