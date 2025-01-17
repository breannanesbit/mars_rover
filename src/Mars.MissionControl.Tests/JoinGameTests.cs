﻿using Mars.MissionControl.Exceptions;
using System.Linq;

namespace Mars.MissionControl.Tests;

internal class JoinGameTests
{
    [Test]
    public void GameHasTarget()
    {
        var game = Helpers.CreateGame(5, 5);
        game.TargetLocation.X.Should().BeGreaterThan(0);
        game.TargetLocation.Y.Should().BeGreaterThan(0);
    }

    [Test]
    public void CanJoinGame()
    {
        var game = Helpers.CreateGame(5, 5);
        var joinResult = game.Join("Jonathan");
        joinResult.Should().NotBeNull();
        joinResult.BatteryLevel.Should().Be(new GameStartOptions().StartingBatteryLevel);
    }

    [Test]
    public void GameGeneratesATokenValueObject()
    {
        var game = Helpers.CreateGame(5, 5);
        var joinResult = game.Join("P1");
        joinResult.Token.Should().BeOfType<PlayerToken>();
    }

    [Test]
    public void SecondPlayerCanJoin()
    {
        var game = Helpers.CreateGame(5, 5);
        var token1 = game.Join("P1");
        var token2 = game.Join("P2");
        token1.Should().NotBe(token2);
    }

    [Test]
    public void CannotPlayIfYouHaveNotJoined()
    {
        var game = Helpers.CreateGame(5, 5);
        game.PlayGame();
        var token1 = PlayerToken.Generate();
        Assert.Throws<UnrecognizedTokenException>(() => game.MovePerseverance(token1, Direction.Forward));
    }

    [Test]
    public void PlayerCannotMoveIfGameStateIsNotPlaying()
    {
        var game = Helpers.CreateGame(5, 5);
        var joinResult = game.Join("P1");
        var token1 = joinResult.Token;
        Assert.Throws<InvalidGameStateException>(() => game.MovePerseverance(token1, Direction.Forward));
    }

    [Test]
    public void WhenAPlayerJoinsTheyArePlacedOnTheMap()
    {
        var game = Helpers.CreateGame(5, 5);
        var joinResult = game.Join("P1");
        var token = joinResult.Token;
        var location = game.GetPlayerLocation(token);
        location.Should().NotBeNull();
    }

    [Test]
    public void TooManyPlayersCannotJoinGame()
    {
        var game = Helpers.CreateGame(5, 5);
        foreach (var i in Enumerable.Range(0, 16))
        {
            game.Join($"P{i}");
        }
        Assert.Throws<TooManyPlayersException>(() => game.Join("P17"));
    }
}
