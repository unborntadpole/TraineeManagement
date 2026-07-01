namespace SubmissionProcessor.Worker.Services;

using System.IO;
// using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

using SubmissionProcessor.Worker.DTO;
using SubmissionProcessor.Worker.Constants;

// SaveAsync, OpenReadAsync, ExistsAsync, and DeleteAsync 
public interface IFileStorageService
{
    Task<Result<string>> SaveAsync(IFormFile file);
    Task<Result<FileStream>> OpenReadAsync(string fileName);
    Task<Result<bool>> ExistsAsync(string fileName);
    Task<Result<string>> DeleteAsync(string fileName);
}

public class LocalFileStorageService : IFileStorageService
{
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly IHostEnvironment _environment;
    public LocalFileStorageService(IHostEnvironment environment, ILogger<LocalFileStorageService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<Result<string>> SaveAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("Save file failed: File is empty");
            return Result<string>.ServerError("File cannot be empty", 400);
        } 
        else if (file.Length > UploadFilesConstants.MaxLength)
        {
            _logger.LogWarning("Save file failed: File is too big");
            return Result<string>.ServerError($"File too big. Max size is {UploadFilesConstants.MaxLength/1024/1024} megabytes", 413);
        }
        
        var ext = Path.GetExtension(file.FileName);
        if (!UploadFilesConstants.AllowedExtensions.Contains(ext))
        {
            _logger.LogWarning("File save failed: File extension invalid");
            return Result<string>.ServerError($"File type not compatible. Only {string.Join(",", UploadFilesConstants.AllowedExtensions)} extensions are allowed.", 415);
            // throw new ArgumentException($"Only {string.Join(",", UploadFilesConstants.AllowedExtensions)} are allowed.");
        }        

        var path = Path.Combine(_environment.ContentRootPath, UploadFilesConstants.UploadDirectory);

        if (!Directory.Exists(path))
        {
            _logger.LogInformation("File save: Created a direcetory for file upload");
            Directory.CreateDirectory(path);
        }

        try
        {
            var fileName = $"{Guid.NewGuid().ToString()}{ext}";
            var fileNameWithPath = Path.Combine(path, fileName);
            using var stream = new FileStream(fileNameWithPath, FileMode.Create);
            await file.CopyToAsync(stream);
            _logger.LogInformation($"File save successful. File name {fileName}");
            return Result<string>.Success(fileName);
        }
        catch (Exception e)
        {
            _logger.LogWarning($"File save failed: {e.Message}");
            return Result<string>.ServerError($"File save failed: \n{e.Message}",500);
        }
    }

    public async Task<Result<FileStream>> OpenReadAsync( string fileName)
    {
        try
        {
            var path = Path.Combine(_environment.ContentRootPath, UploadFilesConstants.RequestPath);
            path = Path.Combine(path, fileName);
            if (!File.Exists(path))
            {
                _logger.LogWarning($"File open failed: File with name {fileName} not found");
                return Result<FileStream>.ServerError($"File with name {fileName} not found",404);
            }
            // using (StreamReader sr = new StreamReader(path))
            // {
            //     string content= await sr.ReadToEndAsync();
            //     _logger.LogInformation($"Successfully read file {fileName}");
            //     return Result<string>.Success(content);
            // }

            return Result<FileStream>.Success(File.OpenRead(path));
        }
        catch (Exception e)
        {
            _logger.LogWarning($"Failed to read the file {fileName}. \n{e.Message}");
            return Result<FileStream>.ServerError(e.Message,500);
        }
    }

    public async Task<Result<string>> DeleteAsync( string fileName)
    {        try
        {
            var path = Path.Combine(_environment.ContentRootPath, UploadFilesConstants.RequestPath);
            path = Path.Combine(path, fileName);
            if (!File.Exists(path))
            {
                _logger.LogWarning($"File delete failed: File {fileName} was not found");
                return Result<string>.ServerError($"No file with name {fileName} found",500);
            }
            File.Delete(path);
            return Result<string>.Success($"{fileName}");
        }
        catch (Exception e)
        {
            _logger.LogWarning($"Failed to find the file {fileName}. \n{e.Message}");
            return Result<string>.ServerError(e.Message,500);
        }
    }

    public async Task<Result<bool>> ExistsAsync(string fileName)
    {
        try
        {
            var path = Path.Combine(_environment.ContentRootPath, UploadFilesConstants.UploadDirectory);
            path = Path.Combine(path, fileName);
            if (File.Exists(path))
            {
                _logger.LogInformation($"Found file {fileName}");
                return Result<bool>.Success(true);
            }
            return Result<bool>.ServerError($"File does not exist with name {fileName}", 404);
        }
        catch (Exception e)
        {
            _logger.LogWarning($"Failed to find the file {fileName}. \n{e.Message}");
            return Result<bool>.ServerError(e.Message,500);
        }
    }

}