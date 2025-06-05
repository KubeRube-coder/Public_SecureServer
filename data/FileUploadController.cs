using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using SecureServer.Data;
using SecureServer.Models;
using System.Reflection.Metadata.Ecma335;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats;
using SecureServer.Controllers;

[ApiController]
[Route("api/upload")]
public class FileUploadController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly string BasePath;
    private readonly string _uploadPath;
    private readonly ILogger<FileUploadController> _logger;

    public FileUploadController(ApplicationDbContext context, IConfiguration configuration, ILogger<FileUploadController> logger)
    {
        _context = context;

        BasePath = configuration["Paths:PathToPublicFiles"]
            ?? throw new ArgumentNullException("Paths:PathToPublicFiles", "Путь в конфигурации не найден.");

        _uploadPath = Path.Combine(BasePath, "uploads");

        _logger = logger;
    }

    private readonly string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private readonly string[] allowedMimeTypes = { "image/jpeg", "image/png", "image/webp", "image/gif" };
    private const int MAX_FILES = 8; // Максимальное количество файлов

    [HttpPost]
    public async Task<IActionResult> UploadFiles([FromForm] List<IFormFile> files, [FromForm] string modName)
    {
        if (files == null || files.Count == 0)
            return BadRequest("Файлы не загружены");

        if (files.Count > MAX_FILES)
            return BadRequest($"Ошибка: можно загрузить максимум {MAX_FILES} файлов");

        if (string.IsNullOrWhiteSpace(modName))
            return BadRequest("Вы пытаетесь загрузить файлы без привязки к моду!");

        var username = Request.Headers["UserName"].SingleOrDefault();

        if (string.IsNullOrWhiteSpace(username))
            return BadRequest("Uncorrect user!");

        string directoryPath = Path.Combine(_uploadPath, username, modName);

        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        if (Directory.EnumerateFiles(directoryPath).Count() > MAX_FILES)
            return BadRequest($"Ошибка: можно загрузить максимум {MAX_FILES} файлов");

        var uploadedFiles = new List<string>();

        foreach (var file in files)
        {
            string ext = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(ext) || !allowedMimeTypes.Contains(file.ContentType.ToLower()))
                return BadRequest("Ошибка: можно загружать только изображения (JPEG, PNG, WebP, GIF)");

            using var inputStream = file.OpenReadStream();
            using var image = await Image.LoadAsync(inputStream);

            IImageEncoder encoder = file.ContentType switch
            {
                "image/png" => new PngEncoder { CompressionLevel = PngCompressionLevel.Level6 },
                "image/jpeg" => new JpegEncoder { Quality = 60 },
                _ => new JpegEncoder { Quality = 60 }
            };

            await using var outStream = new MemoryStream();
            await image.SaveAsync(outStream, encoder);
            outStream.Position = 0;

            string filePath = Path.Combine(directoryPath, file.FileName);

            await using var output = new FileStream(filePath, FileMode.Create);
            await outStream.CopyToAsync(output);

            uploadedFiles.Add(file.FileName);
            await CreateNewInfoAboutFile(modName, filePath, null);
            await ModifyModInfo(modName, filePath);
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "Файлы загружены", files = uploadedFiles });
    }

    [HttpDelete]
    public async Task<IActionResult> RemoveFiles([FromForm] string modName, [FromForm] string fileName)
    {
        if (string.IsNullOrWhiteSpace(modName))
            return BadRequest("Вы пытаетесь загрузить файлы без привязки к моду!");

        var username = Request.Headers["UserName"].SingleOrDefault();

        if (string.IsNullOrWhiteSpace(username))
            return BadRequest("Uncorrect user!");

        var cleanFileName = fileName;

        if (cleanFileName.StartsWith($"{username}/ ") || cleanFileName.StartsWith($"{username}\\"))
        {
            cleanFileName = cleanFileName.Substring($"{username}/".Length);
        }

        string filePath = Path.Combine(BasePath, cleanFileName);

        filePath = Path.GetFullPath(filePath);

        if (!filePath.StartsWith(Path.GetFullPath(BasePath)))
        {
            return BadRequest("Uncorrect filename");
        }

        if (!System.IO.File.Exists(filePath))
        {
            await DeleteModInfo(modName, fileName);

            return BadRequest($"File does not exist {filePath}");
        }

        System.IO.File.Delete(filePath);
        await DeleteModInfo(modName, fileName);

        return Ok(new { message = "Deleted" });
    }

    [HttpPost("id")]
    public async Task<IActionResult> UploadFiles(
    [FromForm] List<IFormFile> files,
    [FromForm] int modName) // или [FromQuery] — смотри как тебе удобнее
    {
        if (files == null || files.Count == 0)
            return BadRequest("Файлы не загружены");

        if (modName == 0)
            return BadRequest("Вы пытаетесь загрузить файлы без привязки к моду!");

        var username = Request.Headers["UserName"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(username))
            return BadRequest("Uncorrect user!");

        var modNameDWS = await _context.Mods.FirstOrDefaultAsync(md => md.Id == modName);
        if (modNameDWS == null)
            return BadRequest("Uncorrect Id");

        string directoryPath = Path.Combine(_uploadPath, username, modNameDWS.NameDWS);
        Directory.CreateDirectory(directoryPath);

        var uploadedFiles = new List<string>();

        foreach (var file in files)
        {
            string fileExtension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
                return BadRequest("Можно загружать только изображения (JPEG, PNG, WebP, GIF)");

            string filePath = Path.Combine(directoryPath, file.FileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            uploadedFiles.Add(file.FileName);
            await CreateNewInfoAboutFile(modNameDWS.NameDWS, filePath, null);
            await ModifyModInfo(modNameDWS.NameDWS, filePath);
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "Файлы загружены", files = uploadedFiles });
    }

    private async Task ModifyModInfo(string modName, string fullPath)
    {
        var modFromMods = await _context.Mods.FirstOrDefaultAsync(m => m.NameDWS == modName);
        var modFromPending = await _context.pendingMods.FirstOrDefaultAsync(p => p.NameDWS == modName);

        IModProvider? result = modFromMods != null
            ? modFromMods
            : modFromPending;

        if (result == null)
            return;

        var allImages = result.image_url?.Split("!--!", StringSplitOptions.RemoveEmptyEntries).ToList()
            ?? new List<string>();

        allImages.Add(Path.GetRelativePath(BasePath, fullPath).Replace("\\", "/"));
        result.image_url = string.Join("!--!", allImages);

        if (result is Mod mod)
            _context.Mods.Update(mod);
        else if (result is PendingMod pending)
            _context.pendingMods.Update(pending);
    }

    private async Task DeleteModInfo(string modName, string fullPath)
    {
        var modFromMods = await _context.Mods.FirstOrDefaultAsync(m => m.NameDWS == modName);
        var modFromPending = await _context.pendingMods.FirstOrDefaultAsync(p => p.NameDWS == modName);

        IModProvider? result = modFromMods != null
            ? modFromMods
            : modFromPending;

        if (result == null)
            return;

        var allImages = result.image_url?.Split("!--!", StringSplitOptions.RemoveEmptyEntries).ToList()
            ?? new List<string>();

        allImages.Remove(fullPath);
        _logger.LogInformation(fullPath);
        result.image_url = string.Join("!--!", allImages);

        if (result is Mod mod)
            _context.Mods.Update(mod);
        else if (result is PendingMod pending)
            _context.pendingMods.Update(pending);

        await _context.SaveChangesAsync();
    }

    private async Task CreateNewInfoAboutFile(string name, string fullPath, string? version)
    {
        var file = await _context.filesdifine.FirstOrDefaultAsync(f => f.path == fullPath);
        if (file != null)
        {
            return;
        }

        var newInfo = new FilesModel
        {
            modName = name,
            path = Path.GetRelativePath(BasePath, fullPath).Replace("\\", "/"),
            version = version
        };

        _context.filesdifine.Add(newInfo);
    }
}

[ApiController]
[Route("api/update")]
public class FileController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public FileController(ApplicationDbContext context)
    {
        _context = context;
        if (!Directory.Exists(_uploadPath))
            Directory.CreateDirectory(_uploadPath);
    }

    private readonly string _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "DayzWorkshopAppRelease");

    [HttpGet("getVersions")]
    public async Task<IActionResult> getFileVersion()
    {
        var version = await _context.filesdifine.FirstOrDefaultAsync(f => f.modName == "update.zip");

        return Ok(new { version = version?.version ?? "Not found last update!" });
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile([FromForm] IFormFile file, string version)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Файл не загружен");

        string filePath = Path.Combine(_uploadPath, "update.zip");

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        CreateNewInfoAboutFile(filePath, version);

        await _context.SaveChangesAsync();

        return Ok(new { message = "Файл загружен", fileName = "update.zip" });
    }

    [HttpPut]
    public async Task<IActionResult> UpdateFile([FromForm] IFormFile file, string version)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Файл не загружен");

        string filePath = Path.Combine(_uploadPath, "update.zip");

        if (!System.IO.File.Exists(filePath))
            return NotFound("Файл для обновления не найден");

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        UpdateInfoAboutFile(filePath, version);

        await _context.SaveChangesAsync();

        return Ok(new { message = "Файл обновлен", fileName = "update.zip" });
    }

    [HttpGet("latestversion")]
    public IActionResult DownloadStaticFile()
    {
        string filePath = Path.Combine(_uploadPath, "update.zip");

        if (!System.IO.File.Exists(filePath))
            return NotFound("Файл не найден");

        var fileStream = System.IO.File.OpenRead(filePath);
        return File(fileStream, "application/octet-stream", "update.zip");
    }

    private void CreateNewInfoAboutFile(string fullPath, string? version)
    {
        var file = _context.filesdifine.FirstOrDefault(f => f.path == fullPath);
        if (file != null)
        {
            return;
        }

        var newInfo = new FilesModel
        {
            modName = "update.zip",
            path = fullPath,
            version = version
        };

        _context.filesdifine.Add(newInfo);
    }

    private void UpdateInfoAboutFile(string filePath, string? version)
    {
        var existInfo = _context.filesdifine.FirstOrDefault(f => f.path == filePath);
        if (existInfo != null)
        {
            existInfo.version = version;
        }
    }
}
