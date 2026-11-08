using System.Net;
using System.Net.Mail;
using System.Threading.Channels;

namespace PennyWise.Api.Services.Email;

// Sending email (password reset, email confirmation, security notices).
//
// Set up with any SMTP service (for example Brevo, Resend, Amazon SES or a
// Gmail app password) through settings, never in code:
//   PennyWise__Email__Host       smtp.example.com
//   PennyWise__Email__Port       587
//   PennyWise__Email__Username   ...
//   PennyWise__Email__Password   ...   (a secret: app settings / environment only)
//   PennyWise__Email__From       PennyWise <no-reply@your-domain>
//   PennyWise__PublicUrl         https://your-site   (where links in emails point)
//
// Without a Host, email is off: the API says so honestly ("Email isn't set up")
// instead of pretending something was sent. In Development, emails are written
// to the log instead, so the links can be tested locally.
public sealed class EmailSettings
{
    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string From { get; init; } = "PennyWise <no-reply@penny-wise.app>";
    public bool EnableSsl { get; init; } = true;
}

public sealed record EmailMessage(string To, string Subject, string Text, string Html);

public interface IEmailSender
{
    // False when this server cannot send email.
    bool IsEnabled { get; }

    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed class SmtpEmailSender(EmailSettings settings) : IEmailSender
{
    public bool IsEnabled => true;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };
        if (!string.IsNullOrEmpty(settings.Username))
        {
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        }
        using var mail = new MailMessage { From = new MailAddress(ParseFrom(settings.From).Address, ParseFrom(settings.From).DisplayName) };
        mail.To.Add(message.To);
        mail.Subject = message.Subject;
        mail.Body = message.Text;
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.Html, null, "text/html"));
        await client.SendMailAsync(mail, cancellationToken);
    }

    private static MailAddress ParseFrom(string from) => new(from);
}

// Development only: prints the email (with its link) to the log.
public sealed class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public bool IsEnabled => true;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Email to {To}: {Subject}\n{Text}", message.To, message.Subject, message.Text);
        return Task.CompletedTask;
    }
}

public sealed class DisabledEmailSender : IEmailSender
{
    public bool IsEnabled => false;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}

// Emails are queued and sent in the background, so a request never waits for
// the mail server, and how long a request takes never hints whether an email
// address has an account.
public sealed class EmailOutbox(IEmailSender sender)
{
    private readonly Channel<EmailMessage> _queue = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropOldest });

    public bool IsEnabled => sender.IsEnabled;

    public void Enqueue(EmailMessage message)
    {
        if (sender.IsEnabled) _queue.Writer.TryWrite(message);
    }

    public ChannelReader<EmailMessage> Reader => _queue.Reader;
}

public sealed class EmailDispatcher(EmailOutbox outbox, IEmailSender sender, ILogger<EmailDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in outbox.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await sender.SendAsync(message, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The address is not logged: it is personal data.
                logger.LogError(ex, "Sending the email \"{Subject}\" failed.", message.Subject);
            }
        }
    }
}
