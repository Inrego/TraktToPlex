using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using TraktNet;
using TraktNet.Objects.Authentication;
using TraktNet.Objects.Basic;
using TraktNet.Objects.Get.Movies;
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
            try
            {
                _plexClient.SetAuthToken(plexKey);
                _plexClient.SetPlexServerUrl(plexUrl);

                _traktClient.Authorization = TraktAuthorization.CreateWith(traktKey);

                await MigrateTvShows();
                await ReportProgress( "--------------------------------------------");
                await ReportProgress( "Finished migrating TV Shows!");
                await ReportProgress( "--------------------------------------------");

                await MigrateMovies();
                await ReportProgress("--------------------------------------------");
                await ReportProgress("Finished migrating Movies!");
                await ReportProgress("--------------------------------------------");
            }
            catch (Exception e)
            {
                throw new HubException(e.Message);
            }
        }

        private async Task MigrateTvShows()
        {
            await ReportProgress( "Importing Trakt shows..");
            var traktShows = (await _traktClient.Sync.GetWatchedShowsAsync(new TraktExtendedInfo().SetFull())).ToArray();
            await ReportProgress( $"Found {traktShows.Length} shows on Trakt");

            await ReportProgress("Importing Plex shows..");
            var plexShows = await _plexClient.GetShows();
            await ReportProgress( $"Found {plexShows.Length} shows on Plex");
            await ReportProgress( "Going through all shows on Plex, to see if we find a match on Trakt..");
            var i = 0;
            foreach (var plexShow in plexShows)
            {
                i++;
                if (plexShow.ExternalProvider.Equals("themoviedb"))
                {
                    await ReportProgress($"Skipping {plexShow.Title} since it's configured to use TheMovieDb agent for metadata. This agent isn't supported, as Trakt doesn't have TheMovieDb ID's.");
                    continue;
                }
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

        private async Task MigrateMovies()
        {
            await ReportProgress("Importing Trakt movies..");
            var traktMovies = (await _traktClient.Sync.GetWatchedMoviesAsync(new TraktExtendedInfo().SetFull())).ToArray();
            await ReportProgress($"Found {traktMovies.Length} movies on Trakt");

            await ReportProgress("Importing Plex movies..");
            var plexMovies = await _plexClient.GetMovies();
            await ReportProgress($"Found {plexMovies.Length} movies on Plex");
            await ReportProgress("Going through all shows on Plex, to see if we find a match on Trakt..");
            var i = 0;
            foreach (var plexMovie in plexMovies)
            {
                i++;
                var traktMovie = traktMovies.FirstOrDefault(x => HasMatchingId(plexMovie, x.Ids));
                if (traktMovie == null)
                {
                    await ReportProgress($"({i}/{plexMovies.Length}) The movie \"{plexMovie.Title}\" was not found as watched on Trakt. Skipping!");
                    continue;
                }
                await ReportProgress($"({i}/{plexMovies.Length}) Found the movie \"{plexMovie.Title}\" as watched on Trakt. Processing!");
                await _plexClient.Scrobble(plexMovie);
                await ReportProgress($"Marking {plexMovie.Title} as watched..");
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
        private bool HasMatchingId(IMediaItem plexItem, ITraktMovieIds traktIds)
        {
            switch (plexItem.ExternalProvider)
            {
                case "imdb":
                    return plexItem.ExternalProviderId.Equals(traktIds.Imdb);
                case "tmdb":
                    return uint.TryParse(plexItem.ExternalProviderId, out var tmdbId) && tmdbId.Equals(traktIds.Tmdb);
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
