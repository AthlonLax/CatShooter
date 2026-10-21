using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace CatShooters
{
    public partial class Form1 : Form
    {
        // ==================== game Settings ====================
        int gameWidth = 450;
        int gameHeight = 700;
        Timer gameTimer = new Timer();
        Random random = new Random();

        enum GameScreen { Menu, Playing, HighScores, GameOver }
        GameScreen currentScreen = GameScreen.Menu;

        // ==================== player and enemy ====================
        HashSet<Keys> pressedKeys = new HashSet<Keys>();
        List<Enemy> enemies = new List<Enemy>();
        List<Bullet> bullets = new List<Bullet>();
        Image playerImage;
        Image backgroundImage;
        Image[] catImages;
        float playerX = 193;
        float playerY = 590;
        float backgroundY = 0;
        int score = 0;
        int lives = 3;
        int frame = 0;
        int shootCooldown = 0;
        int invincibleFrames = 0;
        bool gameOver = false;

        // ==================== LEADERBOARD DATA ====================
        List<ScoreEntry> highScores = new List<ScoreEntry>();
        string scoreFile = Path.Combine(Application.StartupPath, "highscores.txt");
        bool scoreSaved = false;

        // ==================== FORM SETUP ====================
        public Form1()
        {
            InitializeComponent();
            Text = "CatShooters - Space Cat Invasion";
            ClientSize = new Size(gameWidth, gameHeight);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            KeyPreview = true;

            // Images must be added to Project Properties > Resources.
            playerImage = Properties.Resources.player;
            backgroundImage = Properties.Resources.background;
            catImages = new Image[]
            {
                Properties.Resources.cat_basic,
                Properties.Resources.cat_fast,
                Properties.Resources.cat_armored,
                Properties.Resources.cat_boss
            };

            KeyDown += FormKeyDown;
            KeyUp += FormKeyUp;
            Deactivate += (s, e) => pressedKeys.Clear();
            LoadHighScores();
            gameTimer.Interval = 16;
            gameTimer.Tick += GameLoop;
            gameTimer.Start();
        }

        // ==================== KEYBOARD CONTROLS ====================
        private void FormKeyDown(object sender, KeyEventArgs e)
        {
            if (currentScreen == GameScreen.Menu)
            {
                if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1)
                    StartGame();
                else if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2)
                {
                    LoadHighScores();
                    currentScreen = GameScreen.HighScores;
                    Invalidate();
                }
                else if (e.KeyCode == Keys.D3 || e.KeyCode == Keys.NumPad3)
                    Close();
                return;
            }

            if (currentScreen == GameScreen.HighScores)
            {
                if (e.KeyCode == Keys.Escape)
                    ShowMenu();
                return;
            }

            if (currentScreen == GameScreen.GameOver)
            {
                if (e.KeyCode == Keys.Enter)
                    StartGame();
                else if (e.KeyCode == Keys.M)
                    ShowMenu();
                return;
            }

            if (currentScreen == GameScreen.Playing)
                pressedKeys.Add(e.KeyCode);
        }

        private void FormKeyUp(object sender, KeyEventArgs e)
        {
            pressedKeys.Remove(e.KeyCode);
        }

        // ==================== MAIN GAME LOOP ====================
        private void GameLoop(object sender, EventArgs e)
        {
            backgroundY += 0.5f;
            if (backgroundY >= gameHeight)
                backgroundY = 0;

            if (currentScreen != GameScreen.Playing)
            {
                Invalidate();
                return;
            }

            frame++;
            if (shootCooldown > 0) shootCooldown--;
            if (invincibleFrames > 0) invincibleFrames--;

            MovePlayer();
            Shoot();
            SpawnEnemies();
            MoveBullets();
            MoveEnemies();
            CheckCollisions();

            if (gameOver)
            {
                currentScreen = GameScreen.GameOver;
                pressedKeys.Clear();
                gameTimer.Stop();
                AskPlayerName();
                gameTimer.Start();
            }
            Invalidate();
        }

        // ==================== PLAYER MOVEMENT ====================
        private void MovePlayer()
        {
            float speed = 5;
            if (pressedKeys.Contains(Keys.A) || pressedKeys.Contains(Keys.Left)) playerX -= speed;
            if (pressedKeys.Contains(Keys.D) || pressedKeys.Contains(Keys.Right)) playerX += speed;
            if (pressedKeys.Contains(Keys.W) || pressedKeys.Contains(Keys.Up)) playerY -= speed;
            if (pressedKeys.Contains(Keys.S) || pressedKeys.Contains(Keys.Down)) playerY += speed;
            playerX = Math.Max(0, Math.Min(playerX, gameWidth - 64));
            playerY = Math.Max(50, Math.Min(playerY, gameHeight - 70));
        }

        // ==================== PLAYER SHOOTING ====================
        private void Shoot()
        {
            if (pressedKeys.Contains(Keys.Space) && shootCooldown == 0)
            {
                bullets.Add(new Bullet(playerX + 29, playerY - 12));
                shootCooldown = 12;
            }
        }

        // ==================== ENEMY SPAWNING ====================
        private void SpawnEnemies()
        {
            int spawnRate = Math.Max(20, 48 - score / 150);
            if (frame % spawnRate != 0) return;

            int roll = random.Next(100);
            int type;
            if (roll < 50) type = 0;
            else if (roll < 78) type = 1;
            else if (roll < 96) type = 2;
            else type = 3;

            int size = 54;
            int health = 1;
            float speed = 2.5f;
            if (type == 1) { size = 58; speed = 5; }
            else if (type == 2) { size = 75; health = 3; speed = 1.8f; }
            else if (type == 3) { size = 110; health = 10; speed = 1.2f; }

            float x = random.Next(0, gameWidth - size);
            enemies.Add(new Enemy(x, -size, size, speed, health, type));
        }

        // ==================== BULLET MOVEMENT ====================
        private void MoveBullets()
        {
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                bullets[i].Y -= 10;
                if (bullets[i].Y < -30) bullets.RemoveAt(i);
            }
        }

        // ==================== ENEMY MOVEMENT ====================
        private void MoveEnemies()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                enemies[i].Y += enemies[i].Speed;
                if (enemies[i].Y > gameHeight + 100) enemies.RemoveAt(i);
            }
        }

        // ==================== COLLISION DETECTION ====================
        private void CheckCollisions()
        {
            RectangleF playerBox = new RectangleF(playerX + 12, playerY + 12, 40, 40);
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = enemies[i];
                RectangleF enemyBox = new RectangleF(
                    enemy.X + 8, enemy.Y + 8, enemy.Size - 16, enemy.Size - 16);
                bool defeated = false;

                for (int j = bullets.Count - 1; j >= 0; j--)
                {
                    RectangleF bulletBox = new RectangleF(bullets[j].X, bullets[j].Y, 7, 22);
                    if (enemyBox.IntersectsWith(bulletBox))
                    {
                        bullets.RemoveAt(j);
                        enemy.Health--;
                        if (enemy.Health <= 0)
                        {
                            score += enemy.Type == 3 ? 100 : (enemy.Type + 1) * 10;
                            enemies.RemoveAt(i);
                            defeated = true;
                        }
                        break;
                    }
                }
                if (defeated) continue;

                if (invincibleFrames == 0 && enemyBox.IntersectsWith(playerBox))
                {
                    enemies.RemoveAt(i);
                    lives--;
                    invincibleFrames = 80;
                    if (lives <= 0) gameOver = true;
                }
            }
        }

        // ==================== GAME START AND MAIN MENU ====================
        private void StartGame()
        {
            enemies.Clear();
            bullets.Clear();
            pressedKeys.Clear();
            playerX = (gameWidth - 64) / 2f;
            playerY = gameHeight - 110;
            score = 0;
            lives = 3;
            frame = 0;
            shootCooldown = 0;
            invincibleFrames = 0;
            gameOver = false;
            scoreSaved = false;
            currentScreen = GameScreen.Playing;
            Invalidate();
        }

        private void ShowMenu()
        {
            pressedKeys.Clear();
            currentScreen = GameScreen.Menu;
            Invalidate();
        }

        // ==================== LEADERBOARD: LOAD SCORES ====================
        private void LoadHighScores()
        {
            highScores.Clear();
            if (!File.Exists(scoreFile)) return;

            foreach (string line in File.ReadAllLines(scoreFile))
            {
                string[] parts = line.Split('\t');
                int points;
                if (parts.Length == 2 && int.TryParse(parts[1], out points))
                    highScores.Add(new ScoreEntry(parts[0], points));
                else if (int.TryParse(line, out points))
                    highScores.Add(new ScoreEntry("Unknown", points));
            }
            highScores.Sort((a, b) => b.Points.CompareTo(a.Points));
            if (highScores.Count > 5)
                highScores.RemoveRange(5, highScores.Count - 5);
        }

        // ==================== LEADERBOARD: SAVE SCORES ====================
        private void SaveHighScore(string playerName)
        {
            if (scoreSaved) return;
            playerName = (playerName ?? "").Trim()
                .Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
            if (playerName.Length == 0) playerName = "Player";
            if (playerName.Length > 12) playerName = playerName.Substring(0, 12);

            highScores.Add(new ScoreEntry(playerName, score));
            highScores.Sort((a, b) => b.Points.CompareTo(a.Points));
            if (highScores.Count > 5)
                highScores.RemoveRange(5, highScores.Count - 5);

            List<string> lines = new List<string>();
            foreach (ScoreEntry entry in highScores)
                lines.Add(entry.Name + "\t" + entry.Points);
            File.WriteAllLines(scoreFile, lines);
            scoreSaved = true;
        }

        // ==================== LEADERBOARD: NAME ENTRY ====================
        private void AskPlayerName()
        {
            using (Form nameForm = new Form())
            {
                nameForm.Text = "Save Your Score";
                nameForm.ClientSize = new Size(300, 180);
                nameForm.StartPosition = FormStartPosition.CenterParent;
                nameForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                nameForm.MaximizeBox = false;
                nameForm.MinimizeBox = false;

                Label title = new Label();
                title.Text = "Enter your name:";
                title.Location = new Point(30, 20);
                title.AutoSize = true;

                TextBox nameBox = new TextBox();
                nameBox.Location = new Point(30, 55);
                nameBox.Width = 240;
                nameBox.MaxLength = 12;

                Button saveButton = new Button();
                saveButton.Text = "SAVE SCORE";
                saveButton.Location = new Point(75, 105);
                saveButton.Size = new Size(150, 35);
                saveButton.DialogResult = DialogResult.OK;

                nameForm.Controls.Add(title);
                nameForm.Controls.Add(nameBox);
                nameForm.Controls.Add(saveButton);
                nameForm.AcceptButton = saveButton;

                if (nameForm.ShowDialog(this) == DialogResult.OK)
                    SaveHighScore(nameBox.Text);
            }
        }

        // ==================== MAIN SCREEN ====================
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(backgroundImage, new Rectangle(0, (int)backgroundY - gameHeight, gameWidth, gameHeight));
            g.DrawImage(backgroundImage, new Rectangle(0, (int)backgroundY, gameWidth, gameHeight));

            if (currentScreen == GameScreen.Menu) { DrawMenu(g); return; }
            if (currentScreen == GameScreen.HighScores) { DrawHighScores(g); return; }

            foreach (Enemy enemy in enemies)
                g.DrawImage(catImages[enemy.Type], enemy.X, enemy.Y, enemy.Size, enemy.Size);
            foreach (Bullet bullet in bullets)
            {
                g.FillRectangle(Brushes.Cyan, bullet.X, bullet.Y, 7, 22);
                g.FillRectangle(Brushes.White, bullet.X + 2, bullet.Y + 3, 3, 14);
            }
            if (currentScreen == GameScreen.Playing &&
                (invincibleFrames == 0 || frame % 8 < 4))
                g.DrawImage(playerImage, playerX, playerY, 64, 64);

            using (Font hudFont = new Font("Segoe UI", 15, FontStyle.Bold))
                g.DrawString("SCORE: " + score + "    LIVES: " + lives,
                    hudFont, Brushes.White, 14, 12);

            if (currentScreen == GameScreen.GameOver)
                DrawGameOverMenu(g);
        }

        // ==================== MAIN MENU ====================
        private void DrawMenu(Graphics g)
        {
            using (Font title = new Font("Segoe UI", 30, FontStyle.Bold))
            using (Font menu = new Font("Segoe UI", 17, FontStyle.Bold))
            {
                DrawCentered(g, "CAT SHOOTERS", title, Brushes.Cyan, 160);
                DrawCentered(g, "SPACE CAT INVASION", menu, Brushes.White, 230);
                DrawCentered(g, "[ 1 ] PLAY GAME", menu, Brushes.White, 350);
                DrawCentered(g, "[ 2 ] HIGH SCORES", menu, Brushes.Yellow, 420);
                DrawCentered(g, "[ 3 ] EXIT", menu, Brushes.White, 490);
            }
        }

        // ==================== LEADERBOARD ====================
        private void DrawHighScores(Graphics g)
        {
            using (Font title = new Font("Segoe UI", 28, FontStyle.Bold))
            using (Font row = new Font("Segoe UI", 16, FontStyle.Bold))
            {
                DrawCentered(g, "LEADERBOARD", title, Brushes.Yellow, 100);
                if (highScores.Count == 0)
                    DrawCentered(g, "NO SCORES YET", row, Brushes.White, 270);
                else
                {
                    for (int i = 0; i < highScores.Count; i++)
                    {
                        ScoreEntry entry = highScores[i];
                        string line = (i + 1) + ". " + entry.Name + " - " + entry.Points;
                        DrawCentered(g, line, row, Brushes.White, 190 + i * 60);
                    }
                }
                DrawCentered(g, "ESC - BACK TO MENU", row, Brushes.Cyan, 580);
            }
        }

        // ==================== GAME OVER ====================
        private void DrawGameOverMenu(Graphics g)
        {
            using (SolidBrush shade = new SolidBrush(Color.FromArgb(190, 0, 0, 0)))
            using (Font title = new Font("Segoe UI", 36, FontStyle.Bold))
            using (Font menu = new Font("Segoe UI", 17, FontStyle.Bold))
            {
                g.FillRectangle(shade, 0, 0, gameWidth, gameHeight);
                DrawCentered(g, "GAME OVER", title, Brushes.Red, 160);
                DrawCentered(g, "FINAL SCORE: " + score, menu, Brushes.White, 260);
                DrawCentered(g, "[ ENTER ] RESTART", menu, Brushes.Cyan, 365);
                DrawCentered(g, "[ M ] MAIN MENU", menu, Brushes.White, 425);
            }
        }

        // ==================== DRAWING ====================
        private void DrawCentered(Graphics g, string text, Font font, Brush brush, float y)
        {
            SizeF size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, (gameWidth - size.Width) / 2, y);
        }

        // ==================== GAME CLASSES ====================
        private class Enemy
        {
            public float X, Y, Speed;
            public int Size, Health, Type;
            public Enemy(float x, float y, int size, float speed, int health, int type)
            {
                X = x; Y = y; Size = size; Speed = speed; Health = health; Type = type;
            }
        }

        private class Bullet
        {
            public float X, Y;
            public Bullet(float x, float y) { X = x; Y = y; }
        }

        private class ScoreEntry
        {
            public string Name;
            public int Points;
            public ScoreEntry(string name, int points) { Name = name; Points = points; }
        }

        // ==================== CLEANUP ====================
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            gameTimer.Stop();
            gameTimer.Dispose();
            base.OnFormClosed(e);
        }
    }
}
