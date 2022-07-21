using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oqtane.Enums;
using Oqtane.Infrastructure;
using Oqtane.Models;
using Oqtane.Repository;
using Oqtane.Shared;
using System.Linq;
using System.Diagnostics;
using System.Globalization;
using System;

namespace Oqtane.Controllers
{
    [Route(ControllerRoutes.ApiRoute)]
    public class LanguageController : Controller
    {
        private readonly ILanguageRepository _languages;
        private readonly ISyncManager _syncManager;
        private readonly ILogManager _logger;
        private readonly Alias _alias;

        public LanguageController(ILanguageRepository language, ISyncManager syncManager, ILogManager logger, ITenantManager tenantManager)
        {
            _languages = language;
            _syncManager = syncManager;
            _logger = logger;
            _alias = tenantManager.GetAlias();
        }

        [HttpGet]
        public IEnumerable<Language> Get(string siteid, string packagename)
        {
            int SiteId;
            if (int.TryParse(siteid, out SiteId) && SiteId == _alias.SiteId)
            {
                if (string.IsNullOrEmpty(packagename))
                {
                    packagename = "Oqtane";
                }
                var languages = _languages.GetLanguages(SiteId).ToList();
                foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), $"{packagename}.*{Constants.SatelliteAssemblyExtension}", SearchOption.AllDirectories))
                {
                    var code = Path.GetFileName(Path.GetDirectoryName(file));
                    if (languages.Any(item => item.Code == code))
                    {
                        languages.Single(item => item.Code == code).Version = FileVersionInfo.GetVersionInfo(file).FileVersion;
                    }
                }
                var defaultCulture = CultureInfo.GetCultureInfo(Constants.DefaultCulture);
                languages.Add(new Language { Code = defaultCulture.Name, Name = defaultCulture.DisplayName, Version = Constants.Version, IsDefault = !languages.Any(l => l.IsDefault) });
                return languages.OrderBy(item => item.Name);
            }
            else
            {
                _logger.Log(LogLevel.Error, this, LogFunction.Security, "Unauthorized Language Get Attempt {SiteId}", siteid);
                HttpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return null;
            }
        }

        [HttpGet("{id}")]
        public Language Get(int id)
        {
            var language = _languages.GetLanguage(id);
            if (language != null && language.SiteId == _alias.SiteId)
            {
                return language;
            }
            else
            {
                _logger.Log(LogLevel.Error, this, LogFunction.Security, "Unauthorized Language Get Attempt {LanguageId}", id);
                HttpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return null;
            }
        }

        [HttpPost]
        [Authorize(Roles = RoleNames.Admin)]
        public Language Post([FromBody] Language language)
        {
            if (ModelState.IsValid && language.SiteId == _alias.SiteId)
            {
                language = _languages.AddLanguage(language);
                _syncManager.AddSyncEvent(_alias.TenantId, EntityNames.Site, _alias.SiteId);
                _logger.Log(LogLevel.Information, this, LogFunction.Create, "Language Added {Language}", language);
            }
            else
            {
                _logger.Log(LogLevel.Error, this, LogFunction.Security, "Unauthorized Language Post Attempt {Language}", language);
                HttpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                language = null;
            }
            return language;
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = RoleNames.Admin)]
        public void Delete(int id)
        {
            var language = _languages.GetLanguage(id);
            if (language != null && language.SiteId == _alias.SiteId)
            {
                _languages.DeleteLanguage(id);
                _syncManager.AddSyncEvent(_alias.TenantId, EntityNames.Site, _alias.SiteId);
                _logger.Log(LogLevel.Information, this, LogFunction.Delete, "Language Deleted {LanguageId}", id);
            }
            else
            {
                _logger.Log(LogLevel.Error, this, LogFunction.Security, "Unauthorized Language Delete Attempt {LanguageId}", id);
                HttpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            }
        }
    }
}
