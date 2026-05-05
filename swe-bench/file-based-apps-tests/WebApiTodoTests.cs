using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Runfile.SweBench.Tests;

public sealed class WebApiTodoTests
{
    [Fact]
    public async Task RootEndpointStillReturnsHelloWorld()
    {
        await using var app = await FileAppServer.StartAsync(Path.Combine("flat", "webapi.cs"));

        var json = await app.Client.GetFromJsonAsync<JsonObject>("/");

        Assert.NotNull(json);
        Assert.Equal("Hello, World!", json["message"]?.GetValue<string>());
    }

    [Fact]
    public async Task TodoCrudEndpointsValidateAndPersistInMemory()
    {
        await using var app = await FileAppServer.StartAsync(Path.Combine("flat", "webapi.cs"));

        var initial = await app.Client.GetFromJsonAsync<JsonArray>("/todos");
        Assert.NotNull(initial);
        Assert.Empty(initial);

        var invalidCreate = await app.Client.PostAsJsonAsync("/todos", new { title = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, invalidCreate.StatusCode);

        var create = await app.Client.PostAsJsonAsync("/todos", new { title = "  write tests  " });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(created);
        var id = created["id"]!.GetValue<int>();
        Assert.True(id > 0);
        Assert.Equal("write tests", created["title"]!.GetValue<string>());
        Assert.False(created["isComplete"]!.GetValue<bool>());

        var read = await app.Client.GetFromJsonAsync<JsonObject>($"/todos/{id}");
        Assert.NotNull(read);
        Assert.Equal(id, read["id"]!.GetValue<int>());

        var update = await app.Client.PutAsJsonAsync($"/todos/{id}", new { title = "ship benchmark", isComplete = true });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(updated);
        Assert.Equal("ship benchmark", updated["title"]!.GetValue<string>());
        Assert.True(updated["isComplete"]!.GetValue<bool>());

        var missingUpdate = await app.Client.PutAsJsonAsync("/todos/999999", new { title = "missing", isComplete = false });
        Assert.Equal(HttpStatusCode.NotFound, missingUpdate.StatusCode);

        var delete = await app.Client.DeleteAsync($"/todos/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var missingRead = await app.Client.GetAsync($"/todos/{id}");
        Assert.Equal(HttpStatusCode.NotFound, missingRead.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocumentContainsTodoPaths()
    {
        await using var app = await FileAppServer.StartAsync(Path.Combine("flat", "webapi.cs"));

        var openApi = await app.Client.GetStringAsync("/openapi/v1.json");

        Assert.Contains("\"/todos\"", openApi);
        Assert.Contains("\"/todos/{id}\"", openApi);
    }
}
