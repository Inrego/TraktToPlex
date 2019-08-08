using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using TraktNet;
using TraktToPlex.Extensions.Microsoft.AspNetCore.Mvc;
using TraktToPlex.Models;
using TraktToPlex.Plex;

namespace TraktToPlex.Controllers
{
    public class HomeController : Controller
    {
        private readonly PlexClient _plexClient;
        private readonly IConfiguration _config;

        public HomeController(PlexClient plexClient, IConfiguration config)
        {
            _plexClient = plexClient;
            _config = config;
        }
        public async Task<IActionResult> Index()
        {
            var viewModel = new AuthViewModel
            {
                PlexKey = HttpContext.Session.GetString("PlexKey"),
                TraktKey = HttpContext.Session.GetString("TraktKey")
            };
            if (!string.IsNullOrEmpty(viewModel.PlexKey) && !string.IsNullOrEmpty(viewModel.TraktKey))
            {
                _plexClient.SetAuthToken(viewModel.PlexKey);
                viewModel.PlexServers = (await _plexClient.GetServers()).Select(x => new SelectListItem{Value = x.Url, Text = x.Name}).ToList();
                if (viewModel.PlexServers.Any())
                    viewModel.PlexServers[0].Selected = true;
            }

            var traktRedirectUrl = HttpUtility.UrlEncode(Url.AbsoluteAction("TraktReturn", "Home"));
            ViewData["TraktUrl"] = $"https://trakt.tv/oauth/authorize?client_id={_config["TraktConfig:ClientId"]}&redirect_uri={traktRedirectUrl}&response_type=code";
            return View(viewModel);
        }

        public IActionResult TraktLogout()
        {
            HttpContext.Session.Remove("TraktKey");
            return RedirectToAction(nameof(Index));
        }
        public IActionResult PlexLogout()
        {
            HttpContext.Session.Remove("PlexKey");
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> PlexLogin()
        {
            var redirectUrl = Url.AbsoluteAction("PlexReturn", "Home");
            var oauthUrl = await _plexClient.GetOAuthUrl(redirectUrl);
            HttpContext.Session.SetString("PlexOauthId", oauthUrl.Id);
            return Redirect(oauthUrl.Url);
        }

        public async Task<IActionResult> PlexReturn()
        {
            var oauthId = HttpContext.Session.GetString("PlexOauthId");
            if (string.IsNullOrEmpty(oauthId))
                throw new Exception("Missing oauth ID.");
            var authToken = await _plexClient.GetAuthToken(oauthId);
            if (string.IsNullOrEmpty(authToken))
                throw new Exception("Plex auth failed.");
            HttpContext.Session.Remove("PlexOauthId");
            HttpContext.Session.SetString("PlexKey", authToken);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> TraktReturn()
        {
            if (Request.Query.ContainsKey("code"))
            {
                var traktClient = new TraktClient(_config["TraktConfig:ClientId"], _config["TraktConfig:ClientSecret"]);
                traktClient.Authentication.RedirectUri = Url.AbsoluteAction("TraktReturn", "Home");
                var authResp = await traktClient.Authentication.GetAuthorizationAsync(Request.Query["code"].First());
                HttpContext.Session.SetString("TraktKey", authResp.Value.AccessToken);
                return RedirectToAction(nameof(Index));
            }
            throw new Exception("Trakt auth failed");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
