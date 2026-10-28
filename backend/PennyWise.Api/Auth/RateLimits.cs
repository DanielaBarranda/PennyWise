namespace PennyWise.Api.Auth;

public static class RateLimits
{
    // Policy name for the sign-up and sign-in endpoints: a limited number of
    // attempts per minute from each IP address slows down password guessing
    // and checking which emails are registered.
    public const string Auth = "auth";
}
