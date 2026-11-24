namespace Gatehouse.Accounts;

/// <summary>How emails are delivered.</summary>
public enum EmailDelivery
{
    /// <summary>Nothing is sent and a warning is logged per email. The default.</summary>
    None,

    /// <summary>Development only: the email is written to the log instead of being sent.</summary>
    Log,

    /// <summary>The email is sent over SMTP (see <see cref="SmtpOptions"/>).</summary>
    Smtp,
}

/// <summary>The "Email" section of the configuration.</summary>
public sealed class EmailOptions
{
    /// <summary>The name of the configuration section: "Email".</summary>
    public const string SectionName = "Email";

    /// <summary>How emails are delivered. <see cref="EmailDelivery.Log"/> is allowed in Development only. Default <see cref="EmailDelivery.None"/>.</summary>
    public EmailDelivery Delivery { get; init; } = EmailDelivery.None;

    /// <summary>True when emails are actually delivered or logged. Use it for <c>SignIn.RequireConfirmedEmail</c>: confirming is pointless when no email can arrive.</summary>
    public bool Enabled => Delivery != EmailDelivery.None;

    /// <summary>Where links in emails point, for example "https://app.example" (in development: the frontend dev server). Required unless Delivery is None.</summary>
    public string FrontendBaseUrl { get; init; } = "";

    /// <summary>The frontend page that confirms an email, relative to <see cref="FrontendBaseUrl"/>. Default "confirm-email".</summary>
    public string ConfirmEmailPath { get; init; } = "confirm-email";

    /// <summary>The frontend page that sets a new password, relative to <see cref="FrontendBaseUrl"/>. Default "reset-password".</summary>
    public string ResetPasswordPath { get; init; } = "reset-password";

    /// <summary>The sender's address. Required for SMTP.</summary>
    public string FromAddress { get; init; } = "";

    /// <summary>The sender's name.</summary>
    public string FromName { get; init; } = "";

    /// <summary>The SMTP server settings.</summary>
    public SmtpOptions Smtp { get; init; } = new();
}

/// <summary>The "Email:Smtp" section: the SMTP server.</summary>
public sealed class SmtpOptions
{
    /// <summary>The server's host name. Required for SMTP.</summary>
    public string Host { get; init; } = "";

    /// <summary>The port. 465 = TLS from the start, anything else (587 by default) = STARTTLS.</summary>
    public int Port { get; init; } = 587;

    /// <summary>The user name. Empty = no sign-in.</summary>
    public string Username { get; init; } = "";

    /// <summary>The password. A secret: user secrets locally, an environment variable (<c>Email__Smtp__Password</c>) in production.</summary>
    public string Password { get; init; } = "";
}
