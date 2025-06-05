using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureServer.Data;

namespace SecureServer.Controllers
{
    [ApiController]
    [Route("api/blacklist")]
    public class BlacklistController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public BlacklistController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBlacklistDetails(int id)
        {
            return Unauthorized();  // пока не надо

            var blacklistEntry = await _context.Blacklist.FindAsync(id);
            if (blacklistEntry == null) return NotFound();

            return Ok(new
            {
                blacklistEntry.Login,
                blacklistEntry.SteamId,
                blacklistEntry.DiscordId,
                blacklistEntry.Reason
            });
        }

        //[HttpPost("ban")]
        //[HttpDelete("ban")]
    }
}
