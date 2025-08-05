#!/usr/bin/env dotnet

new Game().Run();

class Game
{
    private readonly int width;
    private readonly int height;
    private Player player;
    private List<Enemy> enemies;
    private List<Bullet> bullets;
    private int enemyDirection; // 1 for right, -1 for left
    private bool gameOver;
    private bool playerWon;
    private int frameCount = 0;

    public Game(int width = 40, int height = 20)
    {
        Console.Clear();

        this.width = width;
        this.height = height;
        // Initialize player at bottom center.
        player = new Player(width / 2, height - 1);
        bullets = new List<Bullet>();
        enemies = new List<Enemy>();
        // Create a grid of enemies.
        int rows = 3;
        int cols = 8;
        int startX = 5;
        int startY = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                enemies.Add(new Enemy(startX + c, startY + r));
            }
        }
        enemyDirection = 1;
        gameOver = false;
        playerWon = false;
    }

    public void Run()
    {
        Console.CursorVisible = false;
        while (!gameOver)
        {
            HandleInput();
            UpdateGame();
            Render();
            Thread.Sleep(100);
        }
        Console.CursorVisible = true;
        Console.Clear();
        // Display end message with color.
        if (playerWon)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("You win! All enemies defeated.");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Game Over! An enemy reached the bottom.");
        }
        Console.ResetColor();
    }

    private void HandleInput()
    {
        if (Console.KeyAvailable)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            if (keyInfo.Key == ConsoleKey.LeftArrow)
            {
                player = player.MoveLeft();
            }
            else if (keyInfo.Key == ConsoleKey.RightArrow)
            {
                player = player.MoveRight(width);
            }
            else if (keyInfo.Key == ConsoleKey.Spacebar)
            {
                // Fire a bullet from just above the player.
                bullets.Add(new Bullet(player.X, player.Y - 1));
            }
            // Flush any additional key presses.
            while (Console.KeyAvailable) Console.ReadKey(true);
        }
    }

    private void UpdateGame()
    {
        // Update bullets: move them upward.
        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            bullets[i] = bullets[i].MoveUp();
            if (bullets[i].Y < 0)
            {
                bullets.RemoveAt(i);
            }
        }

        // Determine enemy boundaries.
        bool hitEdge = false;
        int minX = int.MaxValue, maxX = int.MinValue;
        foreach (var enemy in enemies)
        {
            if (enemy.X < minX) minX = enemy.X;
            if (enemy.X > maxX) maxX = enemy.X;
        }
        if ((enemyDirection == 1 && maxX >= width - 1) ||
            (enemyDirection == -1 && minX <= 0))
        {
            hitEdge = true;
        }

        // Update enemies.
        if (hitEdge)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                enemies[i] = new Enemy(enemies[i].X, enemies[i].Y + 1);
            }
            enemyDirection *= -1;
        }
        else
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                enemies[i] = new Enemy(enemies[i].X + enemyDirection, enemies[i].Y);
            }
        }

        // Collision detection: bullet vs enemy.
        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            for (int j = enemies.Count - 1; j >= 0; j--)
            {
                if (bullets[i].X == enemies[j].X && bullets[i].Y == enemies[j].Y)
                {
                    bullets.RemoveAt(i);
                    enemies.RemoveAt(j);
                    break;
                }
            }
        }

        // Check win condition.
        if (enemies.Count == 0)
        {
            playerWon = true;
            gameOver = true;
        }
        else
        {
            // Lose condition: any enemy reaches the player's row.
            foreach (var enemy in enemies)
            {
                if (enemy.Y >= player.Y)
                {
                    gameOver = true;
                    break;
                }
            }
        }
    }

    private void Render()
    {
        // Move cursor to top-left.
        Console.SetCursorPosition(0, 0);
        // Create a buffer for the inner game area.
        char[,] buffer = new char[height, width];
        for (int r = 0; r < height; r++)
        {
            for (int c = 0; c < width; c++)
            {
                buffer[r, c] = ' ';
            }
        }

        // Determine animated characters.
        char enemyChar = (frameCount % 2 == 0) ? 'X' : 'x';
        char playerChar = (frameCount % 2 == 0) ? '^' : 'A';
        char bulletChar = (frameCount % 2 == 0) ? '|' : '!';

        // Draw bullets.
        foreach (var bullet in bullets)
        {
            if (bullet.Y >= 0 && bullet.Y < height && bullet.X >= 0 && bullet.X < width)
                buffer[bullet.Y, bullet.X] = bulletChar;
        }
        // Draw player.
        if (player.Y >= 0 && player.Y < height && player.X >= 0 && player.X < width)
            buffer[player.Y, player.X] = playerChar;
        // Draw enemies.
        foreach (var enemy in enemies)
        {
            if (enemy.Y >= 0 && enemy.Y < height && enemy.X >= 0 && enemy.X < width)
                buffer[enemy.Y, enemy.X] = enemyChar;
        }

        // Draw the border.
        // Top border.
        string topBorder = "+" + new string('-', width) + "+";
        Console.WriteLine(topBorder);
        // For each row, draw left border, row content, then right border.
        for (int r = 0; r < height; r++)
        {
            Console.Write("|");
            for (int c = 0; c < width; c++)
            {
                char ch = buffer[r, c];
                // Set color based on entity.
                switch (ch)
                {
                    case '^':
                    case 'A':
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;
                    case 'X':
                    case 'x':
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case '|':
                    case '!':
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    default:
                        Console.ForegroundColor = ConsoleColor.Gray;
                        break;
                }
                Console.Write(ch);
            }
            Console.ResetColor();
            Console.WriteLine("|");
        }
        // Bottom border.
        string bottomBorder = "+" + new string('-', width) + "+";
        Console.WriteLine(bottomBorder);

        frameCount++;
    }

    public record Player(int X, int Y)
    {
        // Return a new Player with updated X when moving.
        public Player MoveLeft() => this with { X = X > 0 ? X - 1 : 0 };
        public Player MoveRight(int maxWidth) => this with { X = X < maxWidth - 1 ? X + 1 : maxWidth - 1 };
    }

    public record Enemy(int X, int Y);

    public record Bullet(int X, int Y)
    {
        // Move the bullet upward.
        public Bullet MoveUp() => this with { Y = Y - 1 };
    }
}
