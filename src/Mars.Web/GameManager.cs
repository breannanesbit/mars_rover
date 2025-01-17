using System.Diagnostics.CodeAnalysis;

namespace Mars.Web;

public class GameManager
{
    private readonly ILogger<Game> logger;

    public GameManager(List<Map> maps, ILogger<Game> logger)
    {
        CreatedOn = DateTime.Now;
        GameStartOptions = new GameStartOptions
        {
            Map = maps[0],
        };
        this.Maps = maps;
        this.logger = logger;
        StartNewGame(GameStartOptions);
    }
    public IReadOnlyList<Map> Maps { get; }

    /// <summary>
    /// If you were to restart this game instance, what options would you use?
    /// </summary>
    public GameStartOptions GameStartOptions { get; }

    /// <summary>
    /// When this instance of the Mars Rover game was instantiated
    /// </summary>
    public DateTime CreatedOn { get; }

    /// <summary>
    /// The game instance
    /// </summary>
    public Game Game { get; private set; }

    /// <summary>
    /// Did something important in the game change?
    /// </summary>
    public event EventHandler? GameStateChanged;

    public event EventHandler? NewGameStarted;

    /// <summary>
    /// Start a new game
    /// </summary>
    /// <param name="startOptions"></param>
    [MemberNotNull(nameof(Game))]
    public void StartNewGame(GameStartOptions startOptions)
    {
        //unsubscribe from old event
        if (Game != null)
        {
            Game.GameStateChanged -= Game_GameStateChanged;
            var playoptions = Game.GamePlayOptions;
            Game.Dispose();
            logger.LogWarning("Game ending {0}", playoptions);
            
        }

        NewGameStarted?.Invoke(this, new EventArgs());

        Game = new Game(startOptions, logger);
        logger.LogInformation("New game created");
        GameStateChanged?.Invoke(this, EventArgs.Empty);

        //subscribe to new event
        Game.GameStateChanged += Game_GameStateChanged;
    }

    public void PlayGame(GamePlayOptions playOptions)
    {
        Game?.PlayGame(playOptions);
    }

    private void Game_GameStateChanged(object? sender, EventArgs e)
    {
        GameStateChanged?.Invoke(this, e);
    }
}
