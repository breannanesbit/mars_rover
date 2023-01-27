﻿using Mars.Web.Types;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Mars.Web.Tests;

internal class BasicIntegrationTestDemo
{
    private WebApplicationFactory<Program> _factory;

    [SetUp]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {

                });
            });
    }

    [TestCase("/", "Mars Rover")]
    [TestCase("/Index", "Sorry, there's nothing at this address")]
    [TestCase("/About", "Sorry, there's nothing at this address")]
    [TestCase("/Privacy", "Sorry, there's nothing at this address")]
    [TestCase("/Contact", "Sorry, there's nothing at this address")]
    public async Task Get_EndpointsReturnSuccessAndCorrectContentType(string url, string contentFragment)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299
        Assert.AreEqual("text/html; charset=utf-8",
            response.Content.Headers.ContentType.ToString());
        var actualContent = await response.Content.ReadAsStringAsync();
        actualContent.Should().Contain(contentFragment);
    }

    [Test]
    public async Task JoinGame()
    {
        var client = _factory.CreateClient();

        //var response = await client.GetStringAsync("/game/join")
        var joinResponse = await client.GetFromJsonAsync<JoinResponse>("/game/join?name=Jonathan");

        joinResponse.Token.Length.Should().Be(13);
    }

    [Test]
    public async Task JoinGame_MissingName()
    {
        var client = _factory.CreateClient();

        var joinResponse = await client.GetAsync("/game/join");
        joinResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }
}
