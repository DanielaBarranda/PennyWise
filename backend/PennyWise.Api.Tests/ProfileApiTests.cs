using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

public class ProfileApiTests : ApiTestBase
{
    private static object PasswordChange(string current, string next, string? confirm = null) =>
        new { currentPassword = current, newPassword = next, confirmPassword = confirm ?? next };

    [Fact]
    public async Task Name_can_be_changed_and_is_returned_by_me()
    {
        var response = await Client.PutAsJsonAsync("/api/auth/profile", new { name = "  Daniela B.  " });
        var me = await Client.GetFromJsonAsync<UserResponse>("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Daniela B.", me!.Name);
        Assert.Equal(DefaultEmail, me.Email);
    }

    [Fact]
    public async Task Blank_name_is_rejected()
    {
        var response = await Client.PutAsJsonAsync("/api/auth/profile", new { name = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Name", (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Keys);
    }

    [Theory]
    [InlineData("Wrong2026", "NewBudget2027", "CurrentPassword")]
    [InlineData(DefaultPassword, "short", "NewPassword")]
    [InlineData(DefaultPassword, DefaultPassword, "NewPassword")]
    public async Task Bad_password_changes_are_rejected(string current, string next, string field)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/change-password", PasswordChange(current, next));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Keys);
    }

    [Fact]
    public async Task Changing_the_password_replaces_it_and_signs_out_other_devices()
    {
        var otherDevice = NewClient();
        (await LoginAsync(otherDevice, DefaultEmail)).EnsureSuccessStatusCode();

        var change = await Client.PostAsJsonAsync("/api/auth/change-password", PasswordChange(DefaultPassword, "NewBudget2027"));

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/expenses")).StatusCode);                 // this device stays in
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.GetAsync("/api/expenses")).StatusCode); // the other is out
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), DefaultEmail)).StatusCode);  // old password fails
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), DefaultEmail, "NewBudget2027")).StatusCode);
    }

    [Fact]
    public async Task Profile_endpoints_require_sign_in()
    {
        var anonymous = NewClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync("/api/auth/profile", new { name = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/auth/change-password", PasswordChange(DefaultPassword, "NewBudget2027"))).StatusCode);
    }
}
