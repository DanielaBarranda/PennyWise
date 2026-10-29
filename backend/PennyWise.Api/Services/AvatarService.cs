using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Data;
using PennyWise.Api.Errors;
using PennyWise.Api.Models;

namespace PennyWise.Api.Services;

// Profile photos. The browser crops and shrinks the photo before uploading; the
// API still checks the size and the file's real type from its first bytes, so
// only genuine JPEG, PNG or WebP images are stored (never SVG or HTML).
public class AvatarService(PennyWiseDbContext db, AppClock clock)
{
    public const int MaxBytes = 256 * 1024;

    public async Task<UserAvatar?> GetAsync(int userId) =>
        await db.UserAvatars.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId);

    public async Task<User> SaveAsync(int userId, Stream body)
    {
        var data = await ReadLimitedAsync(body);
        if (data.Length == 0) throw new ValidationFailedException("photo", "Choose a photo to upload.");
        var contentType = DetectImageType(data)
            ?? throw new ValidationFailedException("photo", "Choose a JPEG, PNG or WebP image.");

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        var avatar = await db.UserAvatars.FirstOrDefaultAsync(a => a.UserId == userId);
        if (avatar is null)
        {
            avatar = new UserAvatar { UserId = userId };
            db.UserAvatars.Add(avatar);
        }
        avatar.ContentType = contentType;
        avatar.Data = data;
        user.AvatarUpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<User> RemoveAsync(int userId)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        await db.UserAvatars.Where(a => a.UserId == userId).ExecuteDeleteAsync();
        user.AvatarUpdatedAt = null;
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream body)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await body.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
                throw new ValidationFailedException("photo", "The photo must be 256 KB or smaller.");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    // Identified by the file's signature, not by its name or the Content-Type header.
    public static string? DetectImageType(ReadOnlySpan<byte> d)
    {
        if (d.Length >= 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF) return "image/jpeg";
        if (d.Length >= 8 && d[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        if (d.Length >= 12 && d[..4].SequenceEqual("RIFF"u8) && d[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }
}
