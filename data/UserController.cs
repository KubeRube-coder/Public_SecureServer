using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SecureServer.Data;
using SecureServer.Models;
using System;
using System.Linq.Expressions;
using static SecureServer.Controllers.UserController;
using static System.Net.Mime.MediaTypeNames;

namespace SecureServer.Controllers
{
    [ApiController]
    [Route("api/token")]
    public class tokenController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public tokenController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("valid/{token}")]
        public async Task<IActionResult> HasValidToken(string token)
        {
            var tokenFBD = await _context.ActiveTokens
                .FirstOrDefaultAsync(t => t.JwtToken == token || t.JwtRefresh == token);

            if (tokenFBD == null) return NotFound();

            bool valid = tokenFBD.ExpiryDate > DateTime.UtcNow;

            return Ok(new { tokenFBD.ExpiryDate, valid });
        }
    }

    [ApiController]
    [Route("api/mods")]
    public class modsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public modsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("public")]
        public async Task<IActionResult> GetModsDetails(
            int page = 1,
            int pageSize = 12,
            string searchTerm = null,
            string developer = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            [FromQuery] List<string> categories = null)
        {
            if (pageSize > 21)
            {
                return BadRequest("You can't request a page size more than 20");
            }

            var query = _context.Mods.AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(m =>
                    m.Name.ToLower().Contains(searchTerm.ToLower()) ||
                    m.Description.ToLower().Contains(searchTerm.ToLower())
                );
            }

            if (!string.IsNullOrEmpty(developer))
            {
                query = query.Where(m =>
                    m.modsby.ToLower().Contains(developer.ToLower())
                );
            }

            if (minPrice.HasValue)
                query = query.Where(m => m.price >= minPrice);

            if (maxPrice.HasValue)
                query = query.Where(m => m.price <= maxPrice);

            if (categories != null && categories.Count > 0)
            {
                var predicates = categories.Select(category =>
                    (Expression<Func<Mod, bool>>)(m => m.categories.Contains(category))
                ).ToList();

                var combinedPredicate = predicates.Aggregate((prev, next) =>
                {
                    var invokedNext = Expression.Invoke(next, prev.Parameters);
                    return Expression.Lambda<Func<Mod, bool>>(
                        Expression.OrElse(prev.Body, invokedNext),
                        prev.Parameters
                    );
                });

                query = query.Where(combinedPredicate);
            }

            int totalMods = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)totalMods / pageSize);

            var mods = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                Mods = mods,
                Pagination = new
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalMods = totalMods,
                    TotalPages = totalPages,
                    HasNextPage = page < totalPages,
                    HasPreviousPage = page > 1
                }
            });
        }

        [HttpGet("public/{id}")]
        public async Task<IActionResult> GetSingleMod(int id)
        {
            var mod = await _context.Mods.SingleOrDefaultAsync(u => u.Id == id);
            if (mod == null) return NotFound();

            return Ok(mod);
        }

        [HttpGet("public/GetsMods/{ids}")]
        public async Task<IActionResult> GetModsByIds(string ids)
        {
            var idlist = ids.Split(',').Select(int.Parse).ToList();

            var mods = await _context.Mods
                .Where(s => idlist.Contains(s.Id))
                .ToListAsync();
            if (!mods.Any()) return NotFound();

            return Ok(mods);
        }

        [HttpGet("public/GetsMods/GetPrems")]
        public async Task<IActionResult> GetPremMods()
        {
            var premMods = await _context.premmods.ToListAsync();

            var premModsByList = premMods
                .SelectMany(pm => pm.mods?
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => int.TryParse(m, out var id) ? id : (int?)null)
                    .Where(id => id.HasValue)
                    .Select(id => new { ModId = id.Value, ModsBy = pm.modsby, Price = pm.premPrice })
                    ?? Enumerable.Empty<dynamic>())
                .ToList();

            var modIds = premModsByList.Select(x => x.ModId).ToList();

            var mods = await _context.Mods
                .Where(m => modIds.Contains(m.Id))
                .ToListAsync();

            var groupedMods = premModsByList
                .GroupBy(p => new { p.ModsBy, p.Price })
                .Select(group => new
                {
                    modName = group.Key.ModsBy,
                    price = group.Key.Price,
                    Mods = mods.Where(m => group.Select(g => g.ModId).Contains(m.Id))
                               .Select(m => new
                               {
                                   m.Id,
                                   modsBy = m.modsby,
                                   Name = m.Name,
                                   NameDWS = m.NameDWS,
                                   Description = m.Description,
                                   Price = m.price,
                                   image_url = m.image_url
                               }).ToList()
                })
                .ToList();

            return Ok(groupedMods);
        }

        [HttpGet("public/GetsMods/prem/{id}")]
        public async Task<IActionResult> GetPremModById(int id)
        {
            var premMods = await _context.premmods.ToListAsync();

            bool premModExists = premMods.Any(p =>
                p.mods.Split(',').Select(int.Parse).Contains(id)
            );

            if (!premModExists) return NotFound();

            var mod = await _context.Mods.SingleOrDefaultAsync(m => m.Id == id);
            if (mod == null) return NotFound();

            return Ok(mod);
        }

        [HttpGet("public/GetsMods/premIDs/{ids}")]
        public async Task<IActionResult> GetPremModsByIds(string ids)
        {
            var idList = ids.Split(',').Select(int.Parse).ToList();

            var premMods = await _context.premmods.ToListAsync();

            var premModsIds = premMods
                .SelectMany(p => p.mods.Split(',').Select(int.Parse))
                .Distinct()
                .ToList();

            var validIds = idList.Where(id => premModsIds.Contains(id)).ToList();

            if (!validIds.Any()) return NotFound();

            var mods = await _context.Mods
                .Where(m => validIds.Contains(m.Id))
                .ToListAsync();

            return Ok(mods);
        }


        [HttpGet("public/getDevs")]
        public async Task<IActionResult> GetDevs()
        {
            var dev = await _context.moddevelopers.ToListAsync();
            return Ok(dev);
        }

        [HttpGet("public/getDev/{id}")]
        public async Task<IActionResult> GetDevById(int id)
        {
            var dev = await _context.moddevelopers.SingleOrDefaultAsync(u => u.Id == id);
            if (dev == null) return NotFound();

            return Ok(dev);
        }

        [HttpGet("public/getDevs/{ids}")]
        public async Task<IActionResult> GetDevsByIds(string ids)
        {
            var idlist = ids.Split(',').Select(int.Parse).ToList();

            var devs = await _context.moddevelopers
                .Where(s => idlist.Contains(s.Id))
                .ToListAsync();

            if (!devs.Any()) return NotFound();

            return Ok(devs);
        }


        [HttpGet("private/getMods/forServers/{servers}")]
        public async Task<IActionResult> GetModsFromServers(string servers)
        {
            if (string.IsNullOrEmpty(servers))
                return BadRequest("Servers is null!");

            var servNames = servers.Split(',').Select(s => s.Trim()).ToList();
            var result = new Dictionary<string, object>();

            foreach (var server in servNames)
            {
                var serverName = await _context.Servers
                    .SingleOrDefaultAsync(s => s.name == server);

                if (serverName == null) continue;

                var modsIds = string.IsNullOrEmpty(serverName.mods)
                    ? new List<int>()
                    : serverName.mods.Split(',').Select(int.Parse).ToList();

                var mods = await _context.Mods
                    .Where(m => modsIds.Contains(m.Id))
                    .ToListAsync();

                var modsDate = mods.Select(mod => new ModUser
                {
                    Id = mod.Id,
                    modsby = mod.modsby,
                    Name = mod.Name,
                    NameDWS = mod.NameDWS,
                    Description = mod.Description,
                    image_url = mod.image_url,
                    price = mod.price,
                    expires_date = _context.purchasesInfo
                        .Where(p => p.whoBuyed == serverName.owner_id && p.modId == mod.Id)
                        .Select(p => p.expires_date)
                        .FirstOrDefault()
                }).ToList();

                result[server] = new
                {
                    mods = modsDate
                };
            }

            return Ok(result);
        }

        [HttpGet("private/GetMods")]
        public async Task<IActionResult> GetModsOnThisIP()
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null)
            {
                return BadRequest();
            }

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) { return BadRequest(); }

            var ipAddress = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()
                                ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                                ?? "No IP";

            var serveDB = await _context.Servers.Where(s => s.ip == ipAddress).ToListAsync();

            var claimedMods = new List<int>();

            foreach (var server in serveDB)
            {
                var temp = server.mods.Split(',')
                .Select(id => int.Parse(id))
                .ToList();

                claimedMods.AddRange(temp);
            }

            var mods = await _context.Mods
                .Where(m => claimedMods.Contains(m.Id))
                .ToListAsync();

            var devsMods = await _context.moddevelopers.FirstOrDefaultAsync(md => md.nameOfMod == user.Login);
            var ListDevsMods = devsMods?.mods.Split(',')
                .Select(id => int.Parse(id))
                .ToList() ?? new List<int>();

            if (user.role == "modcreator" || user.role == "admin")
            {
                var userMods = await _context.Mods
                    .Where(m => ListDevsMods.Contains(m.Id))
                    .ToListAsync();

                mods.AddRange(userMods);
            }


            var modsDate = mods
                .DistinctBy(m => m.Id)
                .Select(mod => new ModUser
                {
                    Id = mod.Id,
                    modsby = mod.modsby,
                    Name = mod.Name,
                    NameDWS = mod.NameDWS,
                    Description = mod.Description,
                    image_url = mod.image_url,
                    price = mod.price,
                    expires_date = _context.purchasesInfo
                        .Where(p => p.whoBuyed == user.Id && p.modId == mod.Id)
                        .Select(p => p.expires_date)
                        .FirstOrDefault()
                })
                .ToList();

            var file = await _context.filesdifine
                    .FirstOrDefaultAsync(f => f.path.EndsWith("update.zip"));

            return Ok(new { LastVersion = file?.version ?? "", modsDate  });
        }

        [HttpPost("change-mods-allocations")]
        public async Task<IActionResult> ChangeModsAllocations(string ids, string serverIp)
        {
            if (string.IsNullOrWhiteSpace(ids)) return BadRequest("Data can't be null");
            if (string.IsNullOrWhiteSpace(serverIp)) return BadRequest("Server IP can't be null");

            var username = Request.Headers["UserName"].SingleOrDefault();
            if (string.IsNullOrWhiteSpace(username)) return BadRequest("Username can't be null");

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return BadRequest("Unknown username");

            var server = await _context.Servers.SingleOrDefaultAsync(s => s.ip == serverIp);
            if (server == null) return BadRequest("Server not found");

            var modsIds = ids.Split("](");
            if (modsIds.Length < 2) return BadRequest("Invalid data format");

            var modsToStorage = modsIds[0].Split(',')
                .Where(id => int.TryParse(id.Trim(), out _))
                .Select(int.Parse)
                .ToList();

            var modsFromStorage = modsIds[1].Split(',')
                .Where(id => int.TryParse(id.Trim(), out _))
                .Select(int.Parse)
                .ToList();

            if (modsToStorage.Count() <= 0 && modsFromStorage.Count() <= 0) return BadRequest("Mods ids is null");

            var serverIdsMods = server.mods.Split(',').Select(id => int.Parse(id.Trim())).ToList();

            var allValidMods = await _context.Mods.Select(m => m.Id).ToListAsync();

            modsToStorage = modsToStorage.Where(id => allValidMods.Contains(id)).ToList();
            modsFromStorage = modsFromStorage.Where(id => allValidMods.Contains(id)).ToList();

            var claimedModsDict = new Dictionary<int, int>();

            if (!string.IsNullOrWhiteSpace(user.ClaimedMods))
            {
                foreach (var entry in user.ClaimedMods.Split(','))
                {
                    var parts = entry.Trim().Split('[');
                    if (parts.Length == 2 &&
                        int.TryParse(parts[0], out int modId) &&
                        int.TryParse(parts[1].TrimEnd(']'), out int count))
                    {
                        claimedModsDict[modId] = count;
                    }
                }
            }

            foreach (var modId in modsFromStorage)
            {
                if (!serverIdsMods.Contains(modId))
                {
                    if (claimedModsDict.ContainsKey(modId))
                    {
                        if (claimedModsDict[modId] > 1)
                            claimedModsDict[modId] -= 1;
                        else
                            claimedModsDict.Remove(modId);
                    }
                }
            }

            foreach (var modId in modsToStorage)
            {
                if (serverIdsMods.Contains(modId))
                {   
                    claimedModsDict[modId] = claimedModsDict.ContainsKey(modId) ? claimedModsDict[modId] + 1 : 1;
                }
            }

            user.ClaimedMods = string.Join(",", claimedModsDict.Select(kvp => $"{kvp.Key}[{kvp.Value}]"));

            var serverMods = string.IsNullOrWhiteSpace(server.mods) ? new List<int>() :
                server.mods.Split(',').Select(id => int.Parse(id.Trim())).ToList();

            serverMods.RemoveAll(modsToStorage.Contains);
            serverMods.AddRange(modsFromStorage.Except(serverMods));

            server.mods = string.Join(",", serverMods);

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Mods successfully reallocated" });

            // Т.к у нас при заканчивании подписки на мод, он пытается удалить из user claimedmods вхождение id[count].
            // Subscription дополняет claimedmods вхождениями id[count]
            // Так же надо убрать отображение модов, которые даются от Subscription
            // При ежедневном обновлении если подписка закончилась, то удаляется вхождение и подписка не обновляется, в subscriptions оно просто становятся subactive = 0, но так же снимет деньги если будет обновление
        }

        [HttpGet("private/avaliableMods")]
        public async Task<IActionResult> GetAvailableMods()
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest("Username is null");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == username);
            if (user == null) return BadRequest("Uncorrect username!");

            if (!string.IsNullOrWhiteSpace(user.ClaimedMods))
            {
                var claimedMods = user.ClaimedMods.Split(',').ToList();
                return Ok(claimedMods);
            }

            return Ok();
        }
    }

    [ApiController]
    [Route("api/server")]
    public class serverController: ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public class ServerDto
        {
            public string Name { get; set; }
            public string Ip { get; set; }
            public string? Port { get; set; }
            public string Mods { get; set; }
        }

        public class ServerEDto
        {
            public string Name { get; set; }
            public string Ip { get; set; }
            public string NewName { get; set; }
            public string NewIp { get; set; }
        }

        public class MassiveType
        {
            public string usernameNew { get; set; }
            public string serverportNew { get; set; }
            public string[] modNamesNew { get; set; }
        }
        public serverController(ApplicationDbContext context)
        {
            _context = context;
        }

        //[HttpGet("private")]
        //public async Task<IActionResult> GetServerOfUser()
        //{
        //    var username = Request.Headers["UserName"].FirstOrDefault();
        //    if (username == null) return BadRequest();

        //    var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
        //    if (user == null) return NotFound();
        //    var serversUser = await _context.Servers
        //        .Where(s => s.owner_id == user.Id)
        //        .ToListAsync();

        //    if (serversUser == null) return NotFound();

        //    return Ok(serversUser.Select(s => new
        //    {
        //        s.mods,
        //        s.name,
        //        s.ip
        //    }));
        //}

        [HttpGet("private")]
        public async Task<IActionResult> GetServerFully()
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == username);
            if (user == null) return BadRequest();

            var servers = await _context.Servers.Where(sr => sr.owner_id == user.Id).ToListAsync();
            if (!servers.Any()) return BadRequest();

            var result = new Dictionary<string, object>();

            foreach (var server in servers)
            {
                if (server == null) continue;

                var modsIds = string.IsNullOrEmpty(server.mods)
                    ? new List<int>()
                    : server.mods.Split(',').Select(int.Parse).ToList();

                var mods = await _context.Mods
                    .Where(m => modsIds.Contains(m.Id))
                    .ToListAsync();

                var modsDate = mods.Select(mod => new ModUser
                {
                    Id = mod.Id,
                    modsby = mod.modsby,
                    Name = mod.Name,
                    NameDWS = mod.NameDWS,
                    Description = mod.Description,
                    image_url = mod.image_url,
                    price = mod.price,
                    expires_date = _context.purchasesInfo
                        .Where(p => p.whoBuyed == server.owner_id && p.modId == mod.Id)
                        .Select(p => p.expires_date)
                        .FirstOrDefault()
                }).ToList();

                result[server.name] = new
                {
                    name = server.name,
                    ip = server.ip,
                    port = server.port,
                    mods = modsDate
                };
            }

            return Ok(result);
        }

        [HttpPost("GetServerName")]
        public async Task<IActionResult> ValidMods([FromBody] MassiveType TypeMods)
        {
            if (TypeMods == null) return BadRequest("Data is null");

            foreach(string mod in TypeMods.modNamesNew)
            {
                var modEx = await _context.Mods.FirstOrDefaultAsync(m => m.NameDWS == mod);
                if (modEx != null)
                {
                    var Webhook = await _context.webhooks.FirstOrDefaultAsync(w => w.NameMod == modEx.NameDWS);
                    if (Webhook != null)
                    {
                        string newMessage = $"❌ Сервер был запущен БЕЗ подтвержденного мода {Webhook.NameMod}!\n"
                            + $"👤 Логин:  {TypeMods.usernameNew}\n"
                            + $"🌍 IP Запуска:  {TypeMods.serverportNew}";

                        var responseDiscord = DiscordSender.SendToDiscord(newMessage, "DWS Guard", "📢ЗАПУСК МОДА НЕ ПОДТВЕРЖДЕН!", 0x00CC0033, "https://i.postimg.cc/4Npvzxp4/Logo-DWSPng.png", Webhook.Discord_web);

                        if (responseDiscord.Result)
                        {
                            Console.WriteLine("Sucessfull sending to Discord message about " + Webhook.NameMod);
                        }
                    }
                    
                }
            }

            return Ok();
        }

        [HttpPost("private/addServer")]
        public async Task<IActionResult> AddnewServer([FromBody] ServerDto serverDto)
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest();

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return NotFound();

            if (serverDto.Port == null)
            {
                serverDto.Port = serverDto.Ip.Split(":", StringSplitOptions.RemoveEmptyEntries).ToList()[1];
            }

            var existingServer = await _context.Servers
                .Where(s => s.owner_id == user.Id && (s.name == serverDto.Name || (s.ip == serverDto.Ip && s.port == serverDto.Port)))
                .FirstOrDefaultAsync();

            if (existingServer != null) return BadRequest("Сервер с таким названием уже существует!");

            var serverCount = await _context.Servers
                .Where(s => s.owner_id == user.Id)
                .CountAsync();

            if (serverCount >= 6) return BadRequest("Maximum of servers!");

            var serverIPPort = serverDto.Ip.Split(":", StringSplitOptions.RemoveEmptyEntries).ToList();

            var newServer = new Servers
            {
                name = serverDto.Name,
                ip = serverIPPort[0],
                port = serverIPPort[1],
                owner_id = user.Id,
                mods = "0"
            };

            _context.Servers.Add(newServer);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Сервер успешно добавлен", serverId = newServer.id, serverName = newServer.name });
        }

        [HttpPost("private/removeServer")]
        public async Task<IActionResult> RemoveServer([FromBody] ServerDto serverDto)
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest();

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return NotFound();

            var server = await _context.Servers.SingleOrDefaultAsync(s => (s.name == serverDto.Name && s.ip == serverDto.Ip && s.owner_id == user.Id));
            if (server == null) return NotFound();

            _context.Servers.Remove(server);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Сервер успешно удален" });
        }

        [HttpPost("private/editServer")]
        public async Task<IActionResult> EditServer([FromBody] ServerEDto serverDto)
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest();

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return NotFound();

            var server = await _context.Servers.SingleOrDefaultAsync(s => (s.name == serverDto.Name && s.ip == serverDto.Ip && s.owner_id == user.Id));
            if (server == null) return NotFound();

            var serverIPPort = serverDto.NewIp.Split(":", StringSplitOptions.RemoveEmptyEntries).ToList();

            server.ip = serverIPPort[0];
            server.port = serverIPPort[1];
            server.name = serverDto.NewName;

            await _context.SaveChangesAsync();

            return Ok(server);
        }
    }

    [ApiController]
    [Route("api/ip")]
    public class IpController : ControllerBase
    {
        [HttpGet("get")]
        public async Task<IActionResult> GetMyIP()
        {
            string ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()
                    ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "No IP";

            return Ok(ip);

        }
    }

    [ApiController]
    [Route("api/user")]
    public class UserController : ControllerBase
    {
        public class BoughtData
        {
            public string ServerIp { get; set; }
            public string ServerPort { get; set; }
            public int[] Mods { get; set; }
        }

        public class PremData
        {
            public string NameOfDeveloperName { get; set; }
        }

        public class ModData
        {
            public string ModName { get; set; }
            public string? ModKey { get; set; }
        }

        public class wrapper
        {
            public ModData[] List { get; set; }
        }

        public class vNRZhLnbyXbh
        {
            public string[] keys { set; get; }
        }

        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserController> _logger;
        private readonly BalanceHandler _balanceHandler;
        private readonly vNRZhLnbyXbh _vNRZhLnbyXbh;

        public UserController(ApplicationDbContext context, ILogger<UserController> logger, BalanceHandler balanceHandler, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _balanceHandler = balanceHandler;
            _vNRZhLnbyXbh = configuration.GetSection("vNRZhLnbyXbh").Get<vNRZhLnbyXbh>(); // Секретики
        }

        private class FullModResponse
        {
            public string V { get; set; }
            public string Desc { get; set; }
            public List<ModDataResponse> List { get; set; }
        }

        private class ModDataResponse
        {
            public string ModName { get; set; }
            public string Member { get; set; }
            public string PartnerDS { get; set; }
            public string? Alerts_WH { get; set; }
            public string? DS_WH_OK { get; set; }
            public string? DS_WH_ER { get; set; }
            public string SteamID { get; set; }
            public string Discord { get; set; }
        }

        [HttpGet("KGB/GetMods")] // Только от опред ip
        public async Task<IActionResult> GetKGBMods(string? ip, string? port, string? secretKey)
        {
            if (!_vNRZhLnbyXbh.keys.Contains(secretKey))
                return Ok(Array.Empty<ModDataResponse>());

            var server = await _context.Servers.FirstOrDefaultAsync(sr => sr.ip == ip && sr.port == port);
            if (server == null)
                return Ok(Array.Empty<ModDataResponse>());

            var modIds = server.mods
                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.Parse(id.Trim()))
                .ToList();

            var mods = await _context.Mods
                .Where(m => modIds.Contains(m.Id))
                .ToListAsync();

            var modbys = mods.Select(m => m.modsby).Distinct().ToList();
            var modNames = mods.Select(m => m.NameDWS).Distinct().ToList();

            var developers = await _context.moddevelopers
                .Where(md => modbys.Contains(md.modsby))
                .ToListAsync();

            var devNames = developers.Select(d => d.nameOfMod).ToList();

            var users = await _context.Users
                .Where(u => devNames.Contains(u.Login) || u.Id == server.owner_id)
                .ToListAsync();

            var webhooks = await _context.webhooks
                .Where(w => modNames.Contains(w.NameMod))
                .ToListAsync();

            var ownerUser = users.FirstOrDefault(u => u.Id == server.owner_id);

            var VerifiedMods = mods.Select(mod =>
            {
                var modDev = developers.FirstOrDefault(md => md.modsby == mod.modsby);
                var devUser = users.FirstOrDefault(u => u.Login == modDev?.nameOfMod);
                var modWeb = webhooks.FirstOrDefault(wb => wb.NameMod == mod.NameDWS);

                return new ModDataResponse
                {
                    ModName = mod.Name,
                    Member = devUser?.SteamId ?? "",
                    PartnerDS = devUser?.DiscordId ?? "",
                    Alerts_WH = modWeb?.Discord_web ?? "",
                    DS_WH_OK = modWeb?.Discord_web_OK ?? "",
                    DS_WH_ER = modWeb?.Discord_web_ER ?? "",
                    SteamID = ownerUser?.SteamId ?? "",
                    Discord = ownerUser?.DiscordId ?? ""
                };
            }).ToList();

            return Ok(VerifiedMods);
        }


        [HttpGet]
        public async Task<IActionResult> GetUserDetails()
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest();

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return NotFound();

            return Ok(new
            {
                user.SteamId,
                user.DiscordId,
                user.ClaimedMods
            });
        }

        [HttpGet("balance")]
        public async Task<IActionResult> GetWallet()
        {
            if (Request.Headers.TryGetValue("Username", out var username))
            {
                var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username.First());
                if (user == null) return NotFound();
                return Ok(user.balance);
            }

            return BadRequest();
        }

        [HttpGet("getMods/{username}")]
        public async Task<IActionResult> GetModsOfUser(string username)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return NotFound();

            var modsIds = await _context.Servers.FirstOrDefaultAsync(s => s.owner_id == user.Id);

            var devsMods = await _context.moddevelopers.FirstOrDefaultAsync(md => md.nameOfMod == user.Login);
            var ListDevsMods = devsMods?.mods.Split(',')
                .Select(id => int.Parse(id))
                .ToList() ?? new List<int>();

            var allModsNames = new List<string>();

            var PremMods = await _context.subscription.SingleOrDefaultAsync(u => u.login == user.Login);
            if (PremMods != null && PremMods.subActive == true)
            {
                var modsPremIds = PremMods.subscriptionMods.Split(',').Select(int.Parse).ToList();
                var modsPrem = await _context.Mods.Where(m => modsPremIds.Contains(m.Id)).Select(m => m.NameDWS).ToListAsync();

                if (modsPrem != null) allModsNames.AddRange(modsPrem);
            }

            if ((user.role == "modcreator" || user.role == "admin") && ListDevsMods.Any())
            {
                var userModsNames = await _context.Mods
                    .Where(m => ListDevsMods.Contains(m.Id))
                    .Select(m => m.NameDWS)
                    .ToListAsync();

                allModsNames.AddRange(userModsNames);
            }

            if (modsIds != null)
            {
                var modsIdsFromServers = modsIds.mods.Split(',')
                    .Select(int.Parse)
                    .ToList();

                var serverModsNames = await _context.Mods
                    .Where(m => modsIdsFromServers.Contains(m.Id))
                    .Select(m => m.NameDWS)
                    .ToListAsync();

                allModsNames.AddRange(serverModsNames);
            }

            if (allModsNames.Count() == 0)
            {
                return Ok("NOT FOUND");
            } else
            {
                return Ok(string.Join(",", allModsNames.Distinct()));
            }
        }

        [HttpGet("GetPurchaseHistory")]
        public async Task<IActionResult> GetPurcasheHistory()
        {
            if (Request.Headers.TryGetValue("Username", out var username))
            {
                var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username.First());
                if (user == null) return NotFound();

                var mods = await _context.Mods.ToListAsync();
                var servers = await _context.Servers.ToListAsync();

                var purchasesUser = await _context.purchasesInfo
                    .Where(u => u.whoBuyed == user.Id)
                    .ToListAsync();

                if (purchasesUser != null)
                {
                    var result = purchasesUser.Select(s =>
                    {
                        var mod = mods.SingleOrDefault(m => m.Id == s.modId);
                        if (mod == null)
                        {
                            return null;
                        }

                        var server = servers.SingleOrDefault(ser => ser.id == s.serverId);
                        if (server == null)
                        {
                            return null;
                        }

                        return new
                        {
                            id = s.Id,
                            modsby = mod.modsby,
                            name = mod.Name,
                            server = server.name,
                            bought_date = s.date,
                            BuyWhenExpires = s.BuyWhenExpires,
                            expires_date = s.expires_date
                        };
                    }).Where(x => x != null);

                    return Ok(result);
                }
            }

            return BadRequest("Uncorrect headers!");
        }

        [HttpGet("GetMySubscriptions")]
        public async Task<IActionResult> getMySubscriptions()
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest("Username is null");

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return BadRequest("User not found");

            var subscription = await _context.subscription.Where(s => s.login == user.Login).ToListAsync();
            if (subscription == null) return BadRequest("You don't have subscriptions");

            var NewSub = subscription.Select(s =>
            {
                var userMod = _context.premmods.FirstOrDefaultAsync(md => md.mods == s.subscriptionMods);

                return new
                {
                    id = s.Id,
                    subscriptionMods = userMod.Result?.modsby,
                    BuyWhenExpires = s.BuyWhenExpires,
                    BoughtData = s.boughtDate,
                    expireData = s.expireData
                };
            });

            return Ok(NewSub);
        }

        [HttpPost("SetBoughtSub/{id}")]
        public async Task<IActionResult> SetSubscription(int id)
        {
            if (id == 0) return BadRequest("Uncorrect id");

            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest("Username was not provided");

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return BadRequest("Uncorrect username");

            var subscription = await _context.subscription.SingleOrDefaultAsync(sub => sub.login == user.Login && sub.Id == id);
            if (subscription == null) return NotFound("Uncorrect id. Subscription not found");

            subscription.BuyWhenExpires = !subscription.BuyWhenExpires;
            await _context.SaveChangesAsync();

            return Ok(new {message = ("now subscription is " + subscription.BuyWhenExpires) });
        }

        [HttpPost("SetBoughtMod/{id}")]
        public async Task<IActionResult> SetModAutoBuy(int id)
        {
            if (id == 0) return BadRequest("Uncorrect id");

            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest("Username was not provided");

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return BadRequest("Uncorrect username");

            var ModSub = await _context.purchasesInfo.SingleOrDefaultAsync(ms => ms.whoBuyed == user.Id && ms.Id == id);
            if (ModSub == null) return NotFound("Mod subscription not found. Recheck id");

            ModSub.BuyWhenExpires = !ModSub.BuyWhenExpires;
            await _context.SaveChangesAsync();

            return Ok(new { message = ("now mod subscription is" + ModSub.BuyWhenExpires) });
        }

        [HttpPost("buy/mods")]
        public async Task<IActionResult> HandleBoughtMods([FromBody] BoughtData Data)
        {
            if (Data == null) return BadRequest("Data is null");

            var server = await _context.Servers.SingleOrDefaultAsync(s => s.ip == Data.ServerIp);
            if (server == null) return NotFound("Server was not found");

            var username = Request.Headers["UserName"].FirstOrDefault();

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return BadRequest("User not found");

            if (server.owner_id != user.Id) return BadRequest();

            var mods = await _context.Mods.Where(m => Data.Mods.Contains(m.Id)).ToListAsync();
            if (mods.Count() == 0) return NotFound("Mods not found");

            var modCreators = await _context.moddevelopers.ToListAsync();

            float price = 0;

            foreach (var mod in mods)
            {
                price += mod.price;
            }

            _logger.LogInformation(price.ToString());

            if (price >= 0 && price <= user.balance)
            {
                var ServerModsIds = server.mods.Split(',').Select(id => int.Parse(id.Trim())).ToList();
                
                foreach (var mod in mods)
                {
                    if (!ServerModsIds.Contains(mod.Id))
                    {
                        ServerModsIds.Add(mod.Id);
                        var purchases = new purchasesInfo
                        {
                            whoBuyed = user.Id,
                            modId = mod.Id,
                            serverId = server.id,
                            date = DateTime.Now,
                            BuyWhenExpires = true,
                            expires_date = DateTime.Now.AddDays(30),
                        };

                        var modCr = modCreators.FirstOrDefault(md => md.modsby == mod.modsby);

                        if (modCr != null && mod.price > 0)
                        {
                            await _balanceHandler.ProcessTransactionAsync(user.Id, modCr.nameOfMod, mod.price, mod.Id.ToString());
                        }

                        _context.purchasesInfo.Add(purchases);
                    } else if (ServerModsIds.Contains(mod.Id))
                    {
                        price -= mod.price;
                    }
                }

                user.balance -= price;

                ServerModsIds.Sort();

                server.mods = string.Join(",", ServerModsIds);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Successfull bought"
                });
            }

            return BadRequest("Not enought money");
        }

        [HttpPost("buy/premium")]
        public async Task<IActionResult> BuyPremium([FromBody] PremData Data)
        {
            if (Data == null) return BadRequest("Data is null");

            var username = Request.Headers["UserName"].FirstOrDefault();
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return BadRequest("User not found");

            var moddeveloper = await _context.moddevelopers.FirstOrDefaultAsync(md => md.nameOfMod == Data.NameOfDeveloperName);
            if (moddeveloper == null) return BadRequest("Developer was not found");

            var SubscriptionsMods = await _context.premmods.FirstOrDefaultAsync(sm => sm.modsby == moddeveloper.modsby);
            if (SubscriptionsMods == null) return NotFound("You don't have any subscriptions");

            float price = SubscriptionsMods.premPrice;

            if (price > 0 && price <= user.balance)
            {
                var subscription = new subscription
                {
                    login = user.Login,
                    steamid = user.SteamId,
                    subscriptionMods = SubscriptionsMods.mods,
                    subActive = true,
                    BuyWhenExpires = true,
                    boughtDate = DateTime.UtcNow,
                    expireData = DateTime.UtcNow.AddDays(30),
                };

                await _balanceHandler.ProcessTransactionAsync(user.Id, moddeveloper.nameOfMod, price, SubscriptionsMods.mods);

                user.balance -= price;

                var claimedModsDict = new Dictionary<int, int>();

                if (!string.IsNullOrWhiteSpace(user.ClaimedMods))
                {
                    foreach (var entry in user.ClaimedMods.Split(','))
                    {
                        var parts = entry.Trim().Split('[');
                        if (parts.Length == 2 &&
                            int.TryParse(parts[0], out int modId) &&
                            int.TryParse(parts[1].TrimEnd(']'), out int count))
                        {
                            claimedModsDict[modId] = count;
                        }
                    }
                }

                var allMods = await _context.Mods.Select(m => m.Id).ToListAsync();

                var modIds = SubscriptionsMods.mods
                    .Split(',')
                    .Select(id => id.Trim())
                    .Where(id => int.TryParse(id, out int modId) && allMods.Contains(modId))
                    .Select(int.Parse)
                    .ToList();

                foreach (var modId in modIds)
                {
                    claimedModsDict[modId] = claimedModsDict.ContainsKey(modId) ? claimedModsDict[modId] + 1 : 1;
                }

                user.ClaimedMods = string.Join(",", claimedModsDict.Select(kvp => $"{kvp.Key}[{kvp.Value}]"));

                _context.subscription.Add(subscription);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Successfully bought"
                });
            }

            return BadRequest("Not enough money");
        }


        [HttpGet("getDetainedMods")]
        public async Task<IActionResult> GetDetainedMods()
        {
            var username = Request.Headers["UserName"].SingleOrDefault();
            if (username == null) return BadRequest("Username can't be null");

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return BadRequest("Unknown username");

            return Ok();
        }

        [HttpGet("GetData")]
        public async Task<IActionResult> GetData()
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == username);
            if (user == null) return BadRequest();

            var servers = await _context.Servers.Where(sr => sr.owner_id == user.Id).ToListAsync();

            var result = new Dictionary<string, object>();
            if (servers.Any())
            {
                foreach (var server in servers)
                {
                    if (server == null) continue;

                    var modsIds = string.IsNullOrEmpty(server.mods)
                        ? new List<int>()
                        : server.mods.Split(',').Select(int.Parse).ToList();

                    var mods = await _context.Mods
                        .Where(m => modsIds.Contains(m.Id))
                        .ToListAsync();

                    var modsDate = mods.Select(mod => new ModUser
                    {
                        Id = mod.Id,
                        modsby = mod.modsby,
                        Name = mod.Name,
                        NameDWS = mod.NameDWS,
                        Description = mod.Description,
                        image_url = mod.image_url,
                        price = mod.price,
                        expires_date = _context.purchasesInfo
                            .Where(p => p.whoBuyed == server.owner_id && p.modId == mod.Id)
                            .Select(p => p.expires_date)
                            .FirstOrDefault()
                    }).ToList();

                    result[server.name] = new
                    {
                        name = server.name,
                        ip = server.ip,
                        port = server.port,
                        mods = modsDate
                    };
                }
            }

            var dev = await _context.moddevelopers.ToListAsync();

            return Ok(new
            {
                servers = result.Any() ? result : new Dictionary<string, object> { },
                modders = dev,
            });
        }

        [HttpGet("GetPurchasesData")]
        public async Task<IActionResult> GetPurchasesData()
        {
            if (Request.Headers.TryGetValue("Username", out var username))
            {
                var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username.First());
                if (user == null) return NotFound();

                var mods = await _context.Mods.ToListAsync();
                var servers = await _context.Servers.ToListAsync();

                var purchasesUser = await _context.purchasesInfo
                    .Where(u => u.whoBuyed == user.Id)
                    .ToListAsync();

                if (purchasesUser != null)
                {
                    var result = purchasesUser.Select(s =>
                    {
                        var mod = mods.SingleOrDefault(m => m.Id == s.modId);
                        if (mod == null)
                        {
                            return null;
                        }

                        var server = servers.SingleOrDefault(ser => ser.id == s.serverId);
                        if (server == null)
                        {
                            return null;
                        }

                        return new
                        {
                            id = s.Id,
                            modsby = mod.modsby,
                            name = mod.Name,
                            server = server.name,
                            bought_date = s.date,
                            BuyWhenExpires = s.BuyWhenExpires,
                            expires_date = s.expires_date
                        };
                    }).Where(x => x != null);

                    var subscription = await _context.subscription.Where(s => s.login == user.Login).ToListAsync();
                    if (subscription == null) return BadRequest("You don't have subscriptions");

                    var NewSub = subscription.Select(s =>
                    {
                        var userMod = _context.premmods.FirstOrDefaultAsync(md => md.mods == s.subscriptionMods);

                        return new
                        {
                            id = s.Id,
                            subscriptionMods = userMod.Result?.modsby,
                            BuyWhenExpires = s.BuyWhenExpires,
                            BoughtData = s.boughtDate,
                            expireData = s.expireData
                        };
                    });

                    return Ok(new
                    {
                        bought = result,
                        subs = NewSub
                    });
                }
            }

            return BadRequest("Uncorrect headers!");
        }
    }


    [ApiController]
    [Route("api/news")]
    public class NewsClass : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserController> _logger;

        public NewsClass(ApplicationDbContext context, ILogger<UserController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetNews()
        {
            var formNews = await _context.newsmodels
                .Join(
                    _context.Users,
                    news => news.byWhoNews,
                    user => user.Id,
                    (news, user) => new
                    {
                        id = news.id,
                        byWhoNews = user.Login,  // сразу получаем логин
                        title = news.title,
                        images = news.images,
                        dateTime = news.dateTime
                    })
                .ToListAsync();


            return Ok(formNews);
        }
    }
}
