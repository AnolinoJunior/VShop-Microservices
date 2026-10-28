
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VShop.Web.Controllers;

[Authorize]
[ApiExplorerSettings(IgnoreApi = true)]
public class DevTokenController(
    IWebHostEnvironment environment) : Controller
{
    [HttpGet("/dev/access-token")]
    public async Task<IActionResult> GetAccessToken()
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var token = await HttpContext.GetTokenAsync(
            "access_token");

        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(
                "Nenhum access_token encontrado na sessão.");
        }

        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";

        return Content(token, "text/plain");
    }
}
