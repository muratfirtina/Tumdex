using Application.Services;
using Core.CrossCuttingConcerns.Exceptions;
using Infrastructure.Operations;
using Microsoft.AspNetCore.Http;
using SkiaSharp;

namespace Infrastructure.Services;

public class FileNameService : IFileNameService

{
    
    public async Task<string> FileRenameAsync(string pathOrContainerName,string fileName, IFileNameService.HasFile hasFileMethod)
    {
        string extension = Path.GetExtension(fileName);
        string oldName = Path.GetFileNameWithoutExtension(fileName);
        string regulatedFileName = NameOperation.CharacterRegulatory(oldName);
        regulatedFileName = regulatedFileName.ToLower().Trim('-', ' '); //harfleri küçültür ve baştaki ve sondaki - ve boşlukları siler
        //oldName = oldName.Replace("ç", "c").Replace("ğ", "g").Replace("ı", "i").Replace("ö", "o").Replace("ş", "s").Replace("ü", "u").Replace(" ", "-");
        //char[] invalidChars = { '$', ':', ';', '@', '+', '-', '_', '=', '(', ')', '{', '}', '[', ']' ,'∑','€','®','₺','¥','π','¨','~','æ','ß','∂','ƒ','^','∆','´','¬','Ω','√','∫','µ','≥','÷','|'}; //geçersiz karakterleri belirler.
        //oldName = oldName.TrimStart(invalidChars).TrimEnd(invalidChars); //baştaki ve sondaki geçersiz karakterleri siler
        //Regex regex = new Regex("[*'\",+._&#^@|/<>~]");
        //string regulatedFileName = NameOperation.CharacterRegulatory(oldName);
        //string newFileName = regex.Replace(regulatedFileName, string.Empty);//geçersiz karakterleri siler ve yeni dosya ismi oluşturur.
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
        //pathOrContainerName = pathOrContainerName.Replace("ç", "c").Replace("ğ", "g").Replace("ı", "i").Replace("ö", "o").Replace("ş", "s").Replace("ü", "u").Replace(" ", "-");
        //char[] invalidChars = { '$', ':', ';', '@', '+', '-', '_', '=', '(', ')', '{', '}', '[', ']', '∑', '€', '®', '₺', '¥', 'π', '¨', '~', 'æ', 'ß', '∂', 'ƒ', '^', '∆', '´', '¬', 'Ω', '√', '∫', 'µ', '≥', '÷', '|' }; //geçersiz karakterleri belirler.
        //pathOrContainerName = pathOrContainerName.TrimStart(invalidChars).TrimEnd(invalidChars); //baştaki ve sondaki geçersiz karakterleri siler
        //Regex regex = new Regex("[*'\",+._&#^@|/<>~]");
        //string newPath = regex.Replace(regulatedPath, string.Empty);//geçersiz karakterleri siler ve yeni dosya ismi oluşturur.
        return regulatedPath;
    }
    public async Task FileMustBeInFileFormat(IFormFile formFile)
    {
        List<string> extensions = new() { ".jpg", ".png", ".jpeg", ".webp", ".heic",".avif" };

        string extension = Path.GetExtension(formFile.FileName).ToLower();
        if (!extensions.Contains(extension))
            throw new BusinessException("Unsupported format");
        await Task.CompletedTask;
    }
    
    //maximum yüklenebilecek dosya boyutu
    public const int MaxFileSize = 5 * 1024 * 1024; //5MB
    
    private static void ResizeImage(string destinationPath, int percentage)
    {
        using (var originalBitmap = SKBitmap.Decode(destinationPath))
        {
            var newWidth = (int)Math.Round(originalBitmap.Width * percentage / 100.0);
            var newHeight = (int)Math.Round(originalBitmap.Height * percentage / 100.0);
        
            // Eski:
            // var scaledBitmap = originalBitmap.Resize(new SKImageInfo(newWidth, newHeight), SKBitmapResizeMethod.Lanczos3);
        
            // Yeni:
            var scaledBitmap = originalBitmap.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.High);
        
            using (var image = SKImage.FromBitmap(scaledBitmap))
            {
                using (var jpegData = image.Encode(SKEncodedImageFormat.Jpeg, 70))
                {
                    using (var stream = File.OpenWrite(destinationPath))
                    {
                        jpegData.SaveTo(stream);
                    }
                }

                using (var pngData = image.Encode(SKEncodedImageFormat.Png, 70))
                {
                    using (var stream = File.OpenWrite(destinationPath))
                    {
                        pngData.SaveTo(stream);
                    }
                }
            }
        }
    }
    
}