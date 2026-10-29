namespace PennyWise.Api.Models;

// A user's profile photo, kept apart from the Users table so signing in and
// loading the account never read the image bytes.
public class UserAvatar
{
    public int UserId { get; set; }

    public string ContentType { get; set; } = string.Empty;

    public byte[] Data { get; set; } = [];
}
