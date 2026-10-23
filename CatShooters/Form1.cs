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
        // GAME SETTINGS
        int gameWidth = 450;
        int gameHeight = 700;
        Timer gameTimer = new Timer();
        Random random = new Random();

        // GAME SCREENS
        enum GameScreen { Menu, Playing, HighScores, GameOver }
        GameScreen currentScreen = GameScreen.Menu;

        // PLAYER
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

        // POWER UPS
        List<PowerUp> powerUps = new List<PowerUp>();
        int rapidFireFrames = 0;
        int doubleShotFrames = 0;
        int shieldFrames = 0;

        // BOSS
        bool bossAppeared = false;
        GiantBoss boss = null;
        List<BossBullet> bossBullets = new List<BossBullet>();

        // SCORES
        List<ScoreEntry> highScores = new List<ScoreEntry>();
        string scoreFile = Path.Combine(Application.StartupPath, "highscores.txt");
        bool scoreSaved = false;

        // SETUP
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

        // CONTROLS
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

        // GAME LOOP
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
            if (rapidFireFrames > 0) rapidFireFrames--;
            if (doubleShotFrames > 0) doubleShotFrames--;
            if (shieldFrames > 0) shieldFrames--;

            MovePlayer();
            Shoot();
            SpawnGiantBoss();
            SpawnEnemies();
            MoveBullets();
            MoveEnemies();
            MoveBoss();
            MoveBossBullets();
            MovePowerUps();
            CheckCollisions();
            CheckBossCollisions();
            CheckPowerUpCollisions();

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

        // START AND MENU
        private void StartGame()
        {
            enemies.Clear();
            bullets.Clear();
            powerUps.Clear();
            bossBullets.Clear();
            boss = null;
            bossAppeared = false;
            rapidFireFrames = 0;
            doubleShotFrames = 0;
            shieldFrames = 0;
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

        // PLAYER
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

        // SHOOTING
        private void Shoot()
        {
            if (shootCooldown == 0)
            {
                if (doubleShotFrames > 0)
                {
                    bullets.Add(new Bullet(playerX + 12, playerY - 12));
                    bullets.Add(new Bullet(playerX + 46, playerY - 12));
                }
                else
                {
                    bullets.Add(new Bullet(playerX + 29, playerY - 12));
                }

                shootCooldown = rapidFireFrames > 0 ? 5 : 12;
            }
        }

        // BULLETS
        private void MoveBullets()
        {
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                bullets[i].Y -= 10;
                if (bullets[i].Y < -30) bullets.RemoveAt(i);
            }
        }

        private class Bullet
        {
            public float X, Y;
            public Bullet(float x, float y) { X = x; Y = y; }
        }

        // ENEMIES
        private void SpawnEnemies()
        {
            if (boss != null) return;
            int spawnRate = Math.Max(20, 48 - score / 150);
            if (frame % spawnRate != 0) return;

            int roll = random.Next(100);
            int type;
            if (roll < 50) type = 0;
            else if (roll < 78) type = 1;
            else if (roll < 96) type = 2;
            else type = 3;

            Enemy enemy;
            if (type == 0) enemy = new BasicCat(0);
            else if (type == 1) enemy = new FastCat(0);
            else if (type == 2) enemy = new ArmoredCat(0);
            else enemy = new BossCat(0);

            enemy.X = random.Next(0, gameWidth - enemy.Size);
            enemy.Y = -enemy.Size;
            enemies.Add(enemy);
        }

        private void MoveEnemies()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                enemies[i].Move();
                if (enemies[i].Y > gameHeight + 100) enemies.RemoveAt(i);
            }
        }

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
                        enemy.TakeDamage();
                        if (enemy.Health <= 0)
                        {
                            score += enemy.Points;
                            if (random.Next(100) < 18)
                                powerUps.Add(new PowerUp(enemy.X + enemy.Size / 2f,
                                    enemy.Y + enemy.Size / 2f, random.Next(4)));
                            enemies.RemoveAt(i);
                            defeated = true;
                        }
                        break;
                    }
                }
                if (defeated) continue;

                if (invincibleFrames == 0 && shieldFrames == 0 &&
                    enemyBox.IntersectsWith(playerBox))
                {
                    enemies.RemoveAt(i);
                    lives--;
                    invincibleFrames = 80;
                    if (lives <= 0) gameOver = true;
                }
            }
        }
        //a
        private abstract class Enemy
        {
            public float X { get; set; }
            public float Y { get; set; }
            public int Size { get; private set; }
            public int Type { get; private set; }
            public int Health { get; private set; }
            public int Points { get; private set; }

            protected Enemy(int size, int health, int type, int points)
            {
                Size = size;
                Health = health;
                Type = type;
                Points = points;
            }
            //e
            public void TakeDamage()
            {
                if (Health > 0) Health--;
            }

            public abstract void Move();
        }
        //in
        private class BasicCat : Enemy
        {
            public BasicCat(float x) : base(54, 1, 0, 10) { X = x; }
            public override void Move() { Y += 2.5f; }
        }
        //poly
        private class FastCat : Enemy
        {
            public FastCat(float x) : base(58, 1, 1, 20) { X = x; }
            public override void Move() { Y += 5f; }
        }

        private class ArmoredCat : Enemy
        {
            public ArmoredCat(float x) : base(75, 3, 2, 30) { X = x; }
            public override void Move() { Y += 1.8f; }
        }

        private class BossCat : Enemy
        {
            public BossCat(float x) : base(110, 10, 3, 100) { X = x; }
            public override void Move() { Y += 1.2f; }
        }

        // POWER UPS
        private void MovePowerUps()
        {
            for (int i = powerUps.Count - 1; i >= 0; i--)
            {
                powerUps[i].Y += 2.5f;
                if (powerUps[i].Y > gameHeight)
                    powerUps.RemoveAt(i);
            }
        }

        private void CheckPowerUpCollisions()
        {
            RectangleF playerBox = new RectangleF(playerX + 8, playerY + 8, 48, 48);
            for (int i = powerUps.Count - 1; i >= 0; i--)
            {
                PowerUp item = powerUps[i];
                RectangleF itemBox = new RectangleF(item.X - 14, item.Y - 14, 28, 28);
                if (!playerBox.IntersectsWith(itemBox)) continue;

                if (item.Type == 0) rapidFireFrames = 600;
                if (item.Type == 1) doubleShotFrames = 600;
                if (item.Type == 2) shieldFrames = 600;
                if (item.Type == 3) lives = Math.Min(5, lives + 1);
                powerUps.RemoveAt(i);
            }
        }

        // POWER UPS
        private class PowerUp
        {
            public float X, Y;
            public int Type;
            public PowerUp(float x, float y, int type)
            { X = x; Y = y; Type = type; }
        }

        // BOSS
        private void SpawnGiantBoss()
        {
            if (!bossAppeared && score >= 500)
            {
                bossAppeared = true;
                enemies.Clear();
                bossBullets.Clear();
                boss = new GiantBoss(15, 80, gameWidth - 30, 250, 100);
            }
        }

        private void MoveBoss()
        {
            if (boss == null) return;

            if (boss.Y < 55)
                boss.Y += 1.5f;
            else
            {
                boss.X += boss.Direction * 1.1f;
                if (boss.X < 0 || boss.X + boss.Width > gameWidth)
                    boss.Direction *= -1;
            }

            if (frame % 48 == 0 && boss.Y >= 45)
            {
                float center = boss.X + boss.Width / 2f;
                bossBullets.Add(new BossBullet(center - 8, boss.Y + boss.Height, -2));
                bossBullets.Add(new BossBullet(center - 8, boss.Y + boss.Height, 0));
                bossBullets.Add(new BossBullet(center - 8, boss.Y + boss.Height, 2));
            }
        }

        private void MoveBossBullets()
        {
            for (int i = bossBullets.Count - 1; i >= 0; i--)
            {
                BossBullet shot = bossBullets[i];
                shot.X += shot.SpeedX;
                shot.Y += 4;
                if (shot.Y > gameHeight || shot.X < -30 || shot.X > gameWidth + 30)
                    bossBullets.RemoveAt(i);
            }
        }

        private void CheckBossCollisions()
        {
            RectangleF playerBox = new RectangleF(playerX + 12, playerY + 12, 40, 40);

            if (boss != null)
            {
                RectangleF bossBox = new RectangleF(
                    boss.X + 12, boss.Y + 12, boss.Width - 24, boss.Height - 24);

                for (int i = bullets.Count - 1; i >= 0; i--)
                {
                    RectangleF shotBox = new RectangleF(bullets[i].X, bullets[i].Y, 7, 22);
                    if (!shotBox.IntersectsWith(bossBox)) continue;
                    bullets.RemoveAt(i);
                    boss.Health--;
                    if (boss.Health <= 0)
                    {
                        score += 1000;
                        boss = null;
                        bossBullets.Clear();
                        break;
                    }
                }

                if (boss != null && shieldFrames == 0 && invincibleFrames == 0 &&
                    playerBox.IntersectsWith(bossBox))
                {
                    lives--;
                    invincibleFrames = 90;
                    if (lives <= 0) gameOver = true;
                }
            }

            for (int i = bossBullets.Count - 1; i >= 0; i--)
            {
                BossBullet shot = bossBullets[i];
                RectangleF shotBox = new RectangleF(shot.X, shot.Y, 16, 16);
                if (!shotBox.IntersectsWith(playerBox)) continue;
                bossBullets.RemoveAt(i);
                if (shieldFrames == 0 && invincibleFrames == 0)
                {
                    lives--;
                    invincibleFrames = 90;
                    if (lives <= 0) gameOver = true;
                }
            }
        }

        // BOSS
        private class GiantBoss
        {
            public float X, Y, Width, Height, Direction = 1;
            public int Health, MaxHealth;
            public GiantBoss(float x, float y, float width, float height, int health)
            { X = x; Y = y; Width = width; Height = height; Health = health; MaxHealth = health; }
        }

        private class BossBullet
        {
            public float X, Y, SpeedX;
            public BossBullet(float x, float y, float speedX)
            { X = x; Y = y; SpeedX = speedX; }
        }

        private class ScoreEntry
        {
            public string Name;
            public int Points;
            public ScoreEntry(string name, int points) { Name = name; Points = points; }
        }

        // SCORES
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

        // DRAWING
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

            // POWER-UPS
            using (Font itemFont = new Font("Segoe UI", 11, FontStyle.Bold))
            {
                foreach (PowerUp item in powerUps)
                {
                    Brush color = item.Type == 0 ? Brushes.Orange :
                        item.Type == 1 ? Brushes.Cyan :
                        item.Type == 2 ? Brushes.Lime : Brushes.HotPink;
                    string letter = item.Type == 0 ? "R" :
                        item.Type == 1 ? "D" : item.Type == 2 ? "S" : "+";
                    g.FillEllipse(color, item.X - 16, item.Y - 16, 32, 32);
                    g.DrawEllipse(Pens.White, item.X - 16, item.Y - 16, 32, 32);
                    g.DrawString(letter, itemFont, Brushes.Black, item.X - 7, item.Y - 11);
                }
            }

            // GIANT BOSS
            if (boss != null)
            {
                g.DrawImage(catImages[3], boss.X, boss.Y, boss.Width, boss.Height);
                g.FillRectangle(Brushes.DarkRed, 30, 62, gameWidth - 60, 14);
                g.FillRectangle(Brushes.LimeGreen, 30, 62,
                    (gameWidth - 60) * Math.Max(0, boss.Health) / boss.MaxHealth, 14);
                using (Font bossFont = new Font("Segoe UI", 12, FontStyle.Bold))
                    DrawCentered(g, "GIANT CAT BOSS", bossFont, Brushes.White, 35);
            }

            foreach (BossBullet shot in bossBullets)
            {
                g.FillEllipse(Brushes.OrangeRed, shot.X, shot.Y, 16, 16);
                g.DrawEllipse(Pens.Yellow, shot.X, shot.Y, 16, 16);
            }
            foreach (Bullet bullet in bullets)
            {
                g.FillRectangle(Brushes.Cyan, bullet.X, bullet.Y, 7, 22);
                g.FillRectangle(Brushes.White, bullet.X + 2, bullet.Y + 3, 3, 14);
            }
            if (currentScreen == GameScreen.Playing &&
                (invincibleFrames == 0 || frame % 8 < 4))
                g.DrawImage(playerImage, playerX, playerY, 64, 64);

            // HUD
            using (Font hudFont = new Font("Segoe UI", 15, FontStyle.Bold))
                g.DrawString("SCORE: " + score + "    LIVES: " + lives,
                    hudFont, Brushes.White, 14, 12);

            string effects = "";
            if (rapidFireFrames > 0) effects += "RAPID ";
            if (doubleShotFrames > 0) effects += "DOUBLE ";
            if (shieldFrames > 0) effects += "SHIELD ";
            if (effects.Length > 0)
            {
                using (Font effectFont = new Font("Segoe UI", 11, FontStyle.Bold))
                    g.DrawString(effects, effectFont, Brushes.Yellow, 14, 43);
            }

            if (shieldFrames > 0 && currentScreen == GameScreen.Playing)
                g.DrawEllipse(Pens.Lime, playerX - 8, playerY - 8, 80, 80);

            if (currentScreen == GameScreen.GameOver)
                DrawGameOverMenu(g);
        }

        // DRAW MENU
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

        // DRAW SCORES
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

        // DRAW GAME OVER
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

        // TEXT
        private void DrawCentered(Graphics g, string text, Font font, Brush brush, float y)
        {
            SizeF size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, (gameWidth - size.Width) / 2, y);
        }

        // CLEANUP
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            gameTimer.Stop();
            gameTimer.Dispose();
            base.OnFormClosed(e);
        }
    }
}
