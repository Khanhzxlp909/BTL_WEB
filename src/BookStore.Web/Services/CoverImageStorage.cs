using BookStore.Web.Infrastructure;
using SkiaSharp;

namespace BookStore.Web.Services;

public interface ICoverImageStorage
{
    Task<string> SaveAsync(IFormFile file, CancellationToken ct);
    Task DeleteAsync(string path, CancellationToken ct);
}

public sealed class CoverImageStorage(IWebHostEnvironment environment, ILogger<CoverImageStorage>? logger = null) : ICoverImageStorage
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private readonly string root = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads", "covers");

    public async Task<string> SaveAsync(IFormFile file, CancellationToken ct)
    {
        AppException Invalid(string message) => AdminErrors.Field("COVER_IMAGE_INVALID", "CoverFile", message);
        if (file.Length is <= 0 or > MaxBytes) throw Invalid(ValidationText.Pick(
            "Ảnh bìa phải có nội dung và không vượt 5 MB. Chọn ảnh JPEG, PNG hoặc WEBP khác.",
            "The cover must contain image data and be at most 5 MB. Choose another JPEG, PNG or WEBP file."));
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new Dictionary<string, SKEncodedImageFormat>(StringComparer.OrdinalIgnoreCase)
        { [".jpg"] = SKEncodedImageFormat.Jpeg, [".jpeg"] = SKEncodedImageFormat.Jpeg, [".png"] = SKEncodedImageFormat.Png, [".webp"] = SKEncodedImageFormat.Webp };
        if (!allowed.TryGetValue(extension, out var expected)) throw Invalid(ValidationText.Pick(
            "Đuôi tệp ảnh bìa không hợp lệ. Chỉ chọn .jpg, .jpeg, .png hoặc .webp.",
            "The cover file extension is invalid. Choose .jpg, .jpeg, .png or .webp."));
        await using var bytes = new MemoryStream();
        await using (var source = file.OpenReadStream())
        {
            var buffer = new byte[81920];
            int count;
            while ((count = await source.ReadAsync(buffer, ct)) > 0)
            {
                if (bytes.Length + count > MaxBytes) throw Invalid(ValidationText.Pick(
                    "Nội dung ảnh bìa vượt 5 MB. Hãy giảm kích thước tệp rồi thử lại.", "The cover data exceeds 5 MB. Reduce the file size and try again."));
                await bytes.WriteAsync(buffer.AsMemory(0, count), ct);
            }
        }
        bytes.Position = 0;
        using var codec = SKCodec.Create(bytes);
        if (codec is null) throw Invalid(ValidationText.Pick("Không thể giải mã ảnh bìa vì dữ liệu ảnh hỏng. Hãy xuất lại ảnh JPEG, PNG hoặc WEBP.", "The cover is corrupt. Export a valid JPEG, PNG or WEBP image."));
        var info = codec.Info;
        if (info.Width > 8000 || info.Height > 8000 || (long)info.Width * info.Height > 24_000_000)
            throw Invalid(ValidationText.Pick("Ảnh bìa quá lớn để xử lý: tối đa 8000 px mỗi chiều và 24 triệu điểm ảnh. Hãy thu nhỏ ảnh.", "The cover exceeds 8000 px per side or 24 million pixels. Resize the image."));
        if (codec.EncodedFormat != expected)
            throw Invalid(ValidationText.Pick("Nội dung ảnh bìa không khớp đuôi tệp. Xuất lại đúng định dạng JPEG, PNG hoặc WEBP.", "The cover content does not match its extension. Export it again as JPEG, PNG or WEBP."));
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw Invalid(ValidationText.Pick("Nội dung ảnh bìa bị hỏng hoặc thiếu dữ liệu. Hãy xuất lại ảnh đầy đủ rồi thử lại.", "The cover content is incomplete or corrupt. Export the complete image and try again."));
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, 85);
        if (encoded is null) throw new InvalidOperationException("Không thể chuyển ảnh bìa sang WEBP. Kiểm tra thư viện xử lý ảnh theo mã tra cứu.");
        var name = $"{Guid.NewGuid():N}.webp";
        var destination = Path.Combine(root, name);
        try
        {
            Directory.CreateDirectory(root);
            // Decode và encode lại, loại metadata/nội dung đính kèm; không tin tên/đường dẫn client.
            await File.WriteAllBytesAsync(destination, encoded.ToArray(), ct);
            return $"/uploads/covers/{name}";
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            if (File.Exists(destination))
            {
                try { File.Delete(destination); }
                catch (Exception cleanup) { throw new AggregateException("Ghi ảnh bìa thất bại và không thể dọn tệp mới. Kiểm tra kho ảnh theo mã tra cứu.", failure, cleanup); }
            }
            // Lỗi lưu trữ phía máy chủ (thư mục chỉ đọc, hết dung lượng, thiếu quyền): ghi log đầy đủ để tra cứu,
            // client chỉ nhận mô tả bước thất bại; sách chưa được lưu.
            logger?.LogError(failure, "COVER_STORAGE_UNAVAILABLE: không ghi được ảnh bìa vào {Root}.", root);
            throw new AppException(503, "COVER_STORAGE_UNAVAILABLE", ValidationText.Pick(
                "Không thể lưu ảnh bìa vì thư mục lưu ảnh trên máy chủ không ghi được (chỉ đọc, thiếu quyền hoặc hết dung lượng). Sách chưa được lưu. Lưu lại không kèm ảnh, hoặc báo quản trị hệ thống kiểm tra thư mục wwwroot/uploads/covers.",
                "The cover could not be stored because the server upload folder is not writable (read-only, no permission or out of space). The book was not saved. Save without a cover, or ask the system administrator to check wwwroot/uploads/covers."),
                new Dictionary<string, string[]> { ["CoverFile"] = [ValidationText.Pick("Thư mục lưu ảnh bìa trên máy chủ không ghi được.", "The server cover folder is not writable.")] });
        }
    }

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        var name = Path.GetFileName(path);
        if (path != $"/uploads/covers/{name}" || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(name), "N", out _) || Path.GetExtension(name) != ".webp")
            throw new InvalidOperationException("Không thể xóa ảnh bìa: đường dẫn không phải tệp hệ thống sinh. Kiểm tra đường dẫn lưu trữ.");
        File.Delete(Path.Combine(root, name));
        return Task.CompletedTask;
    }
}
