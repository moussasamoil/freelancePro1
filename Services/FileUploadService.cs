namespace lotus_blue.Services
{
    public class FileUploadService
    {
        private readonly IWebHostEnvironment _hostEnvironment;

        public FileUploadService(IWebHostEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment;
        }

        public async Task<string> UploadFileAsync(IFormFile file, string subDirectory)
        {
            if (file == null) return null;

            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var webRootPath = _hostEnvironment.WebRootPath;
            var directoryPath = Path.Combine(webRootPath, subDirectory);

            // Ensure the directory exists
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var filePath = Path.Combine(directoryPath, fileName);

            filePath = filePath.Replace("\\", "/");

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // Returning the relative path without a leading slash
            return Path.Combine(subDirectory, fileName).Replace("\\", "/");
        }


        public void DeleteFile(string relativeFilePath)
        {
            if (string.IsNullOrWhiteSpace(relativeFilePath)) return;

            var fullPath = Path.Combine(_hostEnvironment.WebRootPath, relativeFilePath.TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        public async Task<string> UpdateFileAsync(string existingFilePath, IFormFile newFile, string subDirectory)
        {
            // Delete the old file
            DeleteFile(existingFilePath);

            // If there's no new file, return null (or you could return the existing path if you prefer)
            if (newFile == null) return null;

            // Upload and return the new file path
            return await UploadFileAsync(newFile, subDirectory);
        }
    }
}
