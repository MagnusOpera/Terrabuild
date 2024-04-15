namespace Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Razor.TagHelpers;

public static class LocalStore {
    public static string EnsureStoreExists(string uri) {
        var storePath = Path.GetFullPath(uri);
        if (!Directory.Exists(storePath)) {
            Directory.CreateDirectory(storePath);
        }

        return storePath;
    }


    public static bool Exists(string uri, string path) {
        var storePath = EnsureStoreExists(uri);
        var artifactPath = Path.GetFullPath(Path.Combine(storePath, path));
        if (!artifactPath.StartsWith(storePath)) {
            return false;
        }

        return File.Exists(artifactPath);
    }

    public static byte[] Read(string uri, string path) {
        var storePath = EnsureStoreExists(uri);
        return [];
    }

    public static bool Write(string uri, string path) {
        return false;
    }
}

[ApiController, Route("store")]
public class StoreController(ILogger<AuthController> logger, AppSettings appSettings) : ControllerBase {

    [HttpGet, Route("exists")]
    public ActionResult Exists([FromQuery] string path) {
        switch (appSettings.Store.Type) {
            case StoreType.Local:
                if (LocalStore.Exists(appSettings.Store.Uri, path)) {
                    return Ok();
                } else {
                    return NotFound();
                }

            default:
                return BadRequest();
        }
    }

    [HttpGet, Route("read")]
    public async Task<ActionResult<byte[]>> Read([FromQuery] string path) {
        switch (appSettings.Store.Type) {
            case StoreType.Local:
                return LocalStore.Read(appSettings.Store.Uri, path);

            default:
                return BadRequest();
        }
    }
}
