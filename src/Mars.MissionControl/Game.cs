﻿using System.Collections.ObjectModel;

namespace Mars.MissionControl;
public class Game : IDisposable
{
    public Game(int boardWidth = 5, int boardHeight = 5) : this(new GameStartOptions
    {
        Height = boardHeight,
        Width = boardWidth,
        MapNumber = 1
    })
    {
    }

    public Game(GameStartOptions startOptions)
    {
        if (startOptions.Width < 3 || startOptions.Height < 3)
        {
            throw new BoardTooSmallException();
        }
        GameState = GameState.Joining;
        Board = new Board(startOptions.Width, startOptions.Height, startOptions.MapNumber);
        Map = new Map(this);
        TargetLocation = new Location(startOptions.Width / 2, startOptions.Height / 2);
        PerseveranceVisibilityRadius = startOptions.PerseveranceVisibilityRadius;
        IngenuityVisibilityRadius = startOptions.IngenuityVisibilityRadius;
        StartingBatteryLevel = startOptions.StartingBatteryLevel;
    }

    public int MapNumber => Board.MapNumber;

    public Location TargetLocation { get; private set; }
    public int PerseveranceVisibilityRadius { get; }
    public int IngenuityVisibilityRadius { get; }
    public int StartingBatteryLevel { get; }
    public Map Map { get; private set; }
    private ConcurrentDictionary<PlayerToken, Player> players = new();
    private ConcurrentDictionary<string, PlayerToken> playerTokenCache = new();

    public ReadOnlyCollection<Player> Players =>
        new ReadOnlyCollection<Player>(players.Values.ToList());
    private ConcurrentQueue<Player> winners = new();
    public IEnumerable<Player> Winners => winners.ToArray();

    #region State Changed
    public event EventHandler? GameStateChanged;
    public DateTime lastStateChange;
    public TimeSpan stateChangeFrequency;
    private void raiseStateChange()
    {
        if (lastStateChange + stateChangeFrequency < DateTime.Now)
        {
            GameStateChanged?.Invoke(this, EventArgs.Empty);
            lastStateChange = DateTime.Now;
        }
    }
    #endregion

    public JoinResult Join(string playerName)
    {
        if (GameState != GameState.Joining && GameState != GameState.Playing)
        {
            throw new InvalidGameStateException();
        }

        var player = new Player(playerName) { BatteryLevel = StartingBatteryLevel };
        player = player with
        {
            Location = Board.PlaceNewPlayer(player),
            Orientation = getRandomOrientation()
        };
        if (!players.TryAdd(player.Token, player) ||
           !playerTokenCache.TryAdd(player.Token.Value, player.Token))
        {
            throw new Exception("Unable to add new player...that token already exists?!");
        }

        raiseStateChange();

        return new JoinResult(
            player.Token,
            player.Location,
            player.Orientation,
            player.BatteryLevel,
            TargetLocation,
            Board.GetNeighbors(player.Location, PerseveranceVisibilityRadius),
            Map.LowResolution
        );
    }

    private static Orientation getRandomOrientation()
    {
        return (Orientation)Random.Shared.Next(0, 4);
    }

    public GamePlayOptions? GamePlayOptions { get; private set; }
    public GameState GameState { get; set; }
    public Board Board { get; private set; }
    private Timer? rechargeTimer;

    public void PlayGame() => PlayGame(new GamePlayOptions());

    public void PlayGame(GamePlayOptions gamePlayOptions)
    {
        if (GameState != GameState.Joining)
        {
            throw new InvalidGameStateException($"Cannot play game if currently {GameState}");
        }

        GamePlayOptions = gamePlayOptions;
        GameState = GameState.Playing;
        rechargeTimer = new Timer(timer_Callback, null, 1_000, 1_000);
    }

    private void timer_Callback(object? _)
    {
        foreach (var playerToken in players.Keys)
        {
            var origPlayer = players[playerToken];
            if (origPlayer.BatteryLevel < StartingBatteryLevel)
            {
                var newPlayer = origPlayer with { BatteryLevel = Math.Min(StartingBatteryLevel, origPlayer.BatteryLevel + GamePlayOptions!.RechargePointsPerSecond) };
                players.TryUpdate(playerToken, newPlayer, origPlayer);
            }
        }
        raiseStateChange();
    }

    public MoveResult MovePerseverance(PlayerToken token, Direction direction)
    {
        if (GameState != GameState.Playing)
        {
            throw new InvalidGameStateException();
        }

        if (players.ContainsKey(token) is false)
        {
            throw new UnrecognizedTokenException();
        }

        var player = players[token];
        var unmodifiedPlayer = player;
        string? message;

        if (direction == Direction.Right || direction == Direction.Left)
        {
            player = player with
            {
                BatteryLevel = player.BatteryLevel - 1,
                Orientation = player.Orientation.Turn(direction)
            };
            message = "Turned OK";
        }
        else
        {
            var desiredLocation = direction switch
            {
                Direction.Forward => player.CellInFront(),
                Direction.Reverse => player.CellInBack(),
                _ => throw new Exception("What direction do you think you're going?")
            };

            if (Board.Cells.ContainsKey(desiredLocation) is false)
            {
                player = player with
                {
                    BatteryLevel = player.BatteryLevel - 1
                };
                message = GameMessages.MovedOutOfBounds;
            }
            else
            {
                int newBatteryLevel = player.BatteryLevel - Board[desiredLocation].Difficulty.Value;
                if (newBatteryLevel >= 0)
                {
                    player = player with
                    {
                        BatteryLevel = newBatteryLevel,
                        Location = desiredLocation
                    };
                    message = GameMessages.MovedOK;
                }
                else
                {
                    message = GameMessages.InsufficientBattery;
                }
            }
        }

        if (!players.TryUpdate(token, player, unmodifiedPlayer))
        {
            throw new UnableToUpdatePlayerException();
        }

        if (player.Location == TargetLocation)//you win!
        {
            winners.Enqueue(player);
            players.Remove(player.Token, out _);
            message = GameMessages.YouMadeItToTheTarget;
        }

        raiseStateChange();

        return new MoveResult(
            player.Location,
            player.BatteryLevel,
            player.Orientation,
            Board.GetNeighbors(player.Location, PerseveranceVisibilityRadius),
            message ?? throw new Exception("Game message not set?!")
        );
    }

    public Location GetPlayerLocation(PlayerToken token) => players[token].Location;
    public bool TryTranslateToken(string tokenString, out PlayerToken? token)
    {
        token = null;
        if (playerTokenCache.ContainsKey(tokenString))
        {
            token = playerTokenCache[tokenString];
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        rechargeTimer?.Dispose();
    }
}

public static class GameMessages
{
    public const string MovedOutOfBounds = "Looks like you tried to move beyond the borders of the game.";
    public const string MovedOK = "Moved OK";
    public const string YouMadeItToTheTarget = "You made it to the target!";
    public const string InsufficientBattery = "Insufficient battery to make move.  Wait and recharge your battery.";
}

public record JoinResult(PlayerToken Token, Location PlayerLocation, Orientation Orientation, int BatteryLevel, Location TargetLocation, IEnumerable<Cell> Neighbors, IEnumerable<LowResolutionCell> LowResolutionMap);
public record MoveResult(Location Location, int BatteryLevel, Orientation Orientation, IEnumerable<Cell> Neighbors, string Message);

public enum GameState
{
    Joining,
    Playing,
    GameOver
}

public enum Direction
{
    Forward,
    Left,
    Right,
    Reverse
}
