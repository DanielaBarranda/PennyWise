using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using PennyWise.Api.Models;

namespace PennyWise.Api.Services.Email;

// Signed, time-limited links for email, made with the same Data Protection keys
// as the session cookie, so they can't be forged or changed.
//   - A password reset link carries the account's security stamp, so it stops
//     working once the password changes (it can be used only once).
//   - An email confirmation link carries the email it was sent to.
public sealed class EmailLinkTokens(IDataProtectionProvider dataProtection)
{
    public static readonly TimeSpan ResetLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan VerifyLifetime = TimeSpan.FromDays(3);

    private readonly ITimeLimitedDataProtector _protector =
        dataProtection.CreateProtector("PennyWise.Email.Link").ToTimeLimitedDataProtector();

    public string CreateReset(User user) => _protector.Protect($"reset|{user.Id}|{user.SecurityStamp}", ResetLifetime);

    public string CreateVerify(User user) => _protector.Protect($"verify|{user.Id}|{user.NormalizedEmail}", VerifyLifetime);

    // (user id, stamp or normalized email) when the token is valid for that purpose.
    public (int UserId, string Value)? Read(string? token, string purpose)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var parts = _protector.Unprotect(token.Trim()).Split('|', 3);
            if (parts.Length != 3 || parts[0] != purpose || !int.TryParse(parts[1], out var id)) return null;
            return (id, parts[2]);
        }
        catch (CryptographicException)
        {
            return null; // expired, changed, or not a PennyWise link
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

// The account emails' wording. Links point at the web app (PennyWise:PublicUrl).
public sealed class AccountEmails(EmailOutbox outbox, EmailLinkTokens tokens, IConfiguration configuration)
{
    private string PublicUrl => (configuration["PennyWise:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');

    public bool IsEnabled => outbox.IsEnabled;

    public void SendVerification(User user)
    {
        var link = $"{PublicUrl}/#/verify-email?token={Uri.EscapeDataString(tokens.CreateVerify(user))}";
        outbox.Enqueue(Build(user.Email, "Confirm your email for PennyWise",
            $"Hi {user.Name},",
            "Confirm this email address for your PennyWise account by opening the link below. It works for 3 days.",
            "Confirm my email", link,
            "If you didn't create a PennyWise account, you can ignore this email."));
    }

    public void SendPasswordReset(User user)
    {
        var link = $"{PublicUrl}/#/reset-password?token={Uri.EscapeDataString(tokens.CreateReset(user))}";
        outbox.Enqueue(Build(user.Email, "Reset your PennyWise password",
            $"Hi {user.Name},",
            "Someone asked to reset the password for your PennyWise account. Open the link below to choose a new one. It works for 1 hour and only once.",
            "Choose a new password", link,
            "If you didn't ask for this, ignore this email. Your password stays the same."));
    }

    public void SendPasswordChanged(User user)
    {
        outbox.Enqueue(Build(user.Email, "Your PennyWise password was changed",
            $"Hi {user.Name},",
            $"The password for your PennyWise account was changed on {DateTime.UtcNow.AddHours(8):MMMM d, yyyy 'at' h:mm tt} (Philippine time). Every other device was signed out.",
            null, null,
            "If this wasn't you, use \"Forgot password?\" on the log-in page right away to choose a new password."));
    }

    private static EmailMessage Build(string to, string subject, string greeting, string body, string? button, string? link, string footer)
    {
        var text = $"{greeting}\n\n{body}\n\n{(link is null ? "" : link + "\n\n")}{footer}\n\nPennyWise";
        string E(string s) => WebUtility.HtmlEncode(s);
        var buttonHtml = link is null ? "" :
            $"""<p style="margin:24px 0"><a href="{E(link)}" style="background:#c8102e;color:#ffffff;padding:12px 20px;border-radius:8px;text-decoration:none;font-weight:600;display:inline-block">{E(button!)}</a></p><p style="font-size:13px;color:#555">Or copy this link: <br><span style="word-break:break-all">{E(link)}</span></p>""";
        var html = $"""
            <div style="font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:1.5;color:#1a1a1a;max-width:520px">
              <p style="font-size:18px;font-weight:700;margin:0 0 16px">PennyWise</p>
              <p>{E(greeting)}</p>
              <p>{E(body)}</p>
              {buttonHtml}
              <p style="font-size:13px;color:#555">{E(footer)}</p>
            </div>
            """;
        return new EmailMessage(to, subject, text, html);
    }
}
