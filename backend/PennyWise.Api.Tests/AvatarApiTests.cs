using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

public class AvatarApiTests : ApiTestBase
{
    // Smallest valid file signatures followed by filler bytes; the API checks the
    // signature, not the pixels.
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 5, 6, 7, 8];

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] data, string contentType = "image/png")
    {
        var content = new ByteArrayContent(data);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return client.PutAsync("/api/auth/avatar", content);
    }

    [Fact]
    public async Task New_account_has_no_photo()
    {
        var me = await Client.GetFromJsonAsync<UserResponse>("/api/auth/me");

        Assert.Null(me!.AvatarVersion);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/api/auth/avatar")).StatusCode);
    }

    [Fact]
    public async Task Uploaded_photo_is_served_back_as_an_image_only()
    {
        var upload = await UploadAsync(Client, Png);
        var user = await upload.Content.ReadFromJsonAsync<UserResponse>();
        var photo = await Client.GetAsync($"/api/auth/avatar?v={user!.AvatarVersion}");

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.NotNull(user.AvatarVersion);
        Assert.Equal("image/png", photo.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Png, await photo.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", photo.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("sandbox", photo.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Type_comes_from_the_file_not_the_header()
    {
        await UploadAsync(Client, Jpeg, contentType: "image/png");
        var photo = await Client.GetAsync("/api/auth/avatar");

        Assert.Equal("image/jpeg", photo.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Replacing_the_photo_changes_the_version()
    {
        var first = await (await UploadAsync(Client, Png)).Content.ReadFromJsonAsync<UserResponse>();
        await Task.Delay(5);
        var second = await (await UploadAsync(Client, Jpeg, "image/jpeg")).Content.ReadFromJsonAsync<UserResponse>();

        Assert.NotEqual(first!.AvatarVersion, second!.AvatarVersion);
        Assert.Equal(Jpeg, await Client.GetByteArrayAsync("/api/auth/avatar"));
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>", "image/svg+xml")]
    [InlineData("<html><body>hi</body></html>", "image/png")]
    [InlineData("GIF89a......", "image/gif")]
    public async Task Non_photo_files_are_rejected(string body, string contentType)
    {
        var response = await UploadAsync(Client, System.Text.Encoding.UTF8.GetBytes(body), contentType);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal("Choose a JPEG, PNG or WebP image.", problem!.Errors.Values.Single().Single());
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/api/auth/avatar")).StatusCode);
    }

    [Fact]
    public async Task Empty_upload_is_rejected()
    {
        var response = await UploadAsync(Client, []);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Photo_over_256_KB_is_rejected()
    {
        var big = new byte[256 * 1024 + 1];
        Png.CopyTo(big, 0);

        var response = await UploadAsync(Client, big);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/api/auth/avatar")).StatusCode);
    }

    [Fact]
    public async Task Photo_can_be_removed()
    {
        await UploadAsync(Client, Png);

        var response = await Client.DeleteAsync("/api/auth/avatar");
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(user!.AvatarVersion);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/api/auth/avatar")).StatusCode);
    }

    [Fact]
    public async Task Each_user_only_ever_gets_their_own_photo()
    {
        await UploadAsync(Client, Png);
        var other = await NewUserAsync("other@pennywise.test");

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/auth/avatar")).StatusCode);
        await UploadAsync(other, Jpeg, "image/jpeg");
        Assert.Equal(Png, await Client.GetByteArrayAsync("/api/auth/avatar"));
    }

    [Fact]
    public async Task Photo_needs_sign_in_and_the_client_header()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient().GetAsync("/api/auth/avatar")).StatusCode);

        var noHeader = Factory.CreateClient();
        var content = new ByteArrayContent(Png);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        Assert.NotEqual(HttpStatusCode.OK, (await noHeader.PutAsync("/api/auth/avatar", content)).StatusCode);
    }
}
