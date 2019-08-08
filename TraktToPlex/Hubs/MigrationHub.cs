using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using TraktNet;
using TraktNet.Objects.Authentication;
using TraktNet.Objects.Basic;
using TraktNet.Requests.Parameters;
using TraktToPlex.Plex;
using TraktToPlex.Plex.Models;
using TraktToPlex.Plex.Models.Shows;

namespace TraktToPlex.Hubs
{
    public class MigrationHub : Hub
    {
        private readonly IConfiguration _config;
        private readonly PlexClient _plexClient;
        private readonly TraktClient _traktClient;

        public MigrationHub(IConfiguration config, PlexClient plexClient)
        {
            _config = config;
            _plexClient = plexClient;
            _traktClient = new TraktClient(_config["TraktConfig:ClientId"], _config["TraktConfig:ClientSecret"]);
        }

        public async Task StartMigration(string traktKey, string plexKey, string plexUrl)
        {
            _plexClient.SetAuthToken(plexKey);
            _plexClient.SetPlexServerUrl(plexUrl);

            _traktClient.Authorization = TraktAuthorization.CreateWith(traktKey);

            await MigrateTvShows();
            await ReportProgress( "--------------------------------------------");
            await ReportProgress( "Finished migrating TV Shows!");
            await ReportProgress( "--------------------------------------------");
        }

        private async Task MigrateTvShows()
        {
            await ReportProgress( "Importing Trakt shows..");
            var traktShows = (await _traktClient.Sync.GetWatchedShowsAsync(new TraktExtendedInfo().SetFull())).ToArray();
            await ReportProgress( $"Found {traktShows.Length} shows on Trakt");

            var plexShows = await _plexClient.GetShows();
            await ReportProgress( $"Found {plexShows.Length} shows on Plex");
            await ReportProgress( "Going through all shows on Plex, to see if we find a match on Trakt..");
            var i = 0;
            foreach (var plexShow in plexShows)
            {
                i++;
                var traktShow = traktShows.FirstOrDefault(x => HasMatchingId(plexShow, x.Ids));
                if (traktShow == null)
                {
                    await ReportProgress( $"({i}/{plexShows.Length}) The show \"{plexShow.Title}\" was not found as watched on Trakt. Skipping!");
                    continue;
                }
                await ReportProgress( $"({i}/{plexShows.Length}) Found the show \"{plexShow.Title}\" as watched on Trakt. Processing!");
                await _plexClient.PopulateSeasons(plexShow);
                foreach (var traktSeason in traktShow.WatchedSeasons.Where(x => x.Number.HasValue))
                {
                    var scrobbleEpisodes = new List<Episode>();
                    var plexSeason = plexShow.Seasons.FirstOrDefault(x => x.No == traktSeason.Number);
                    if (plexSeason == null)
                        continue;
                    await _plexClient.PopulateEpisodes(plexSeason);
                    foreach (var traktEpisode in traktSeason.Episodes.Where(x => x.Number.HasValue))
                    {
                        var plexEpisode = plexSeason.Episodes.FirstOrDefault(x => x.No == traktEpisode.Number);
                        if (plexEpisode == null || plexEpisode.ViewCount > 0)
                            continue;
                        scrobbleEpisodes.Add(plexEpisode);
                    }
                    await ReportProgress( $"Marking {scrobbleEpisodes.Count} episodes as watched in season {plexSeason.No} of \"{plexShow.Title}\"..");
                    await Task.WhenAll(scrobbleEpisodes.Select(_plexClient.Scrobble));
                    await ReportProgress( "Done!");
                }
            }
        }

        private bool HasMatchingId(IMediaItem plexItem, ITraktIds traktIds)
        {
            switch (plexItem.ExternalProvider)
            {
                case "imdb":
                    return plexItem.ExternalProviderId.Equals(traktIds.Imdb);
                case "tmdb":
                    return uint.TryParse(plexItem.ExternalProviderId, out var tmdbId) && tmdbId.Equals(traktIds.Tmdb);
                case "thetvdb":
                    return uint.TryParse(plexItem.ExternalProviderId, out var tvdbId) && tvdbId.Equals(traktIds.Tvdb);
                case "tvrage":
                    return uint.TryParse(plexItem.ExternalProviderId, out var tvrageId) && tvrageId.Equals(traktIds.TvRage);
                default:
                    return false;
            }
        }

        private async Task ReportProgress(string progress)
        {
            await Clients.Caller.SendAsync("UpdateProgress", $"[{DateTime.Now}]: {progress}");
        }
    }
}
