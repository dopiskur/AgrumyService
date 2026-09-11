using Agrumy.Web.Dal.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    /// Same-origin passthrough to Agrumy.Api's TileProxy - Leaflet's own tile requests are plain browser &lt;img&gt; fetches that can't carry a JWT bearer token, so they hit this cookie-authenticated endpoint instead and this server forwards with its own token via IApi's BearerTokenHandler.
    [Authorize]
    public class MapController(IApi api) : Controller
    {
        [Route("Map/Tile/{z:int}/{x:int}/{y:int}.png")]
        public async Task<ActionResult> Tile(int z, int x, int y)
        {
            HttpResponseMessage response = await api.MapTile(z, x, y);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode);
            }
            return File(await response.Content.ReadAsStreamAsync(), "image/png");
        }
    }
}
