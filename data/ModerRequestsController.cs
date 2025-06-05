using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureServer.Data;
using SecureServer.Models;

namespace SecureServer.Controllers
{
    [ApiController]
    [Route("api/modder")]
    public class ModerRequestsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthController> _logger;
        private readonly string BasePath;
        private readonly string _uploadPath;
        public ModerRequestsController(ApplicationDbContext context, ILogger<AuthController> logger, IConfiguration configuration)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            BasePath = configuration["Paths:PathToPublicFiles"]
            ?? throw new ArgumentNullException("Paths:PathToPublicFiles", "Путь в конфигурации не найден.");

            _uploadPath = Path.Combine(BasePath, "uploads");
        }

        [HttpGet("GetMyMods")]
        public async Task<IActionResult> GetMods()
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest("Username can't be null!");

            var user = await _context.moddevelopers.FirstOrDefaultAsync(u => u.nameOfMod == username);
            if (user == null) return BadRequest("Uncorrect username!");

            var mods = await _context.Mods
                .Where(md => md.modsby == user.modsby)
                .ToListAsync();

            return Ok(mods);
        }

        [HttpGet("GetMyPends")]
        public async Task<IActionResult> GetPends()
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest("Username can't be null!");

            var user = await _context.moddevelopers.FirstOrDefaultAsync(u => u.nameOfMod == username);
            if (user == null) return BadRequest("Uncorrect username!");

            var modsPends = await _context.pendingMods
                .Where(md => md.Developer == user.modsby)
                .ToListAsync();

            return Ok(modsPends);
        }

        [HttpPost("CreateMod")]
        public async Task<IActionResult> CreateAMod([FromBody] PendingMod pendMod)
        {
            if (pendMod == null) return BadRequest("Data is can't be null");

            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest("Username is can't be null!");

            if (pendMod.Developer != null && pendMod.Developer.Split("Mods by ")[0] != username)
            {
                pendMod.Developer = "Mods by " + username;
            }

            var mods = await _context.Mods.FirstOrDefaultAsync(dws => dws.NameDWS == pendMod.NameDWS);
            if (mods != null) return Ok(new { message = "Параметр NameDWS уже существует, выберите другое название!" });

            int maxAttempts = 10;
            int delayMs = 500;
            List<string> filesAssignments = pendMod?.image_url?.Split("!--!").ToList() ?? new();

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var dbFiles = await _context.filesdifine
                    .Where(f => f.modName == pendMod.NameDWS)
                    .Select(id => id.path)
                    .ToListAsync();

                if (dbFiles.Any())
                {
                    filesAssignments.AddRange(dbFiles);
                    break;
                }

                await Task.Delay(delayMs);
            }

            var stringsFiles = string.Join("!--!", filesAssignments);
            var newMod = new PendingMod
            {
                Developer = pendMod.Developer ?? ("Mods by " + username),
                Name = pendMod.Name,
                NameDWS = pendMod.NameDWS,
                Description = pendMod.Description,
                smallDescription = pendMod.smallDescription,
                categories = pendMod.categories,
                required = pendMod.required,
                price = pendMod.price,
                image_url = stringsFiles,
                refused = null
            };

            _context.pendingMods.Add(newMod);
            await _context.SaveChangesAsync();

            return Ok(newMod);
        }

        [HttpPut("UpdateMod")]
        public async Task<IActionResult> UpdateMod(int id, [FromBody] Mod pendMod)
        {
            if (pendMod == null) return BadRequest("Data is can't be null");

            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest("Username is can't be null!");

            var mods = await _context.Mods.FirstOrDefaultAsync(dws => dws.NameDWS == pendMod.NameDWS && id != dws.Id);
            if (mods != null) return Ok(new { message = "Параметр NameDWS уже существует, выберите другое название!" });

            var editingMod = await _context.Mods.FirstOrDefaultAsync(m => m.Id == id);
            if (editingMod == null) return BadRequest("Can't be found a mod by provided id!");

            int maxAttempts = 10;
            int delayMs = 500;
            List<string> filesAssignments = pendMod?.image_url?.Split("!--!", StringSplitOptions.RemoveEmptyEntries).ToList() ?? new();

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var dbFiles = await _context.filesdifine
                    .Where(f => f.modName == pendMod.NameDWS)
                    .Select(id => id.path)
                    .ToListAsync();

                if (dbFiles.Any())
                {
                    foreach (var dbFile in dbFiles)
                    {
                        if (filesAssignments == null || !filesAssignments.Contains(dbFile))
                        {
                            var fileEntity = await _context.filesdifine.FirstOrDefaultAsync(f => f.modName == pendMod.NameDWS && f.path == dbFile);
                            if (fileEntity != null)
                            {
                                string fullPath = Path.Combine("wwwroot", dbFile.Replace("/", Path.DirectorySeparatorChar.ToString()));
                                if (System.IO.File.Exists(fullPath))
                                {
                                    System.IO.File.Delete(fullPath);
                                }

                                _context.filesdifine.Remove(fileEntity);
                            }
                        }
                    }

                    filesAssignments.AddRange(dbFiles.Where(f => !filesAssignments.Contains(f)));
                    break;
                }

                await Task.Delay(delayMs);
            }

            var stringsFiles = string.Join("!--!", filesAssignments);

            editingMod.Name = pendMod.Name;
            editingMod.Description = pendMod.Description;
            editingMod.smallDescription = pendMod.smallDescription;
            editingMod.required = pendMod.required;
            editingMod.categories = pendMod.categories;
            editingMod.price = pendMod.price;
            editingMod.image_url = stringsFiles;

            _context.Mods.Update(editingMod);
            await _context.SaveChangesAsync();

            return Ok(editingMod);
        }

        [HttpPut("UpdatePend")]
        public async Task<IActionResult> UpdatePend(int id, [FromBody] PendingMod pendMod)
        {
            if (pendMod == null) return BadRequest("Data is can't be null");

            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest("Username is can't be null!");

            if (pendMod.Developer.Split("Mods by ")[0] != username)
            {
                pendMod.Developer = "Mods by " + username;
            }

            var mods = await _context.Mods.FirstOrDefaultAsync(dws => dws.NameDWS == pendMod.NameDWS);
            if (mods != null) return Ok(new { message = "Параметр NameDWS уже существует, выберите другое название!" });

            var editingMod = await _context.pendingMods.FirstOrDefaultAsync(m => m.Id == id);
            if (editingMod == null) return BadRequest("Can't be found a mod by provided id!");

            int maxAttempts = 10;
            int delayMs = 500;
            List<string> filesAssignments = pendMod?.image_url?.Split("!--!", StringSplitOptions.RemoveEmptyEntries).ToList() ?? new();

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var dbFiles = await _context.filesdifine
                    .Where(f => f.modName == pendMod.NameDWS)
                    .Select(f => f.path)
                    .ToListAsync();

                if (dbFiles.Any())
                {
                    foreach (var dbFile in dbFiles)
                    {
                        if (filesAssignments == null ||  !filesAssignments.Contains(dbFile))
                        {
                            var fileEntity = await _context.filesdifine
                                .FirstOrDefaultAsync(f => f.modName == pendMod.NameDWS && f.path == dbFile);

                            if (fileEntity != null)
                            {
                                string fullPath = Path.Combine("wwwroot", dbFile.Replace("/", Path.DirectorySeparatorChar.ToString()));
                                if (System.IO.File.Exists(fullPath))
                                {
                                    System.IO.File.Delete(fullPath);
                                }

                                _context.filesdifine.Remove(fileEntity);
                            }
                        }
                    }

                    filesAssignments.AddRange(dbFiles.Where(f => !filesAssignments.Contains(f)));
                    break;
                }

                await Task.Delay(delayMs);
            }

            var stringsFiles = string.Join("!--!", filesAssignments);

            editingMod.required = pendMod.required;
            editingMod.price = pendMod.price;
            editingMod.categories = pendMod.categories;
            editingMod.Description = pendMod.Description;
            editingMod.image_url = stringsFiles;
            editingMod.Name = pendMod.Name;
            editingMod.NameDWS = pendMod.NameDWS;
            editingMod.smallDescription = pendMod.smallDescription;

            _context.pendingMods.Update(editingMod);
            await _context.SaveChangesAsync();

            return Ok(editingMod);
        }


        [HttpDelete("RemoveMod")]
        public async Task<IActionResult> RemoveMod(int id)
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest("Username is can't be null!");

            var removalMod = await _context.Mods.FirstOrDefaultAsync(m => m.Id == id);
            if (removalMod == null) return BadRequest("Can't be found a mod by provided id!");

            if (removalMod.modsby.Split("Mods by ")[0] != username)
            {
                return BadRequest("You cant remove a not yours mod!");
            }

            _context.Mods.Remove(removalMod);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpDelete("RemovePend")]
        public async Task<IActionResult> RemovePend(int id)
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest("Username is can't be null!");

            var removalMod = await _context.pendingMods.FirstOrDefaultAsync(m => m.Id == id);
            if (removalMod == null) return BadRequest("Can't be found a mod by provided id!");

            if (removalMod.Developer.Split("Mods by ")[0] != username)
            {
                return BadRequest("You cant remove a not yours mod!");
            }

            _context.pendingMods.Remove(removalMod);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("GetDetailsBought")]
        public async Task<IActionResult> GetDetailsBought()
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == username);
            if (user == null) return BadRequest("Incorrect Username");

            var profitData = await _context.profitdata
                .Where(m => m.WhoEarn == user.Id)
                .ToListAsync(); // Сначала полностью забираем данные

            var modIds = profitData.Select(m => m.WhatBought).Distinct().ToList();

            // Теперь загружаем все нужные моды разом
            var mods = await _context.Mods
                .Where(m => modIds.Contains(m.Id.ToString()))
                .ToDictionaryAsync(m => m.Id.ToString(), m => m.Name);

            var data = profitData.Select(m => new
            {
                date = m.Date,
                profit = m.Amount - m.Profit,
                cashedOut = m.CashedOut,
                modName = mods.TryGetValue(m.WhatBought, out var name) ? name : "Unknown Mod"
            }).ToList();

            return Ok(data);
        }

        [HttpGet("GetDataModders")]
        public async Task<IActionResult> getDataModders()
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest("Username can't be null!");

            var user = await _context.Users.FirstOrDefaultAsync(m => m.Login == username);
            var userMod = await _context.moddevelopers.FirstOrDefaultAsync(u => u.nameOfMod == username);
            if (user == null) return BadRequest("Uncorrect username!");
            if (userMod == null) return BadRequest("Uncorrect username!");

            var modsDataOne = await _context.Mods
                .Where(md => md.modsby == userMod.modsby)
                .ToListAsync();

            var modsPends = await _context.pendingMods
                .Where(md => md.Developer == userMod.modsby)
                .ToListAsync();

            var profitData = await _context.profitdata
                .Where(m => m.WhoEarn == user.Id)
                .ToListAsync();

            var modIds = profitData.Select(m => m.WhatBought).Distinct().ToList();

            var mods = await _context.Mods
                .Where(m => modIds.Contains(m.Id.ToString()))
                .ToDictionaryAsync(m => m.Id.ToString(), m => m.Name);

            var data = profitData.Select(m => new
            {
                date = m.Date,
                profit = m.Amount - m.Profit,
                cashedOut = m.CashedOut,
                modName = mods.TryGetValue(m.WhatBought, out var name) ? name : "Unknown Mod"
            }).OrderByDescending(m => m.date).ToList();

            return Ok(new
            {
                pendings = modsPends,
                mods = modsDataOne,
                boughts = data
            });
        }

        [HttpGet("webhook")]
        public async Task<IActionResult> getWebHook(int? count)
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest(new { message = "Missing UserName header." });

            var dev = await _context.moddevelopers
                .FirstOrDefaultAsync(md => md.nameOfMod == username);

            if (dev == null)
                return BadRequest(new { message = "Developer not found." });

            var webhookData = await (
                from mod in _context.Mods orderby mod.modsby == dev.modsby
                join hook in _context.webhooks on mod.NameDWS equals hook.NameMod
                select hook
                ).ToListAsync();

            return Ok(webhookData);
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> SetWebHook(string NM, [FromBody] webhooksError webHook)
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest(new { message = "Missing UserName header." });

            var dev = await _context.moddevelopers
                .FirstOrDefaultAsync(md => md.nameOfMod == username);

            if (dev == null)
                return BadRequest(new { message = "Developer not found." });

            var webhookData = await (
                from hook in _context.webhooks
                join mod in _context.Mods on hook.NameMod equals mod.NameDWS
                where hook.NameMod == webHook.NameMod
                select new { hook, mod }
            ).FirstOrDefaultAsync();

            if (webhookData == null)
                return Ok(new { message = "Not correct data!" });

            if (webhookData.mod.modsby != dev.modsby)
                return Ok(new { message = "Are you sure about your permissions?" });

            webhookData.hook.Discord_web = webHook.Discord_web;
            webhookData.hook.Discord_web_OK = webHook.Discord_web_OK;
            webhookData.hook.Discord_web_ER = webHook.Discord_web_ER;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Webhook was updated!" });
        }

    }
}
