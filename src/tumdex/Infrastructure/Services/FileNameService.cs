using Application.Services;
using Core.CrossCuttingConcerns.Exceptions;
using Infrastructure.Operations;
using Microsoft.AspNetCore.Http;
using SkiaSharp;

namespace Infrastructure.Services;

public class FileNameService : IFileNameService
{
    // Add video file extensions to the validation
    private readonly List<string> _allowedImageExtensions = new() { ".jpg", ".png", ".jpeg", ".webp", ".heic", ".avif" };
    private readonly List<string> _allowedVideoExtensions = new() { ".mp4", ".webm", ".mov", ".avi", ".mkv" };
    
    // Maximum file sizes
    public const int MaxImageSize = 5 * 1024 * 1024; // 5MB
    public const int MaxVideoSize = 50 * 1024 * 1024; // 50MB
    
    public async Task<string> FileRenameAsync(string pathOrContainerName,string fileName, IFileNameService.HasFile hasFileMethod)
    {
        string extension = Path.GetExtension(fileName);
        string oldName = Path.GetFileNameWithoutExtension(fileName);
        string regulatedFileName = NameOperation.CharacterRegulatory(oldName);
        regulatedFileName = regulatedFileName.ToLower().Trim('-', ' '); //harfleri küçültür ve baştaki ve sondaki - ve boşlukları siler
        
        DateTime datetimenow = DateTime.UtcNow;
        string datetimeutcnow = datetimenow.ToString("yyyyMMddHHmmss");//dosya isminin sonuna eklenen tarih bilgisi
        string fullName = $"{regulatedFileName}-{datetimeutcnow}{extension}";//dosya ismi ve uzantısı birleştirilir ve yeni dosya ismi oluşturulur.

        if (hasFileMethod(pathOrContainerName, fullName))
        {
            int i = 1;
            while (hasFileMethod(pathOrContainerName, fullName))
            {
                fullName = $"{regulatedFileName}-{extension}";
                i++;
            }
        }

        return fullName;
    }
    
    public async Task<string> PathRenameAsync(string pathOrContainerName)
    {
        string regulatedPath = NameOperation.CharacterRegulatory(pathOrContainerName);
        regulatedPath = regulatedPath.ToLower().Trim('-', ' '); //harfleri küçültür ve baştaki ve sondaki - ve boşlukları siler
        return regulatedPath;
    }
    
    public async Task FileMustBeInFileFormat(IFormFile formFile)
    {
        string extension = Path.GetExtension(formFile.FileName).ToLower();
        
        // Check if it's either an allowed image or video format
        if (!_allowedImageExtensions.Contains(extension) && !_allowedVideoExtensions.Contains(extension))
            throw new BusinessException("Unsupported file format");
            
        // Check file size limits
        if (_allowedImageExtensions.Contains(extension) && formFile.Length > MaxImageSize)
            throw new BusinessException($"Image file size exceeds limit of {MaxImageSize / (1024 * 1024)}MB");
            
        if (_allowedVideoExtensions.Contains(extension) && formFile.Length > MaxVideoSize)
            throw new BusinessException($"Video file size exceeds limit of {MaxVideoSize / (1024 * 1024)}MB");
            
        await Task.CompletedTask;
    }
    
    public async Task FileMustBeInImageFormat(IFormFile formFile)
    {
        string extension = Path.GetExtension(formFile.FileName).ToLower();
        if (!_allowedImageExtensions.Contains(extension))
            throw new BusinessException("Unsupported image format");
            
        if (formFile.Length > MaxImageSize)
            throw new BusinessException($"Image file size exceeds limit of {MaxImageSize / (1024 * 1024)}MB");
            
        await Task.CompletedTask;
    }
    
    public async Task FileMustBeInVideoFormat(IFormFile formFile)
    {
        string extension = Path.GetExtension(formFile.FileName).ToLower();
        if (!_allowedVideoExtensions.Contains(extension))
            throw new BusinessException("Unsupported video format");
            
        if (formFile.Length > MaxVideoSize)
            throw new BusinessException($"Video file size exceeds limit of {MaxVideoSize / (1024 * 1024)}MB");
            
        await Task.CompletedTask;
    }
    
    public bool IsVideoFile(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLower();
        return _allowedVideoExtensions.Contains(extension);
    }
    
    public bool IsImageFile(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLower();
        return _allowedImageExtensions.Contains(extension);
    }
}